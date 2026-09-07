using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// Reads the selection gates out of the log: what the athlete has actually
/// done recently, what the model says where nothing was done, and how that
/// stands against every published standard between here and a slot.
/// </summary>
internal static class ReadinessReports
{
    /// <summary>A test older than this no longer describes the athlete.</summary>
    private const int MeasurementWindowDays = 120;

    /// <summary>A ruck within this fraction of twelve miles counts as the twelve-mile.</summary>
    private const double TwelveMileTolerance = 0.98;

    private static readonly double TwelveMiles = 12 * Vdot.MileMeters;
    private static readonly double ThirtyFivePounds = 35 / BodyMass.PoundsPerKg;

    public static async Task<ReadinessDto> BuildAsync(
        FitnessDbContext database,
        AthleteSettings row,
        BodyMetric? latestWeight,
        IReadOnlyList<MonthlyAerobicPoint> aerobicTrend,
        LocalDate today,
        CancellationToken cancellationToken)
    {
        var zone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
        var since = today.PlusDays(-MeasurementWindowDays);
        var measurements = new Dictionary<string, Measurement>();

        var anchorEvidence = row.VdotMeasuredOn is { } measuredOn
            ? Text($"modelled from your VDOT {row.StartVdot:0.0} anchor of {measuredOn:yyyy-MM-dd}")
            : Text($"modelled from the default VDOT {row.StartVdot:0.0} — no time trial recorded");

        // Runs: the anchor says what a race would be today, at home altitude.
        measurements[SelectionReadiness.Metrics.RunTwoMile] = Modeled(
            SelectionReadiness.Metrics.RunTwoMile, RaceSeconds(2 * Vdot.MileMeters, row), anchorEvidence);
        measurements[SelectionReadiness.Metrics.RunFiveMile] = Modeled(
            SelectionReadiness.Metrics.RunFiveMile, RaceSeconds(5 * Vdot.MileMeters, row), anchorEvidence);

        // The pace held at the reference heart rate is the aerobic-threshold
        // pace the coaches ask about, if the reference is set at that
        // threshold — which is what the setting is for.
        if (aerobicTrend.Count > 0)
        {
            var month = aerobicTrend[^1];
            measurements[SelectionReadiness.Metrics.AerobicThresholdPace] = new Measurement(
                SelectionReadiness.Metrics.AerobicThresholdPace,
                month.MedianNormalizedSecPerKm * Vdot.MileMeters / 1000,
                Basis.Measured,
                Text($"median pace at {row.ReferenceHr} bpm over {month.RunCount} runs in {month.Year:0000}-{month.Month:00}"),
                new LocalDate(month.Year, month.Month, 1));
        }

        // Rucks: a timed twelve-mile at the load beats any estimate; without
        // one the load-carriage model reads the run engine through the pack.
        var rucks = await RucksAsync(database, zone, cancellationToken);
        var ruck = RuckReport(rucks, row, latestWeight, since, measurements);

        // Calisthenics: the best recent set of each movement the gates count.
        var calisthenics = await CalisthenicsReportAsync(database, zone, since, measurements, cancellationToken);

        // Strength: a triple, read back from the Epley estimate the trend already keeps.
        await StrengthAsync(database, zone, latestWeight, since, measurements, cancellationToken);

        // Grip, as a loaded carry; and the bodyweight the athlete's own
        // standards are set against.
        await CarriesAsync(database, zone, latestWeight, since, measurements, cancellationToken);
        if (latestWeight is { } weighed)
        {
            measurements[SelectionReadiness.Metrics.BodyweightLb] = new Measurement(
                SelectionReadiness.Metrics.BodyweightLb, weighed.WeightKg * BodyMass.PoundsPerKg, Basis.Measured,
                Text($"weigh-in of {weighed.Date:yyyy-MM-dd}"), weighed.Date);
        }

        // The fitness test, scored on the published tables.
        var aftResults = await AftResultsAsync(database, row, today, cancellationToken);
        AftMeasurements(aftResults, since, latestWeight, measurements);

        // Body composition, and what the selection cohort made of it.
        var body = await BodyReportAsync(database, since, measurements, cancellationToken);

        var gates = SelectionReadiness.Evaluate(measurements, row.SelectionDate)
            .Select(g => new GateDto(
                g.Gate.Id,
                g.Gate.Name,
                g.Gate.Purpose,
                g.Gate.WeeksBeforeSelection,
                g.DueOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Status.ToString(),
                g.Passed,
                g.Known,
                g.Requirements.Select(r => new RequirementResultDto(
                    r.Requirement.Metric,
                    r.Requirement.Label,
                    r.Requirement.Comparison.ToString(),
                    r.Requirement.Target,
                    r.Requirement.Unit,
                    r.Requirement.CitationId,
                    r.Status.ToString(),
                    r.Current is { } current ? Dto(current) : null,
                    r.Gap)).ToArray(),
                g.Gate.Untracked))
            .ToArray();

        return new ReadinessDto(
            row.SelectionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            gates,
            ruck,
            calisthenics,
            body,
            aftResults);
    }

