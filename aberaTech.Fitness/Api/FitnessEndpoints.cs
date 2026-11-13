using System.Globalization;
using System.Security.Claims;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using aberaTech.Fitness.Ingest;
using aberaTech.Fitness.IntervalsIcu;
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
    double? SustainedWeeklyHours = null,
    double? PastPeakDistanceMeters = null,
    double? PastPeakSeconds = null,
    int? PastPeakYear = null,
    double? PastPeakWeightKg = null,
    double? GoalWeightKg = null,
    double HomeAltitudeMeters = 0,
    // When set, the anchor VDOT is computed from this race instead of StartVdot.
    double? AnchorDistanceMeters = null,
    double? AnchorSeconds = null,
    int? LtHr = null,
    double? TreadmillGradePercent = null,
    string? TreadmillGradeSince = null);

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
        bool requireOwnerSignIn = true,
        IntervalsIcuOptions? intervalsIcu = null,
        OneDrive.OneDriveOptions? oneDrive = null)
    {
        intervalsIcu ??= new IntervalsIcuOptions();
        oneDrive ??= new OneDrive.OneDriveOptions();
        var group = routes.MapGroup("/api/fitness");

        // The Development bypass is the one case with no policy: sign-in is
        // skipped entirely, decided by FitnessGate, never by this default. It
        // says so — AllowAnonymous — rather than saying nothing, because every
        // endpoint declares who may call it (RouteTableTests) and the
        // fallback policy closes one that does not.
        var api = requireOwnerSignIn ? group.RequireAuthorization(PolicyName) : group.AllowAnonymous();

        // Whatever a write changed, the cached pages may have read.
        api.EvictOnWrite();

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
                hevyApi = options.HasHevyApi,
                intervalsIcu = intervalsIcu.IsConfigured,
                oneDrive = oneDrive.IsConfigured
            });
        }).AllowAnonymous();

        // The week in one page, for the dashboard.
        api.MapGet("/digest", async (FitnessDbContext database, OneDrive.IWorkbookSource source, IClock clock, CancellationToken cancellationToken) =>
            Results.Ok(await DigestReports.BuildAsync(database, source, clock.GetCurrentInstant(), cancellationToken)))
            .CacheOutput(FitnessOutputCache.PolicyName);

        // The same page as text, for the morning brief: one bearer key, no
        // sign-in. Mapped only when a key of real length is configured, and
        // compared in constant time, because a fitness log is health data.
        if (options.HasDigestKey)
        {
            routes.MapGet("/api/fitness/digest.txt", async (HttpContext context, FitnessDbContext database, OneDrive.IWorkbookSource source, IClock clock, CancellationToken cancellationToken) =>
            {
                if (!DigestKeyAllows(context.Request.Headers.Authorization.ToString(), options.DigestKey))
                {
                    return Results.Unauthorized();
                }

                var digest = await DigestReports.BuildAsync(database, source, clock.GetCurrentInstant(), cancellationToken);
                return Results.Text(digest.Text + "\n", "text/plain; charset=utf-8");
            }).AllowAnonymous(); // The bearer key above is the whole of its authorization.
        }

        api.MapGet("/summary", (FitnessDbContext database, IClock clock, CancellationToken cancellationToken) =>
            FitnessReports.SummaryAsync(database, clock, cancellationToken))
            .CacheOutput(FitnessOutputCache.PolicyName);

        api.MapGet("/citations", () => Results.Ok(Citations.All));

        api.MapSolverEndpoints();
        api.MapPredictionLedger();
        api.MapIngestEndpoints(intervalsIcu, options.HasHevyApi);

        // The goals, read from the athlete's own workbook.
        api.MapGoalEndpoints(oneDrive);

        // The thresholds, one value at a time, each kept.
        api.MapThresholdEndpoints();

        // The owner's saved documents, on their own prefix but behind the
        // same policy and the same Development bypass: the training guide
        // and the course planner are the owner's, and a visitor gets neither
        // a read nor a write.
        var progress = routes.MapGroup("/api/progress");
        (requireOwnerSignIn ? progress.RequireAuthorization(PolicyName) : progress.AllowAnonymous()).MapProgressEndpoints();

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
            double? startHours,
            double? rampPerWeek,
            bool? useHistory,
            CancellationToken cancellationToken) =>
        {
            if (compliance is < 0 or > 1) return Fail("compliance 0-1.");
            if (startHours is < 0 or > 40) return Fail("startHours 0-40.");
            if (rampPerWeek is < 0 or > 0.5) return Fail("rampPerWeek 0-0.5.");

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
                DateTime.UtcNow.Year, cancellationToken,
                startHours, rampPerWeek ?? 0, useHistory ?? true));
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

            var settings = await database.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
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
                    a.AverageHr,
                    a.LoadKg
                })
                .ToListAsync(cancellationToken);

            var total = await database.Activities.CountAsync(cancellationToken);

            return Results.Ok(new { activities = rows, total, limit });
        });

        // No watch records what was in the pack, and without it a ruck is a
        // walk as far as the models know. Typed on the page, per ruck.
        api.MapPut("/activities/{id:guid}/load", async (
            Guid id, ActivityLoadUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            if (update.LoadKg is <= 0 or > 100) return Fail("A load is 0-100 kg, or empty to clear it.");

            var activity = await database.Activities.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (activity is null) return Results.NotFound();
            if (activity.Sport != "ruck") return Fail("Only a ruck carries a load.");

            activity.LoadKg = update.LoadKg;
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        // A fitness test is a measurement the gates outrank the model with;
        // stored raw, scored on the way out against the published tables.
        api.MapPost("/aft", async (AftResultUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var date = ParseDate(update.Date);
            if (date is null) return Fail("A test needs a date.");
            if (update.DeadliftKg is < 0 or > 400 || update.HandReleasePushUps is < 0 or > 200
                || update.SprintDragCarrySeconds is < 0 or > 900 || update.PlankSeconds is < 0 or > 1800
                || update.TwoMileSeconds is < 0 or > 3600)
            {
                return Fail("One of those results is outside anything the tables score.");
            }

            var existing = await database.AftResults.SingleOrDefaultAsync(r => r.Date == date, cancellationToken);
            if (existing is null)
            {
                existing = new AftResult { Id = Guid.NewGuid(), Date = date.Value };
                database.AftResults.Add(existing);
            }

            existing.DeadliftKg = update.DeadliftKg;
            existing.HandReleasePushUps = update.HandReleasePushUps;
            existing.SprintDragCarrySeconds = update.SprintDragCarrySeconds;
            existing.PlankSeconds = update.PlankSeconds;
            existing.TwoMileSeconds = update.TwoMileSeconds;

            await database.SaveChangesAsync(cancellationToken);

            var row = await database.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
                      ?? new AthleteSettings { Id = 1 };
            var today = SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Etc/UTC"]).Date;
            return Results.Ok(ReadinessReports.Score(existing, row, today));
        });

        // The FBI test, entered by hand like the AFT. The only source for FBI
        // push-ups: training push-ups are a different technique.
        api.MapPost("/fbi", async (FbiResultUpdate update, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var date = ParseDate(update.Date);
            if (date is null) return Fail("A test needs a date.");
            if (update is { PullUps: null, Sprint300mSeconds: null, PushUps: null, Run1_5MileSeconds: null })
            {
                return Fail("Enter at least one event.");
            }

            if (update.PullUps is < 0 or > 100 || update.PushUps is < 0 or > 200
                || update.Sprint300mSeconds is < 30 or > 180 || update.Run1_5MileSeconds is < 300 or > 1800)
            {
                return Fail("One of those results is outside anything the tables score.");
            }

            var existing = await database.FbiResults.SingleOrDefaultAsync(r => r.Date == date, cancellationToken);
            if (existing is null)
            {
                existing = new FbiResult { Id = Guid.NewGuid(), Date = date.Value };
                database.FbiResults.Add(existing);
            }

            existing.PullUps = update.PullUps;
            existing.Sprint300mSeconds = update.Sprint300mSeconds;
            existing.PushUps = update.PushUps;
            existing.Run1_5MileSeconds = update.Run1_5MileSeconds;
            await database.SaveChangesAsync(cancellationToken);

            var row = await database.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
            return Results.Ok(ReadinessReports.ScoreFbi(existing, row?.Female ?? false));
        });

        api.MapDelete("/fbi/{id:guid}", async (Guid id, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var existing = await database.FbiResults.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (existing is null) return Results.NotFound();

            database.FbiResults.Remove(existing);
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapDelete("/aft/{id:guid}", async (Guid id, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var existing = await database.AftResults.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (existing is null) return Results.NotFound();

            database.AftResults.Remove(existing);
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
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

        // A partial update: only the fields the request names change. Each
        // card sends its own fields alone. Until 2026-11-13 every card sent
        // the whole record, so a card holding an old copy of a value wrote it
        // back over a newer one. The thresholds are not saved here at all:
        // each is logged on its own through /thresholds.
        api.MapPatch("/settings", async (JsonElement body, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            if (body.ValueKind != JsonValueKind.Object) return Fail("Send a JSON object of the settings to change.");

            var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (row is null)
            {
                row = new AthleteSettings { Id = 1 };
                database.Settings.Add(row);
            }

            // Today's values, with the named fields laid over them.
            var merged = JsonSerializer.SerializeToNode(AsUpdate(row), SettingsJson)!.AsObject();
            foreach (var property in body.EnumerateObject())
            {
                var key = merged.Select(field => field.Key)
                    .FirstOrDefault(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase));
                if (key is null) return Fail($"There is no setting called {property.Name}.");
                merged[key] = JsonNode.Parse(property.Value.GetRawText());
            }

            SettingsUpdate update;
            try
            {
                update = merged.Deserialize<SettingsUpdate>(SettingsJson)!;
            }
            catch (JsonException)
            {
                return Fail("A setting has a value of the wrong type.");
            }

            if (update.HomeAltitudeMeters is < 0 or > 5000)
            {
                return Fail("Home altitude 0-5000 m.");
            }

            if (update.TreadmillGradePercent is < 0 or > TreadmillGrade.MaxPercent)
            {
                return Fail($"Treadmill incline 0-{TreadmillGrade.MaxPercent:0}%.");
            }

            if ((update.TreadmillGradePercent is null) != string.IsNullOrWhiteSpace(update.TreadmillGradeSince))
            {
                return Fail("A treadmill incline needs the date it started, and a date needs an incline.");
            }

            if (!string.IsNullOrWhiteSpace(update.TreadmillGradeSince) && ParseDate(update.TreadmillGradeSince) is null)
            {
                return Fail("The incline's start date is yyyy-mm-dd.");
            }

            if (update.PastPeakWeightKg is < 30 or > 250 || update.GoalWeightKg is < 30 or > 250)
            {
                return Fail("Weights are 30-250 kg.");
            }

            row.PlanMinutesPerWeek = update.PlanMinutesPerWeek;
            row.VdotMeasuredOn = ParseDate(update.VdotMeasuredOn);
            row.BirthYear = update.BirthYear;
            row.Female = update.Female;
            row.AvailableHoursPerWeek = Math.Clamp(update.AvailableHoursPerWeek, 0, 40);
            row.SustainedWeeklyHours = update.SustainedWeeklyHours is { } sustained
                ? Math.Clamp(sustained, 0, 40)
                : null;
            row.PastPeakDistanceMeters = update.PastPeakDistanceMeters;
            row.PastPeakSeconds = update.PastPeakSeconds;
            row.PastPeakYear = update.PastPeakYear;
            row.PastPeakWeightKg = update.PastPeakWeightKg;
            row.GoalWeightKg = update.GoalWeightKg;
            row.HomeAltitudeMeters = update.HomeAltitudeMeters;
            row.TreadmillGradePercent = update.TreadmillGradePercent;
            row.TreadmillGradeSince = ParseDate(update.TreadmillGradeSince);

            // A race is the honest way to state the anchor; raw VDOT stays as
            // the escape hatch. The race happened at home altitude, so its
            // sea-level equivalent is what scores it.
            if (update is { AnchorDistanceMeters: { } anchorDistance, AnchorSeconds: { } anchorSeconds }
                && anchorDistance is >= 400 and <= 100_000 && anchorSeconds > 0)
            {
                row.StartVdot = Domain.Vdot.FromRace(
                    anchorDistance,
                    Domain.Altitude.ToSeaLevel(anchorSeconds, update.HomeAltitudeMeters) / 60.0);
                row.AnchorDistanceMeters = anchorDistance;
                row.AnchorSeconds = anchorSeconds;
            }
            else if (Math.Abs(update.StartVdot - row.StartVdot) > 1e-6)
            {
                // A typed VDOT replaces the race it came from.
                row.StartVdot = update.StartVdot;
                row.AnchorDistanceMeters = null;
                row.AnchorSeconds = null;
            }

            await database.SaveChangesAsync(cancellationToken);

            // The anchor is what the priors are built from, so moving it moves
            // the posterior.
            cache.Invalidate();

            return Results.NoContent();
        });

        // The one setting the Goals tab edits in place: the running hours its
        // forecasts assume. Nothing else is sent, so nothing else can change.
        api.MapPut("/settings/available-hours", async (AvailableHoursUpdate update, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            if (update.Hours is < 0 or > 40 || double.IsNaN(update.Hours)) return Fail("Hours a week are 0-40.");

            var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (row is null)
            {
                row = new AthleteSettings { Id = 1 };
                database.Settings.Add(row);
            }

            row.AvailableHoursPerWeek = update.Hours;
            await database.SaveChangesAsync(cancellationToken);
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
            hevyApi = false,
            intervalsIcu = false
        })).AllowAnonymous();

        return routes;
    }

    /// <summary>
    /// A 400 whose body is the message itself.
    ///
    /// Results.BadRequest(string) serialises the message as a JSON string, and
    /// the page shows the response body verbatim, so the reader was handed the
    /// quotes as well: <c>holiday.jpg: "Nothing importable in that file."</c>
    /// </summary>
    /// <summary>
    /// Whether an Authorization header carries the digest key: bearer scheme,
    /// exact key, compared in constant time so the comparison's timing says
    /// nothing about how much of a guess was right.
    /// </summary>
    internal static bool DigestKeyAllows(string authorizationHeader, string digestKey)
    {
        const string scheme = "Bearer ";
        if (digestKey.Trim().Length < 32) return false;
        if (!authorizationHeader.StartsWith(scheme, StringComparison.Ordinal)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(authorizationHeader[scheme.Length..].Trim()),
            Encoding.UTF8.GetBytes(digestKey.Trim()));
    }

    private static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web);

    /// <summary>The row as an update that changes nothing.</summary>
    private static SettingsUpdate AsUpdate(AthleteSettings row) => new(
        row.ReferenceHr,
        row.LtSecondsPerKm,
        row.PlanMinutesPerWeek,
        row.StartVdot,
        row.VdotMeasuredOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        row.BirthYear,
        row.Female,
        row.AvailableHoursPerWeek,
        row.SustainedWeeklyHours,
        row.PastPeakDistanceMeters,
        row.PastPeakSeconds,
        row.PastPeakYear,
        row.PastPeakWeightKg,
        row.GoalWeightKg,
        row.HomeAltitudeMeters,
        null,
        null,
        row.LtHr,
        row.TreadmillGradePercent,
        row.TreadmillGradeSince?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static IResult Fail(string message) =>
        Results.Text(message, "text/plain", statusCode: StatusCodes.Status400BadRequest);

    private static LocalDate? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = LocalDatePattern.Iso.Parse(value.Trim());
        return parsed.Success ? parsed.Value : null;
    }
}
