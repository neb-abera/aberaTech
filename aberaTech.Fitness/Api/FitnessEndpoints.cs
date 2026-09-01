using System.Security.Claims;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using aberaTech.Fitness.Ingest;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Api;

public sealed record SettingsUpdate(
    int ReferenceHr,
    double? LtSecondsPerKm,
    double PlanMinutesPerWeek,
    double StartVdot,
    string? VdotMeasuredOn,
    int? BirthYear = null,
    bool? Female = null,
    double AvailableHoursPerWeek = 7,
    double? PastPeakDistanceMeters = null,
    double? PastPeakSeconds = null,
    int? PastPeakYear = null,
    double HomeAltitudeMeters = 0,
    // When set, the anchor VDOT is computed from this race instead of StartVdot.
    double? AnchorDistanceMeters = null,
    double? AnchorSeconds = null);

public sealed record BodyMetricUpdate(string Date, double WeightKg, double? BodyFatPercent);

public sealed record GoalUpdate(
    string Metric,
    double TargetValue,
    string TargetDate,
    double? DistanceMeters = null,
    string? Label = null);

/// <summary>The fitness API. Personal health data, so everything requires the owner policy.</summary>
public static class FitnessEndpoints
{
    public const string PolicyName = "fitness-owner";

    /// <summary>
    /// Uploads are files the owner picked. A Garmin "Export Your Data" archive
    /// carries every original .fit file alongside the summaries, so the ceiling
    /// is the archive's, not a CSV's; what the archive is allowed to weigh once
    /// decompressed is Import's business.
    /// </summary>
    private const long MaxUploadBytes = 100L * 1024 * 1024;

