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
    double? PastPeakDistanceMeters = null,
    double? PastPeakSeconds = null,
    int? PastPeakYear = null,
    double HomeAltitudeMeters = 0,
    double? PastAltitudeMeters = null,
    // When set, the anchor VDOT is computed from this race instead of StartVdot.
    double? AnchorDistanceMeters = null,
    double? AnchorSeconds = null);

public sealed record BodyMetricUpdate(string Date, double WeightKg, double? BodyFatPercent);

public sealed record GoalUpdate(string Metric, double TargetValue, string TargetDate);

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

        api.MapGet("/predictions", async (
            FitnessDbContext database,
            double weeklyHours,
            double compliance,
            double? targetWeightKg,
            CancellationToken cancellationToken) =>
        {
            if (weeklyHours is < 0 or > 40 || compliance is < 0 or > 1)
            {
                return Results.BadRequest("weeklyHours 0-40, compliance 0-1.");
            }

            var prediction = await FitnessReports.PredictionsAsync(
                database, weeklyHours, compliance, targetWeightKg,
                DateTime.UtcNow.Year, cancellationToken);
            return Results.Ok(prediction);
        });

        api.MapGet("/predictions/required", async (
            FitnessDbContext database,
            double distanceMeters,
            double targetSeconds,
            double monthsAvailable,
            double compliance,
            CancellationToken cancellationToken) =>
        {
            if (distanceMeters is < 400 or > 100_000 || targetSeconds <= 0
                || monthsAvailable is <= 0 or > 120 || compliance is <= 0 or > 1)
            {
                return Results.BadRequest("Out-of-range goal parameters.");
            }

            var settings = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
                           ?? new AthleteSettings { Id = 1 };

            return Results.Ok(FitnessReports.RequiredDose(
                settings.StartVdot,
                FitnessReports.ReclaimVdotFrom(settings, DateTime.UtcNow.Year),
                settings.HomeAltitudeMeters,
                distanceMeters, targetSeconds, monthsAvailable, compliance));
        });

        // One import route, whatever the file is. Asking the owner to classify
        // their own download is how the Garmin archive — the file Garmin
        // actually sends — ended up fitting none of the three buttons there
        // used to be here.
        api.MapPost("/import", async (HttpRequest request, FitnessDbContext database, CancellationToken cancellationToken) =>
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
                return Results.BadRequest("That file is larger than 100 MB.");
            }

            if (buffer.Length == 0) return Results.BadRequest("Empty upload.");
            if (buffer.Length > MaxUploadBytes) return Results.BadRequest("That file is larger than 100 MB.");

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
                return Results.BadRequest(exception.Message);
            }

            var outcome = await ActivityStore.UpsertAsync(database, result.Activities, cancellationToken);
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
            api.MapPost("/sync/hevy", async (HevyApiClient hevy, FitnessDbContext database, CancellationToken cancellationToken) =>
            {
                var activities = await hevy.FetchAllAsync(cancellationToken);
                var outcome = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
                return Results.Ok(new { fetched = activities.Count, added = outcome.Added });
            });
        }

        api.MapGet("/activities", (FitnessDbContext database, CancellationToken cancellationToken) =>
            database.Activities
                .OrderByDescending(a => a.StartedAt)
                .Take(50)
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
                .ToListAsync(cancellationToken));

        api.MapPut("/settings", async (SettingsUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            if (update.ReferenceHr is < 80 or > 220) return Results.BadRequest("Reference HR out of range.");

            var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (row is null)
            {
                row = new AthleteSettings { Id = 1 };
                database.Settings.Add(row);
            }

            if (update.HomeAltitudeMeters is < 0 or > 5000)
            {
                return Results.BadRequest("Home altitude 0-5000 m.");
            }

            if (update.PastAltitudeMeters is < 0 or > 5000)
            {
                return Results.BadRequest("Past altitude 0-5000 m.");
            }

            row.ReferenceHr = update.ReferenceHr;
            row.LtSecondsPerKm = update.LtSecondsPerKm;
            row.PlanMinutesPerWeek = update.PlanMinutesPerWeek;
            row.VdotMeasuredOn = ParseDate(update.VdotMeasuredOn);
            row.BirthYear = update.BirthYear;
            row.PastPeakDistanceMeters = update.PastPeakDistanceMeters;
            row.PastPeakSeconds = update.PastPeakSeconds;
            row.PastPeakYear = update.PastPeakYear;
            row.HomeAltitudeMeters = update.HomeAltitudeMeters;
            row.PastAltitudeMeters = update.PastAltitudeMeters;

            // A race is the honest way to state the anchor; raw VDOT stays as
            // the escape hatch. The race happened where the athlete was then,
            // which is not necessarily where they are now, so its sea-level
            // equivalent is taken at the past altitude.
            if (update is { AnchorDistanceMeters: { } anchorDistance, AnchorSeconds: { } anchorSeconds }
                && anchorDistance is >= 400 and <= 100_000 && anchorSeconds > 0)
            {
                row.StartVdot = Domain.Vdot.FromRace(
                    anchorDistance,
                    Domain.Altitude.ToSeaLevel(anchorSeconds, row.PastAltitudeOrHome) / 60.0);
            }
            else
            {
                row.StartVdot = update.StartVdot;
            }

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/body-metrics", async (BodyMetricUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var date = ParseDate(update.Date);
            if (date is null || update.WeightKg is <= 30 or > 250) return Results.BadRequest("Implausible weigh-in.");

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
                return Results.BadRequest("A goal needs a metric, a positive target and a date.");
            }

            var existing = await database.Goals.SingleOrDefaultAsync(g => g.Metric == update.Metric, cancellationToken);
            if (existing is null)
            {
                database.Goals.Add(new Goal
                {
                    Id = Guid.NewGuid(),
                    Metric = update.Metric,
                    TargetValue = update.TargetValue,
                    TargetDate = date.Value
                });
            }
            else
            {
                existing.TargetValue = update.TargetValue;
                existing.TargetDate = date.Value;
            }

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return routes;
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

    private static LocalDate? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = LocalDatePattern.Iso.Parse(value.Trim());
        return parsed.Success ? parsed.Value : null;
    }
}
