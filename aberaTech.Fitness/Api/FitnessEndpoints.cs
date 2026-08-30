using System.Security.Claims;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using aberaTech.Fitness.Ingest;
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
    // When set, the anchor VDOT is computed from this race instead of StartVdot.
    double? AnchorDistanceMeters = null,
    double? AnchorSeconds = null);

public sealed record BodyMetricUpdate(string Date, double WeightKg, double? BodyFatPercent);

public sealed record GoalUpdate(string Metric, double TargetValue, string TargetDate);

/// <summary>The fitness API. Personal health data, so everything requires the owner policy.</summary>
public static class FitnessEndpoints
{
    public const string PolicyName = "fitness-owner";

    /// <summary>Uploads are files the owner picked; 10 MB covers years of history.</summary>
    private const long MaxUploadBytes = 10 * 1024 * 1024;

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

        api.MapPost("/import/hevy-csv", async (HttpRequest request, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var text = await ReadBodyAsync(request, cancellationToken);
            if (text is null) return Results.BadRequest("Empty or oversized upload.");

            var activities = HevyCsv.Parse(text, DateTimeZoneProviders.Tzdb["Etc/UTC"]);
            var added = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
            return Results.Ok(new { parsed = activities.Count, added });
        });

        api.MapPost("/import/garmin-csv", async (HttpRequest request, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var text = await ReadBodyAsync(request, cancellationToken);
            if (text is null) return Results.BadRequest("Empty or oversized upload.");

            var activities = GarminActivitiesCsv.Parse(text, DateTimeZoneProviders.Tzdb["Etc/UTC"]);
            var added = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
            return Results.Ok(new { parsed = activities.Count, added });
        });

        api.MapPost("/import/fit", async (HttpRequest request, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length is 0 or > MaxUploadBytes) return Results.BadRequest("Empty or oversized upload.");

            buffer.Position = 0;
            var activity = Ingest.FitImport.Parse(buffer);
            if (activity is null) return Results.BadRequest("Not a decodable FIT activity file.");

            var added = await ActivityStore.UpsertAsync(database, [activity], cancellationToken);
            return Results.Ok(new { parsed = 1, added });
        });

        if (options.HasHevyApi)
        {
            api.MapPost("/sync/hevy", async (HevyApiClient hevy, FitnessDbContext database, CancellationToken cancellationToken) =>
            {
                var activities = await hevy.FetchAllAsync(cancellationToken);
                var added = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
                return Results.Ok(new { fetched = activities.Count, added });
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

            row.ReferenceHr = update.ReferenceHr;
            row.LtSecondsPerKm = update.LtSecondsPerKm;
            row.PlanMinutesPerWeek = update.PlanMinutesPerWeek;
            row.VdotMeasuredOn = ParseDate(update.VdotMeasuredOn);
            row.BirthYear = update.BirthYear;
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

    private static async Task<string?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 or > MaxUploadBytes) return null;
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static LocalDate? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = LocalDatePattern.Iso.Parse(value.Trim());
        return parsed.Success ? parsed.Value : null;
    }
}