    private static double RaceSeconds(double distanceMeters, AthleteSettings row) =>
        Altitude.AtAltitude(Vdot.MinutesFor(distanceMeters, row.StartVdot) * 60, row.HomeAltitudeMeters);

    private static async Task<IReadOnlyList<(LocalDate Date, Activity Activity)>> RucksAsync(
        FitnessDbContext database, DateTimeZone zone, CancellationToken cancellationToken)
    {
        var rucks = await database.Activities
            .Where(a => a.Sport == "ruck" && a.DistanceMeters != null && a.DistanceMeters > 0 && a.DurationSeconds > 0)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);

        return rucks.Select(a => (a.StartedAt.InZone(zone).Date, a)).ToArray();
    }

    private static RuckReportDto RuckReport(
        IReadOnlyList<(LocalDate Date, Activity Activity)> rucks,
        AthleteSettings row,
        BodyMetric? weight,
        LocalDate since,
        Dictionary<string, Measurement> measurements)
    {
        var bodyKg = weight?.WeightKg;

        // The trend needs a body mass, a load and a heart rate; a ruck
        // without any one of them is volume, not a fitness reading.
        var trend = bodyKg is { } mass
            ? rucks
                .Where(r => r.Activity.LoadKg is > 0 && r.Activity.AverageHr is > 0
                            && r.Activity.DurationSeconds >= AerobicAnalysis.MinimumSeconds)
                .Select(r => (r.Date, Pace: LoadCarriage.NormalizedSecPerKm(
                    new LoadedMarch(mass, r.Activity.LoadKg!.Value, r.Activity.DistanceMeters!.Value, r.Activity.DurationSeconds),
                    r.Activity.AverageHr!.Value,
                    row.ReferenceHr)))
                .GroupBy(r => (r.Date.Year, r.Date.Month))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var paces = g.Select(x => x.Pace).OrderBy(x => x).ToArray();
                    var median = paces.Length % 2 == 1
                        ? paces[paces.Length / 2]
                        : (paces[paces.Length / 2 - 1] + paces[paces.Length / 2]) / 2;
                    return new RuckPointDto($"{g.Key.Year:0000}-{g.Key.Month:00}", Math.Round(median), paces.Length);
                })
                .ToArray()
            : [];

        var marches = rucks
            .Where(r => r.Activity.LoadKg is > 0)
            .Select(r => new RuckMarchDto(
                r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.Activity.DistanceMeters!.Value,
                r.Activity.DurationSeconds,
                r.Activity.LoadKg!.Value,
                r.Activity.AverageHr,
                bodyKg is { } m
                    ? LoadCarriage.ImpliedVdot(new LoadedMarch(m, r.Activity.LoadKg!.Value, r.Activity.DistanceMeters!.Value, r.Activity.DurationSeconds))
                    : null))
            .OrderByDescending(m => m.Date)
            .Take(20)
            .ToArray();

        // A timed twelve-mile at or above the load is the measurement.
        foreach (var (metric, loadKg) in new[]
                 {
                     (SelectionReadiness.Metrics.RuckTwelveMileAt45, LoadCarriage.ReferenceLoadKg),
                     (SelectionReadiness.Metrics.RuckTwelveMileAt35, ThirtyFivePounds)
                 })
        {
            var timed = rucks
                .Where(r => r.Date >= since
                            && r.Activity.LoadKg is { } load && load >= loadKg - 0.01
                            && r.Activity.DistanceMeters >= TwelveMiles * TwelveMileTolerance)
                .OrderBy(r => r.Activity.DurationSeconds * TwelveMiles / r.Activity.DistanceMeters!.Value)
                .FirstOrDefault();

            if (timed.Activity is { } best)
            {
                var scaled = best.DurationSeconds * TwelveMiles / best.DistanceMeters!.Value;
                measurements[metric] = new Measurement(
                    metric, scaled, Basis.Measured,
                    Text($"{Format.Distance(best.DistanceMeters.Value)} at {best.LoadKg!.Value * BodyMass.PoundsPerKg:0} lb on {timed.Date:yyyy-MM-dd}"),
                    timed.Date);
            }
            else if (bodyKg is { } body)
            {
                measurements[metric] = Modeled(
                    metric,
                    LoadCarriage.PredictSeconds(row.StartVdot, body, loadKg, TwelveMiles),
                    Text($"load-carriage model from your VDOT {row.StartVdot:0.0} at {body * BodyMass.PoundsPerKg:0} lb bodyweight; no timed twelve-mile at this load in the last {MeasurementWindowDays} days"));
            }
        }

        double? at45 = bodyKg is { } b45 ? LoadCarriage.PredictSeconds(row.StartVdot, b45, LoadCarriage.ReferenceLoadKg, TwelveMiles) : null;
        double? at35 = bodyKg is { } b35 ? LoadCarriage.PredictSeconds(row.StartVdot, b35, ThirtyFivePounds, TwelveMiles) : null;
        var steps = bodyKg is { } bs
            ? LoadCarriage.Explain(row.StartVdot, bs, LoadCarriage.ReferenceLoadKg, TwelveMiles)
                .Select(s => new StepDto(s.Label, s.Expression, s.Value, s.CitationId)).ToArray()
            : [];

        return new RuckReportDto(
            LoadCarriage.ReferenceLoadKg,
            LoadCarriage.RuckEfficiency,
            trend,
            marches,
            at45,
            at35,
            rucks.Count(r => r.Activity.LoadKg is null),
            steps);
    }

    private static async Task<CalisthenicsDto> CalisthenicsReportAsync(
        FitnessDbContext database,
        DateTimeZone zone,
        LocalDate since,
        Dictionary<string, Measurement> measurements,
        CancellationToken cancellationToken)
    {
        var sets = await database.StrengthSets
            .Join(database.Activities, s => s.ActivityId, a => a.Id, (s, a) => new { s, a.StartedAt })
            .ToListAsync(cancellationToken);

        var best = Calisthenics.BestPerDay(sets.Select(x => new BodyweightSet(
            x.StartedAt.InZone(zone).Date, x.s.Exercise, x.s.Reps, x.s.DurationSeconds, x.s.WeightKg)));

        var latest = new List<BestSetDto>();
        foreach (var metric in new[]
                 {
                     Calisthenics.PullUps, Calisthenics.HandReleasePushUps, Calisthenics.PushUps,
                     Calisthenics.SitUps, Calisthenics.Plank
                 })
        {
            if (Calisthenics.Best(best, metric, since) is not { } point) continue;

            latest.Add(new BestSetDto(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), metric, point.Value));
            measurements[metric] = new Measurement(
                metric, point.Value, Basis.Measured,
                Text($"best set in the strength log, {point.Date:yyyy-MM-dd}"), point.Date);
        }

        return new CalisthenicsDto(
            latest,
            best.Select(p => new BestSetDto(p.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Metric, p.Value)).ToArray());
    }

    /// <summary>The longest farmer's carry at the athlete's own standard load, if one is logged.</summary>
    private static async Task CarriesAsync(
        FitnessDbContext database,
        DateTimeZone zone,
        BodyMetric? weight,
        LocalDate since,
        Dictionary<string, Measurement> measurements,
        CancellationToken cancellationToken)
    {
        if (weight is not { } w) return;

        var sets = await database.StrengthSets
            .Join(database.Activities, s => s.ActivityId, a => a.Id, (s, a) => new { s, a.StartedAt })
            .Where(x => x.s.WeightKg > 0 && x.s.DistanceMeters != null)
            .ToListAsync(cancellationToken);

        var best = Carries.Longest(
            sets.Select(x => new LoggedCarry(x.StartedAt.InZone(zone).Date, x.s.Exercise, x.s.WeightKg, x.s.DistanceMeters)),
            w.WeightKg,
            Carries.StandardBodyweightMultiple,
            since);

        if (best is null) return;

        measurements[SelectionReadiness.Metrics.FarmersCarryMeters] = new Measurement(
            SelectionReadiness.Metrics.FarmersCarryMeters, best.DistanceMeters, Basis.Measured,
            Text($"{best.TotalLoadKg * BodyMass.PoundsPerKg:0} lb total ({best.TotalLoadKg / 2 * BodyMass.PoundsPerKg:0} lb a hand, as logged) on {best.Date:yyyy-MM-dd}, against {w.WeightKg * Carries.StandardBodyweightMultiple * BodyMass.PoundsPerKg:0} lb needed at {w.WeightKg * BodyMass.PoundsPerKg:0} lb bodyweight"),
            best.Date);
    }

    private static async Task StrengthAsync(
        FitnessDbContext database,
        DateTimeZone zone,
        BodyMetric? weight,
        LocalDate since,
        Dictionary<string, Measurement> measurements,
        CancellationToken cancellationToken)
    {
        var sets = await database.StrengthSets
            .Join(database.Activities, s => s.ActivityId, a => a.Id, (s, a) => new { s, a.StartedAt })
            .Where(x => x.s.WeightKg > 0 && x.s.Reps >= 1 && x.s.Reps <= OneRepMax.MaxTrustworthyReps)
            .ToListAsync(cancellationToken);

        foreach (var (lift, reps, bodyweightMetric, poundsMetric) in new[]
                 {
                     ("deadlift", 3, SelectionReadiness.Metrics.DeadliftTripleToBodyweight, SelectionReadiness.Metrics.DeadliftTripleLb),
                     ("front squat", 3, SelectionReadiness.Metrics.FrontSquatTripleToBodyweight, (string?)null),
                     ("front squat", 1, SelectionReadiness.Metrics.FrontSquatToBodyweight, (string?)null),
                     ("bench press", 1, SelectionReadiness.Metrics.BenchPressToBodyweight, (string?)null),
                     ("back squat", 5, (string?)null, SelectionReadiness.Metrics.BackSquatFiveLb)
                 })
        {
            var recent = sets
                .Where(x => IsLift(x.s.Exercise, lift) && x.StartedAt.InZone(zone).Date >= since)
                .Select(x => (Date: x.StartedAt.InZone(zone).Date, E1Rm: OneRepMax.Epley(x.s.WeightKg, x.s.Reps)))
                .OrderByDescending(x => x.E1Rm)
                .FirstOrDefault();

            if (recent.E1Rm <= 0) continue;

            // Epley backwards: the n-rep max is the single divided by (1 + n/30);
            // the single is the estimate itself.
            var repMaxKg = reps == 1 ? recent.E1Rm : recent.E1Rm / (1 + reps / 30.0);
            var evidence = Text($"{repMaxKg * BodyMass.PoundsPerKg:0} lb for {reps}, from an Epley estimate of {recent.E1Rm * BodyMass.PoundsPerKg:0} lb on {recent.Date:yyyy-MM-dd}");

            if (poundsMetric is not null)
            {
                measurements[poundsMetric] = new Measurement(
                    poundsMetric, repMaxKg * BodyMass.PoundsPerKg, Basis.Modeled, evidence, recent.Date);
            }

            if (bodyweightMetric is not null && weight is { } w)
            {
                measurements[bodyweightMetric] = new Measurement(
                    bodyweightMetric, repMaxKg / w.WeightKg, Basis.Modeled,
                    Text($"{evidence}, over {w.WeightKg * BodyMass.PoundsPerKg:0} lb bodyweight"), recent.Date);
            }
        }
    }

    /// <summary>Whether a logged exercise is the lift a gate names, and not a variant that is not.</summary>
    internal static bool IsLift(string exercise, string lift)
    {
        var name = exercise.Trim().ToLowerInvariant();

        if (lift == "bench press")
        {
            // The flat bench; an incline or decline is a different lift.
            return name.Contains("bench press") && !(name.Contains("incline") || name.Contains("decline"));
        }

        if (lift == "back squat")
        {
            // "Squat (Barbell)" is the back squat; every named variant is not.
            if (!name.Contains("squat")) return false;
            return !(name.Contains("front") || name.Contains("split") || name.Contains("bulgarian")
                     || name.Contains("goblet") || name.Contains("hack") || name.Contains("pistol")
                     || name.Contains("jump") || name.Contains("overhead") || name.Contains("air")
                     || name.Contains("wall") || name.Contains("sissy") || name.Contains("zercher")
                     || name.Contains("belt") || name.Contains("smith") || name.Contains("machine"));
        }

        if (!name.Contains(lift)) return false;

        // A Romanian or stiff-leg deadlift is an accessory, not the pull the
        // standard is written for.
        return lift != "deadlift"
               || !(name.Contains("romanian") || name.Contains("stiff") || name.Contains("single leg") || name.Contains("single-leg"));
    }

    private static async Task<IReadOnlyList<AftResultDto>> AftResultsAsync(
        FitnessDbContext database, AthleteSettings row, LocalDate today, CancellationToken cancellationToken)
    {
        var results = await database.AftResults
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return results.Select(r => Score(r, row, today)).ToArray();
    }

    /// <summary>One test scored on the athlete's band and scale, as of today.</summary>
    internal static AftResultDto Score(AftResult result, AthleteSettings row, LocalDate today)
    {
        // Without a birth year the youngest band applies: the hardest scale,
        // so an unstated age never flatters the score.
        var age = row.BirthYear is { } birthYear ? Math.Max(0, today.Year - birthYear) : 0;
        var female = row.Female ?? false;

        var score = Aft.Score(
            new AftPerformance(result.DeadliftKg, result.HandReleasePushUps, result.SprintDragCarrySeconds, result.PlankSeconds, result.TwoMileSeconds),
            age, female);

        return new AftResultDto(
            result.Id.ToString(),
            result.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.DeadliftKg,
            result.HandReleasePushUps,
            result.SprintDragCarrySeconds,
            result.PlankSeconds,
            result.TwoMileSeconds,
            score.Total,
            score.LowestEventPoints,
            score.MeetsCombatStandard,
            Aft.AgeBands[score.AgeBand],
            row.BirthYear is null,
            score.Events.Select(e => new AftEventDto(e.Event.ToString(), Aft.Name(e.Event), e.Raw, e.Points)).ToArray(),
            Aft.Explain(score).Select(s => new StepDto(s.Label, s.Expression, s.Value, s.CitationId)).ToArray());
    }

    /// <summary>
    /// What the most recent test says. The latest official test always counts —
    /// it is the athlete's record until the next one — but one older than the
    /// measurement window says so in its evidence, because a gate scored from a
    /// months-old card is a different thing from one scored from last week's.
    /// </summary>
    internal static void AftMeasurements(
        IReadOnlyList<AftResultDto> results,
        LocalDate since,
        BodyMetric? weight,
        Dictionary<string, Measurement> measurements)
    {
        if (results.Count == 0) return;

        var latest = results[0];
        var on = LocalDatePattern(latest.Date);

        var evidence = on < since
            ? Text($"AFT of {latest.Date} ({Period.Between(on, since.PlusDays(MeasurementWindowDays), PeriodUnits.Days).Days} days old — retest to refresh)")
            : Text($"AFT of {latest.Date}");

        measurements[SelectionReadiness.Metrics.AftTotal] = new Measurement(SelectionReadiness.Metrics.AftTotal, latest.Total, Basis.Measured, evidence, on);
        measurements[SelectionReadiness.Metrics.AftLowestEvent] = new Measurement(SelectionReadiness.Metrics.AftLowestEvent, latest.LowestEvent, Basis.Measured, evidence, on);

        // A test's events are measurements too, and outrank the model. They
        // outrank a strength-log set only when they are better.
        Prefer(measurements, SelectionReadiness.Metrics.RunTwoMile, latest.TwoMileSeconds, lowerIsBetter: true, evidence, on);
        Prefer(measurements, SelectionReadiness.Metrics.HandReleasePushUps, latest.HandReleasePushUps, lowerIsBetter: false, evidence, on);
        Prefer(measurements, SelectionReadiness.Metrics.Plank, latest.PlankSeconds, lowerIsBetter: false, evidence, on);
        Prefer(measurements, SelectionReadiness.Metrics.DeadliftTripleLb, latest.DeadliftKg * BodyMass.PoundsPerKg, lowerIsBetter: false, evidence, on);

        // The test's deadlift is a real triple, which the log's Epley estimate
        // only approximates; over the current bodyweight it scores the entry
        // test's ratio too.
        if (weight is { } w)
        {
            Prefer(measurements, SelectionReadiness.Metrics.DeadliftTripleToBodyweight, latest.DeadliftKg / w.WeightKg, lowerIsBetter: false,
                Text($"{evidence}, over {w.WeightKg * BodyMass.PoundsPerKg:0} lb bodyweight"), on);
        }
    }

    private static void Prefer(
        Dictionary<string, Measurement> measurements, string metric, double value, bool lowerIsBetter, string evidence, LocalDate on)
    {
        if (measurements.TryGetValue(metric, out var current) && current.Basis == Basis.Measured)
        {
            var better = lowerIsBetter ? value < current.Value : value > current.Value;
            if (!better) return;
        }

        measurements[metric] = new Measurement(metric, value, Basis.Measured, evidence, on);
    }

    private static async Task<BodyReportDto> BodyReportAsync(
        FitnessDbContext database, LocalDate since, Dictionary<string, Measurement> measurements, CancellationToken cancellationToken)
    {
        var metrics = await database.BodyMetrics.OrderBy(m => m.Date).ToListAsync(cancellationToken);

        var points = metrics
            .Select(m => new BodyPointDto(
                m.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                m.WeightKg,
                m.BodyFatPercent,
                m.BodyFatPercent is { } fat ? BodyComposition.LeanMassKg(m.WeightKg, fat) : null))
            .ToArray();

        var latestFat = metrics.LastOrDefault(m => m.BodyFatPercent is not null);
        double? bodyFat = latestFat?.BodyFatPercent;
        double? leanMass = latestFat is { BodyFatPercent: { } f } ? BodyComposition.LeanMassKg(latestFat.WeightKg, f) : null;

        if (latestFat is not null && latestFat.Date >= since && bodyFat is { } fatNow)
        {
            measurements[SelectionReadiness.Metrics.BodyFat] = new Measurement(
                SelectionReadiness.Metrics.BodyFat, fatNow, Basis.Measured,
                Text($"weigh-in of {latestFat.Date:yyyy-MM-dd}"), latestFat.Date);
        }

        return new BodyReportDto(
            points,
            bodyFat,
            leanMass,
            bodyFat is { } bf ? BodyComposition.SelectionRateByBodyFat(bf) : null,
            leanMass is { } lm ? BodyComposition.SelectionRateByLeanMass(lm) : null);
    }

    private static Measurement Modeled(string metric, double value, string evidence) =>
        new(metric, value, Basis.Modeled, evidence);

    private static StandingDto Dto(Measurement m) =>
        new(m.Metric, m.Value, m.Basis.ToString(), m.Evidence, m.On?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static LocalDate LocalDatePattern(string isoDate)
    {
        var parts = isoDate.Split('-');
        return new LocalDate(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