    public static IServiceCollection AddFitnessAuthorization(this IServiceCollection services, FitnessOptions options)
    {
        // The cookie and Google schemes are registered by the admin auth the
        // server already configures; this policy only decides who they let in.
        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyName, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => options.Allows(
                    context.User.FindFirstValue(ClaimTypes.Email))));

        return services;
    }

    public static IEndpointRouteBuilder MapFitnessEndpoints(
        this IEndpointRouteBuilder routes,
        FitnessOptions options,
        bool requireOwnerSignIn = true)
    {
        var group = routes.MapGroup("/api/fitness");

        // The Development bypass is the one case with no policy: sign-in is
        // skipped entirely, decided by FitnessGate, never by this default.
        var api = requireOwnerSignIn ? group.RequireAuthorization(PolicyName) : group;

        // Who am I — the one anonymous route, so the page can decide whether to
        // show a sign-in button or the dashboard. Discloses nothing but the
        // fact the feature exists, which the navigation already does.
        routes.MapGet("/api/fitness/me", (HttpContext context) =>
        {
            var email = context.User.FindFirstValue(ClaimTypes.Email);
            var signedIn = !requireOwnerSignIn
                           || (context.User.Identity?.IsAuthenticated == true && options.Allows(email));
            return Results.Ok(new
            {
                configured = true,
                signedIn,
                hevyApi = options.HasHevyApi
            });
        });

        api.MapGet("/summary", (FitnessDbContext database, CancellationToken cancellationToken) =>
            FitnessReports.SummaryAsync(database, cancellationToken));

        api.MapGet("/citations", () => Results.Ok(Citations.All));

        api.MapSolverEndpoints();

        api.MapGet("/predictions", async (
            FitnessDbContext database,
            HttpRequest request,
            double? weeklyHours,
            double? easyHours,
            double? thresholdHours,
            double? intervalHours,
            double? strengthHours,
            double compliance,
            double? targetWeightKg,
            CancellationToken cancellationToken) =>
        {
            if (compliance is < 0 or > 1) return Fail("compliance 0-1.");

            // Either name the week zone by zone, or give a total and let the
            // model split it the way it would advise splitting it.
            var named = easyHours ?? thresholdHours ?? intervalHours;
            TrainingDose plan;
            if (named is not null)
            {
                if (new[] { easyHours, thresholdHours, intervalHours, strengthHours }
                    .Any(h => h is < 0 or > 40))
                {
                    return Fail("Each zone takes 0-40 hours a week.");
                }

                plan = new TrainingDose(
                    easyHours ?? 0, thresholdHours ?? 0, intervalHours ?? 0, strengthHours ?? 0);
            }
            else
            {
                if (weeklyHours is not { } total || total is < 0 or > 40)
                {
                    return Fail("weeklyHours 0-40, or name the hours by zone.");
                }

                plan = DoseResponse.Allocate(total).Dose with { StrengthHours = strengthHours ?? 0 };
            }

            var distances = ParseNumbers(request.Query["distances"], 400, 100_000)
                            ?? FitnessReports.DefaultDistances;
            var horizons = ParseNumbers(request.Query["horizons"], 0, 120)
                           ?? FitnessReports.DefaultHorizons;

            if (distances.Count > 8 || horizons.Count > 12)
            {
                return Fail("At most 8 distances and 12 horizons.");
            }

            return Results.Ok(await FitnessReports.PredictionsAsync(
                database, plan, compliance, targetWeightKg, distances, horizons,
                DateTime.UtcNow.Year, cancellationToken));
        });

        // The inverse question, over any distance and any date the athlete
        // names rather than a menu of four.
        api.MapGet("/predictions/goal", async (
            FitnessDbContext database,
            double distanceMeters,
            double targetSeconds,
            double monthsAvailable,
            double? availableHours,
            CancellationToken cancellationToken) =>
        {
            if (distanceMeters is < 400 or > 100_000 || targetSeconds <= 0
                || monthsAvailable is <= 0 or > 120
                || availableHours is < 0 or > 40)
            {
                return Fail("Out-of-range goal parameters.");
            }

            var settings = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
                           ?? new AthleteSettings { Id = 1 };

            return Results.Ok(await FitnessReports.GoalAsync(
                database, distanceMeters, targetSeconds, monthsAvailable,
                availableHours ?? settings.AvailableHoursPerWeek,
                DateTime.UtcNow.Year, cancellationToken));
        });

        // One import route, whatever the file is. Asking the owner to classify
        // their own download is how the Garmin archive — the file Garmin
        // actually sends — ended up fitting none of the three buttons there
        // used to be here.
        api.MapPost("/import", async (HttpRequest request, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            // Kestrel's own 30 MB default would reject a Garmin archive before
            // this handler ever ran.
            var sizeLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeLimit is { IsReadOnly: false }) sizeLimit.MaxRequestBodySize = MaxUploadBytes;

            using var buffer = new MemoryStream();
            try
            {
                await request.Body.CopyToAsync(buffer, cancellationToken);
            }
            catch (BadHttpRequestException)
            {
                return Fail("That file is larger than 100 MB.");
            }

            if (buffer.Length == 0) return Fail("Empty upload.");
            if (buffer.Length > MaxUploadBytes) return Fail("That file is larger than 100 MB.");

            buffer.Position = 0;

            ImportResult result;
            try
            {
                result = Import.Read(buffer, DateTimeZoneProviders.Tzdb["Etc/UTC"]);
            }
            catch (Exception exception) when (exception is FormatException or InvalidDataException)
            {
                // What the file was is the owner's problem to fix, so say it
                // rather than answering a bare 400.
                return Fail(exception.Message);
            }

            var outcome = await ActivityStore.UpsertAsync(database, result.Activities, cancellationToken);

            // New history means the fitted posterior is stale, whatever shape
            // the file arrived in.
            cache.Invalidate();

            return Results.Ok(new
            {
                kind = result.Kind,
                parsed = result.Activities.Count,
                added = outcome.Added,
                // Said out loud rather than folded into the count: uploading
                // both of the files Garmin offers should visibly reconcile,
                // not silently look like half of it did nothing.
                skipped = outcome.Skipped,
                superseded = outcome.Superseded
            });
        });

        if (options.HasHevyApi)
        {
            api.MapPost("/sync/hevy", async (HevyApiClient hevy, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
            {
                var activities = await hevy.FetchAllAsync(cancellationToken);
                var outcome = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
                cache.Invalidate();
                return Results.Ok(new { fetched = activities.Count, added = outcome.Added });
            });
        }

        // The page shows the newest few and says so. Returning a bare fifty made
        // a truncated list look like the whole history.
        api.MapGet("/activities", async (FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            const int limit = 50;

            var rows = await database.Activities
                .OrderByDescending(a => a.StartedAt)
                .Take(limit)
                .Select(a => new
                {
                    a.Id,
                    a.Source,
                    startedAt = a.StartedAt.ToString(),
                    a.Sport,
                    a.Name,
                    a.DistanceMeters,
                    a.DurationSeconds,
                    a.AverageHr
                })
                .ToListAsync(cancellationToken);

            var total = await database.Activities.CountAsync(cancellationToken);

            return Results.Ok(new { activities = rows, total, limit });
        });

        // A bad import has to be undoable from the page. Without this the only
        // way to take a wrong row back out was the database.
        api.MapDelete("/activities/{id:guid}", async (Guid id, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var activity = await database.Activities
                .Include(a => a.Sets)
                .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (activity is null) return Results.NotFound();

            database.Activities.Remove(activity);
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapPut("/settings", async (SettingsUpdate update, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            if (update.ReferenceHr is < 80 or > 220) return Fail("Reference HR out of range.");

            var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (row is null)
            {
                row = new AthleteSettings { Id = 1 };
                database.Settings.Add(row);
            }

            if (update.HomeAltitudeMeters is < 0 or > 5000)
            {
                return Fail("Home altitude 0-5000 m.");
            }

            row.ReferenceHr = update.ReferenceHr;
            row.LtSecondsPerKm = update.LtSecondsPerKm;
            row.PlanMinutesPerWeek = update.PlanMinutesPerWeek;
            row.VdotMeasuredOn = ParseDate(update.VdotMeasuredOn);
            row.BirthYear = update.BirthYear;
            row.Female = update.Female;
            row.AvailableHoursPerWeek = Math.Clamp(update.AvailableHoursPerWeek, 0, 40);
            row.PastPeakDistanceMeters = update.PastPeakDistanceMeters;
            row.PastPeakSeconds = update.PastPeakSeconds;
            row.PastPeakYear = update.PastPeakYear;
            row.HomeAltitudeMeters = update.HomeAltitudeMeters;

            // A race is the honest way to state the anchor; raw VDOT stays as
            // the escape hatch. The race happened at home altitude, so its
            // sea-level equivalent is what scores it.
            if (update is { AnchorDistanceMeters: { } anchorDistance, AnchorSeconds: { } anchorSeconds }
                && anchorDistance is >= 400 and <= 100_000 && anchorSeconds > 0)
            {
                row.StartVdot = Domain.Vdot.FromRace(
                    anchorDistance,
                    Domain.Altitude.ToSeaLevel(anchorSeconds, update.HomeAltitudeMeters) / 60.0);
            }
            else
            {
                row.StartVdot = update.StartVdot;
            }

            await database.SaveChangesAsync(cancellationToken);

            // The anchor is what the priors are built from, so moving it moves
            // the posterior.
            cache.Invalidate();

            return Results.NoContent();
        });

        api.MapPost("/body-metrics", async (BodyMetricUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var date = ParseDate(update.Date);
            if (date is null || update.WeightKg is <= 30 or > 250) return Fail("Implausible weigh-in.");

            var existing = await database.BodyMetrics.SingleOrDefaultAsync(m => m.Date == date, cancellationToken);
            if (existing is null)
            {
                database.BodyMetrics.Add(new BodyMetric
                {
                    Id = Guid.NewGuid(),
                    Date = date.Value,
                    WeightKg = update.WeightKg,
                    BodyFatPercent = update.BodyFatPercent
                });
            }
            else
            {
                existing.WeightKg = update.WeightKg;
                existing.BodyFatPercent = update.BodyFatPercent;
            }

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapPut("/goals", async (GoalUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var date = ParseDate(update.TargetDate);
            if (date is null || update.TargetValue <= 0 || string.IsNullOrWhiteSpace(update.Metric))
            {
                return Fail("A goal needs a metric, a positive target and a date.");
            }

            if (update.DistanceMeters is { } distance && distance is < 400 or > 100_000)
            {
                return Fail("A running goal is over 400 m to 100 km.");
            }

            var existing = await database.Goals.SingleOrDefaultAsync(g => g.Metric == update.Metric, cancellationToken);
            if (existing is null)
            {
                database.Goals.Add(new Goal
                {
                    Id = Guid.NewGuid(),
                    Metric = update.Metric,
                    TargetValue = update.TargetValue,
                    TargetDate = date.Value,
                    DistanceMeters = update.DistanceMeters,
                    Label = update.Label
                });
            }
            else
            {
                existing.TargetValue = update.TargetValue;
                existing.TargetDate = date.Value;
                existing.DistanceMeters = update.DistanceMeters ?? existing.DistanceMeters;
                existing.Label = update.Label ?? existing.Label;
            }

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapDelete("/goals/{metric}", async (
            string metric, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var existing = await database.Goals.SingleOrDefaultAsync(g => g.Metric == metric, cancellationToken);
            if (existing is null) return Results.NotFound();

            database.Goals.Remove(existing);
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return routes;
    }

    /// <summary>Comma-separated numbers from the query string, validated, or null when absent.</summary>
    private static IReadOnlyList<double>? ParseNumbers(string? raw, double low, double high)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var values = new List<double>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!double.TryParse(part, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value < low || value > high)
            {
                return null;
            }

            values.Add(value);
        }

        return values.Count > 0 ? values : null;
    }

    /// <summary>The identity endpoint alone, for a deployment where fitness is not configured.</summary>
    public static IEndpointRouteBuilder MapFitnessUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/fitness/me", () => Results.Ok(new
        {
            configured = false,
            signedIn = false,
            hevyApi = false
        }));

        return routes;
    }

    /// <summary>
    /// A 400 whose body is the message itself.
    ///
    /// Results.BadRequest(string) serialises the message as a JSON string, and
    /// the page shows the response body verbatim, so the reader was handed the
    /// quotes as well: <c>holiday.jpg: "Nothing importable in that file."</c>
    /// </summary>
    private static IResult Fail(string message) =>
        Results.Text(message, "text/plain", statusCode: StatusCodes.Status400BadRequest);

    private static LocalDate? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = LocalDatePattern.Iso.Parse(value.Trim());
        return parsed.Success ? parsed.Value : null;
    }
}
