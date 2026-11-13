using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>What the log says today, and what it said on every earlier date, by metric.</summary>
internal sealed record Readings(
    Dictionary<string, Measurement> Measurements,
    Dictionary<string, List<DatedValue>> Histories,
    RuckReportDto Ruck,
    CalisthenicsDto Calisthenics,
    BodyReportDto Body,
    IReadOnlyList<AftResultDto> AftResults);

internal static class ReadinessReports
{
    /// <summary>A test older than this no longer describes the athlete.</summary>
    private const int MeasurementWindowDays = 120;

    /// <summary>A ruck within this fraction of twelve miles counts as the twelve-mile.</summary>
    private const double TwelveMileTolerance = 0.98;

    private static readonly double TwelveMiles = 12 * Vdot.MileMeters;
    private static readonly double ThirtyFivePounds = 35 / BodyMass.PoundsPerKg;

    public static ReadinessDto Build(
        TrainingHistory history,
        ReadinessEvidence evidence,
        IReadOnlyList<MonthlyAerobicPoint> aerobicTrend,
        LocalDate today)
    {
        var readings = Gather(history, evidence, aerobicTrend, today);

        return new ReadinessDto(
            readings.Ruck,
            readings.Calisthenics,
            readings.Body,
            readings.AftResults);
    }

    /// <summary>Every measurement the goals are scored from, and every dated reading behind them.</summary>
    internal static Readings Gather(
        TrainingHistory history,
        ReadinessEvidence evidence,
        IReadOnlyList<MonthlyAerobicPoint> aerobicTrend,
        LocalDate today)
    {
        var row = history.Row;
        var latestWeight = evidence.LatestWeight;
        var zone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
        var since = today.PlusDays(-MeasurementWindowDays);
        var measurements = new Dictionary<string, Measurement>();
        var histories = new Dictionary<string, List<DatedValue>>();

        var anchorSource = row.AnchorSource ?? (row.VdotMeasuredOn is null ? "the default VDOT" : "the VDOT in your profile");
        var anchorEvidence = row.VdotMeasuredOn is not null
            ? Text($"modeled from your VDOT {row.StartVdot:0.0}, from {anchorSource}")
            : Text($"modeled from the default VDOT {row.StartVdot:0.0}, with no race, time trial, or AFT recorded");

        // Runs: the anchor says what a race would be today, at home altitude.
        measurements[SelectionReadiness.Metrics.RunTwoMile] = Modeled(
            SelectionReadiness.Metrics.RunTwoMile, RaceSeconds(2 * Vdot.MileMeters, row), anchorEvidence, row.VdotMeasuredOn,
            anchorSource, Models.Vdot);
        measurements[SelectionReadiness.Metrics.RunFiveMile] = Modeled(
            SelectionReadiness.Metrics.RunFiveMile, RaceSeconds(5 * Vdot.MileMeters, row), anchorEvidence, row.VdotMeasuredOn,
            anchorSource, Models.Vdot);
        measurements[SelectionReadiness.Metrics.RunTenMile] = Modeled(
            SelectionReadiness.Metrics.RunTenMile, RaceSeconds(10 * Vdot.MileMeters, row), anchorEvidence, row.VdotMeasuredOn,
            anchorSource, Models.Vdot);
        measurements[SelectionReadiness.Metrics.RunOneAndHalfMile] = Modeled(
            SelectionReadiness.Metrics.RunOneAndHalfMile, RaceSeconds(1.5 * Vdot.MileMeters, row), anchorEvidence, row.VdotMeasuredOn,
            anchorSource, Models.Vdot);

        // A fast stretch inside a training run counts toward the run goals
        // (Neb, 2026-11-04), but only when it beats what the goal already
        // reads: the fastest 2 miles of an easy run say nothing about a race.
        foreach (var (metric, best, miles) in new (string, Func<Activity, double?>, string)[]
                 {
                     (SelectionReadiness.Metrics.RunOneAndHalfMile, a => a.Best1_5MileSeconds, "1.5 mi"),
                     (SelectionReadiness.Metrics.RunTwoMile, a => a.Best2MileSeconds, "2 mi"),
                     (SelectionReadiness.Metrics.RunFiveMile, a => a.Best5MileSeconds, "5 mi"),
                     (SelectionReadiness.Metrics.RunTenMile, a => a.Best10MileSeconds, "10 mi")
                 })
        {
            var fastest = history.Activities
                .Where(a => a.Sport == "run" && best(a) is > 0)
                .Select(a => (Activity: a, Seconds: best(a)!.Value, Date: a.StartedAt.InZone(zone).Date))
                .Where(x => x.Date >= since)
                .OrderBy(x => x.Seconds)
                .FirstOrDefault();
            if (fastest.Activity is null) continue;
            if (measurements.TryGetValue(metric, out var held) && held.Value <= fastest.Seconds) continue;

            measurements[metric] = new Measurement(metric, fastest.Seconds, Basis.Measured,
                Text($"fastest {miles} inside a training run, {fastest.Date:yyyy-MM-dd}{(fastest.Activity.Indoor == true ? ", treadmill" : "")}"),
                fastest.Date,
                SourceName(fastest.Activity.Source));
        }

        // Aerobic-threshold pace. Measured only by a heart-rate drift test he
        // named as one, held inside the 5% line. Without a test, the pace at
        // his AeT heart rate across the month's easy runs is an estimate of
        // it, and is shown as modeled.
        var aetTest = FitnessReports.FieldTestsIn(history.Activities, row, today).Tests
            .LastOrDefault(t => t.Kind == FieldTests.AetKind
                                && t.Date >= today.PlusDays(-FieldTests.SuggestionWindowDays)
                                && t.DriftPercent is <= FieldTests.DriftLimit);
        if (aetTest is not null)
        {
            measurements[SelectionReadiness.Metrics.AerobicThresholdPace] = new Measurement(
                SelectionReadiness.Metrics.AerobicThresholdPace,
                aetTest.SecPerKm * Vdot.MileMeters / 1000,
                Basis.Measured,
                Text($"heart-rate drift test of {aetTest.Date:yyyy-MM-dd}: {aetTest.AverageHr} bpm, drift {aetTest.DriftPercent:0.0%}{(aetTest.Indoor ? ", treadmill" : "")}"),
                aetTest.Date,
                "a heart-rate drift test you named as one");
        }
        else if (aerobicTrend.Count > 0)
        {
            var month = aerobicTrend[^1];
            measurements[SelectionReadiness.Metrics.AerobicThresholdPace] = new Measurement(
                SelectionReadiness.Metrics.AerobicThresholdPace,
                month.MedianNormalizedSecPerKm * Vdot.MileMeters / 1000,
                Basis.Modeled,
                Text($"no drift test yet; median pace of {month.RunCount} easy runs in {month.Year:0000}-{month.Month:00}, scaled to {row.ReferenceHr} bpm"),
                // The last run counted, so the Goals tab ages it from that
                // run rather than from the first of the month.
                month.LastRun ?? new LocalDate(month.Year, month.Month, 1),
                "easy runs from Garmin",
                Models.HeartRatePace);
        }

        // Rucks: a timed twelve-mile at the load beats any estimate; without
        // one the load-carriage model reads the run engine through the pack.
        var rucks = Rucks(history.Activities, zone);
        var ruck = RuckReport(rucks, row, latestWeight, since, measurements);

        // Calisthenics: the best recent set of each movement the gates count.
        var calisthenics = CalisthenicsReport(evidence.Sets, zone, since, measurements, histories);

        // Strength: a triple, read back from the Epley estimate the trend already keeps.
        Strength(evidence.Sets, zone, latestWeight, since, measurements, histories);

        // Grip, as a loaded carry; and the bodyweight the athlete's own
        // standards are set against.
        CarriesIn(evidence.Sets, zone, latestWeight, since, measurements);
        if (latestWeight is { } weighed)
        {
            measurements[SelectionReadiness.Metrics.BodyweightLb] = new Measurement(
                SelectionReadiness.Metrics.BodyweightLb, weighed.WeightKg * BodyMass.PoundsPerKg, Basis.Measured,
                Text($"weigh-in of {weighed.Date:yyyy-MM-dd}"), weighed.Date, Sources.WeighIn);
        }

        // The fitness test, scored on the published tables.
        var aftResults = evidence.AftResults.Select(r => Score(r, row, today)).ToArray();
        AftMeasurements(aftResults, since, latestWeight, measurements, histories);
        FbiMeasurements(evidence.FbiResults ?? [], measurements, histories);

        // Body composition, and what the selection cohort made of it.
        var body = BodyReport(evidence.BodyMetrics, since, measurements, histories);

        return new Readings(measurements, histories, ruck, calisthenics, body, aftResults);
    }

    private static void Record(Dictionary<string, List<DatedValue>> histories, string metric, LocalDate date, double value)
    {
        if (!histories.TryGetValue(metric, out var list))
        {
            list = [];
            histories[metric] = list;
        }

        list.Add(new DatedValue(date, value));
    }

    private static double RaceSeconds(double distanceMeters, AthleteSettings row) =>
        Altitude.AtAltitude(Vdot.MinutesFor(distanceMeters, row.StartVdot) * 60, row.HomeAltitudeMeters);

    /// <summary>The rucks in the log, which is held oldest first.</summary>
    private static IReadOnlyList<(LocalDate Date, Activity Activity)> Rucks(
        IReadOnlyList<Activity> activities, DateTimeZone zone) =>
        activities
            .Where(a => a.Sport == "ruck" && a.DistanceMeters != null && a.DistanceMeters > 0 && a.DurationSeconds > 0)
            .Select(a => (a.StartedAt.InZone(zone).Date, a))
            .ToArray();

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
                    timed.Date,
                    SourceName(best.Source));
            }
            else if (bodyKg is { } body)
            {
                measurements[metric] = Modeled(
                    metric,
                    LoadCarriage.PredictSeconds(row.StartVdot, body, loadKg, TwelveMiles),
                    Text($"load-carriage model from your VDOT {row.StartVdot:0.0} at {body * BodyMass.PoundsPerKg:0} lb bodyweight; no timed twelve-mile at this load in the last {MeasurementWindowDays} days"),
                    row.VdotMeasuredOn,
                    Text($"{row.AnchorSource ?? (row.VdotMeasuredOn is null ? "the default VDOT" : "the VDOT in your profile")} and your latest weigh-in"),
                    Models.LoadCarriage);
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

    private static CalisthenicsDto CalisthenicsReport(
        IReadOnlyList<DatedSet> sets,
        DateTimeZone zone,
        LocalDate since,
        Dictionary<string, Measurement> measurements,
        Dictionary<string, List<DatedValue>> histories)
    {
        var best = Calisthenics.BestPerDay(sets.Select(x => new BodyweightSet(
            x.StartedAt.InZone(zone).Date, x.Set.Exercise, x.Set.Reps, x.Set.DurationSeconds, x.Set.WeightKg)));

        var latest = new List<BestSetDto>();
        foreach (var metric in new[]
                 {
                     Calisthenics.PullUps, Calisthenics.HandReleasePushUps, Calisthenics.PushUps,
                     Calisthenics.SitUps, Calisthenics.Plank
                 })
        {
            foreach (var day in best.Where(p => p.Metric == metric)) Record(histories, metric, day.Date, day.Value);
            if (Calisthenics.Best(best, metric, since) is not { } point) continue;

            latest.Add(new BestSetDto(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), metric, point.Value));

            // Neb, 2026-11-04. A pull-up done to standard in training is a
            // pull-up, so a training PR counts. Push-ups are not: he trains
            // them to a metronome and finishes on his knees, and the FBI
            // push-up and the AFT hand-release push-up are each their own
            // technique. Only a recorded test sets either. A plank held in
            // training is a floor for the test.
            if (metric == Calisthenics.PullUps)
            {
                measurements[metric] = new Measurement(
                    metric, point.Value, Basis.Measured,
                    Text($"best set to standard in training, {point.Date:yyyy-MM-dd}"), point.Date, Sources.Hevy);
            }
            else if (metric is not (Calisthenics.PushUps or Calisthenics.HandReleasePushUps))
            {
                measurements[metric] = new Measurement(
                    metric, point.Value, Basis.Training,
                    Text($"best hold in training, {point.Date:yyyy-MM-dd}: a floor for the test, not a test result"), point.Date, Sources.Hevy);
            }
        }

        return new CalisthenicsDto(
            latest,
            best.Select(p => new BestSetDto(p.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Metric, p.Value)).ToArray());
    }

    /// <summary>The longest farmer's carry at the athlete's own standard load, if one is logged.</summary>
    private static void CarriesIn(
        IReadOnlyList<DatedSet> sets,
        DateTimeZone zone,
        BodyMetric? weight,
        LocalDate since,
        Dictionary<string, Measurement> measurements)
    {
        if (weight is not { } w) return;

        var best = Carries.Longest(
            sets.Where(x => x.Set.WeightKg > 0 && x.Set.DistanceMeters != null)
                .Select(x => new LoggedCarry(x.StartedAt.InZone(zone).Date, x.Set.Exercise, x.Set.WeightKg, x.Set.DistanceMeters)),
            w.WeightKg,
            Carries.StandardBodyweightMultiple,
            since);

        if (best is null) return;

        measurements[SelectionReadiness.Metrics.FarmersCarryMeters] = new Measurement(
            SelectionReadiness.Metrics.FarmersCarryMeters, best.DistanceMeters, Basis.Measured,
            Text($"{best.TotalLoadKg * BodyMass.PoundsPerKg:0} lb total ({best.TotalLoadKg / 2 * BodyMass.PoundsPerKg:0} lb a hand, as logged) on {best.Date:yyyy-MM-dd}, against {w.WeightKg * Carries.StandardBodyweightMultiple * BodyMass.PoundsPerKg:0} lb needed at {w.WeightKg * BodyMass.PoundsPerKg:0} lb bodyweight"),
            best.Date,
            Sources.Hevy);
    }

    private static void Strength(
        IReadOnlyList<DatedSet> all,
        DateTimeZone zone,
        BodyMetric? weight,
        LocalDate since,
        Dictionary<string, Measurement> measurements,
        Dictionary<string, List<DatedValue>> histories)
    {
        var sets = all.Where(x => OneRepMax.IsEstimable(x.Set.WeightKg, x.Set.Reps)).ToArray();

        foreach (var (lift, reps, bodyweightMetric, poundsMetric) in new[]
                 {
                     ("deadlift", 3, SelectionReadiness.Metrics.DeadliftTripleToBodyweight, SelectionReadiness.Metrics.DeadliftTripleLb),
                     ("front squat", 3, SelectionReadiness.Metrics.FrontSquatTripleToBodyweight, (string?)null),
                     ("front squat", 1, SelectionReadiness.Metrics.FrontSquatToBodyweight, SelectionReadiness.Metrics.FrontSquatLb),
                     ("bench press", 1, SelectionReadiness.Metrics.BenchPressToBodyweight, SelectionReadiness.Metrics.BenchPressLb),
                     ("back squat", 5, (string?)null, SelectionReadiness.Metrics.BackSquatFiveLb),
                     ("back squat", 1, (string?)null, SelectionReadiness.Metrics.BackSquatLb)
                 })
        {
            // Every day the lift was trained, its best estimate: the history a
            // forecast is drawn through.
            var byDay = sets
                .Where(x => IsLift(x.Set.Exercise, lift))
                .GroupBy(x => x.StartedAt.InZone(zone).Date)
                .Select(g => (Date: g.Key, E1Rm: g.Max(x => OneRepMax.Epley(x.Set.WeightKg, x.Set.Reps))))
                .OrderBy(x => x.Date)
                .ToArray();

            foreach (var day in byDay)
            {
                var dayRepMax = reps == 1 ? day.E1Rm : day.E1Rm / (1 + reps / 30.0);
                if (poundsMetric is not null) Record(histories, poundsMetric, day.Date, dayRepMax * BodyMass.PoundsPerKg);
                if (bodyweightMetric is not null && weight is { } bw) Record(histories, bodyweightMetric, day.Date, dayRepMax / bw.WeightKg);
            }

            var recent = byDay
                .Where(x => x.Date >= since)
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
                    poundsMetric, repMaxKg * BodyMass.PoundsPerKg, Basis.Modeled, evidence, recent.Date, Sources.Hevy, Models.Epley(reps));
            }

            if (bodyweightMetric is not null && weight is { } w)
            {
                measurements[bodyweightMetric] = new Measurement(
                    bodyweightMetric, repMaxKg / w.WeightKg, Basis.Modeled,
                    Text($"{evidence}, over {w.WeightKg * BodyMass.PoundsPerKg:0} lb bodyweight"), recent.Date,
                    Text($"{Sources.Hevy} and your latest weigh-in"), Models.Epley(reps));
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

        // A Romanian, stiff-leg, or straight-leg deadlift is an accessory, not
        // the pull the standard is written for. Hevy names one "Straight Leg
        // Deadlift", which the digest reported as a deadlift PR on 2026-11-04.
        return lift != "deadlift"
               || !(name.Contains("romanian") || name.Contains("stiff") || name.Contains("straight")
                    || name.Contains("rdl") || name.Contains("single leg") || name.Contains("single-leg"));
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
    /// What the most recent test says. The latest official test always counts.
    /// It is the athlete's record until the next one. One older than the
    /// measurement window says so in its evidence, because a gate scored from a
    /// months-old card is a different thing from one scored from last week's.
    /// </summary>
    /// <summary>
    /// The FBI test's events as measurements: the latest result for each event
    /// that was run. A test outranks a training set and the model.
    /// </summary>
    internal static void FbiMeasurements(
        IReadOnlyList<FbiResult> results,
        Dictionary<string, Measurement> measurements,
        Dictionary<string, List<DatedValue>>? histories = null)
    {
        void Event(string metric, Func<FbiResult, double?> read, bool lowerIsBetter)
        {
            foreach (var result in results)
            {
                if (read(result) is { } value && histories is not null) Record(histories, metric, result.Date, value);
            }

            if (results.FirstOrDefault(r => read(r) is not null) is not { } latest) return;

            // Better of the test and a measured training PR (a pull-up to
            // standard counts); a modeled value always gives way.
            Prefer(measurements, metric, read(latest)!.Value, lowerIsBetter, Text($"FBI PFT of {latest.Date:yyyy-MM-dd}"), latest.Date, Sources.Fbi);
        }

        Event(SelectionReadiness.Metrics.PullUps, r => r.PullUps, lowerIsBetter: false);
        Event(SelectionReadiness.Metrics.Sprint300m, r => r.Sprint300mSeconds, lowerIsBetter: true);
        Event(SelectionReadiness.Metrics.PushUps, r => r.PushUps, lowerIsBetter: false);
        Event(SelectionReadiness.Metrics.RunOneAndHalfMile, r => r.Run1_5MileSeconds, lowerIsBetter: true);
    }

    /// <summary>One FBI test with its points, the events not run left out of the total.</summary>
    internal static FbiResultDto ScoreFbi(FbiResult result, bool female)
    {
        int? Points(FbiEvent fbiEvent, double? raw) => raw is { } r ? Fbi.Points(fbiEvent, r, female) : null;
        var pull = Points(FbiEvent.PullUps, result.PullUps);
        var sprint = Points(FbiEvent.Sprint300m, result.Sprint300mSeconds);
        var push = Points(FbiEvent.PushUps, result.PushUps);
        var run = Points(FbiEvent.Run1_5Mile, result.Run1_5MileSeconds);
        int?[] all = [pull, sprint, push, run];
        return new FbiResultDto(
            result.Id, result.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.PullUps, result.Sprint300mSeconds, result.PushUps, result.Run1_5MileSeconds,
            pull, sprint, push, run, all.Sum(p => p ?? 0), all.Count(p => p is not null));
    }

    internal static void AftMeasurements(
        IReadOnlyList<AftResultDto> results,
        LocalDate since,
        BodyMetric? weight,
        Dictionary<string, Measurement> measurements,
        Dictionary<string, List<DatedValue>>? histories = null)
    {
        if (results.Count == 0) return;

        if (histories is not null)
        {
            foreach (var result in results)
            {
                var date = LocalDatePattern(result.Date);
                Record(histories, SelectionReadiness.Metrics.AftTotal, date, result.Total);
                Record(histories, SelectionReadiness.Metrics.AftLowestEvent, date, result.LowestEvent);
                Record(histories, SelectionReadiness.Metrics.HandReleasePushUps, date, result.HandReleasePushUps);
                Record(histories, SelectionReadiness.Metrics.Plank, date, result.PlankSeconds);
                Record(histories, SelectionReadiness.Metrics.SprintDragCarry, date, result.SprintDragCarrySeconds);
                Record(histories, SelectionReadiness.Metrics.DeadliftTripleLb, date, result.DeadliftKg * BodyMass.PoundsPerKg);
                if (weight is { } bw) Record(histories, SelectionReadiness.Metrics.DeadliftTripleToBodyweight, date, result.DeadliftKg / bw.WeightKg);
            }
        }

        var latest = results[0];
        var on = LocalDatePattern(latest.Date);

        var evidence = on < since
            ? Text($"AFT of {latest.Date} ({Period.Between(on, since.PlusDays(MeasurementWindowDays), PeriodUnits.Days).Days} days old, retest to refresh)")
            : Text($"AFT of {latest.Date}");

        measurements[SelectionReadiness.Metrics.AftTotal] = new Measurement(SelectionReadiness.Metrics.AftTotal, latest.Total, Basis.Measured, evidence, on, Sources.Aft);
        measurements[SelectionReadiness.Metrics.AftLowestEvent] = new Measurement(SelectionReadiness.Metrics.AftLowestEvent, latest.LowestEvent, Basis.Measured, evidence, on, Sources.Aft);

        // A test's events are measurements too, and outrank the model. They
        // outrank a strength-log set only when they are better.
        Prefer(measurements, SelectionReadiness.Metrics.RunTwoMile, latest.TwoMileSeconds, lowerIsBetter: true, evidence, on, Sources.Aft);
        Prefer(measurements, SelectionReadiness.Metrics.HandReleasePushUps, latest.HandReleasePushUps, lowerIsBetter: false, evidence, on, Sources.Aft);
        Prefer(measurements, SelectionReadiness.Metrics.Plank, latest.PlankSeconds, lowerIsBetter: false, evidence, on, Sources.Aft);
        Prefer(measurements, SelectionReadiness.Metrics.SprintDragCarry, latest.SprintDragCarrySeconds, lowerIsBetter: true, evidence, on, Sources.Aft);
        Prefer(measurements, SelectionReadiness.Metrics.DeadliftTripleLb, latest.DeadliftKg * BodyMass.PoundsPerKg, lowerIsBetter: false, evidence, on, Sources.Aft);

        // The test's deadlift is a real triple, which the log's Epley estimate
        // only approximates; over the current bodyweight it scores the entry
        // test's ratio too.
        if (weight is { } w)
        {
            Prefer(measurements, SelectionReadiness.Metrics.DeadliftTripleToBodyweight, latest.DeadliftKg / w.WeightKg, lowerIsBetter: false,
                Text($"{evidence}, over {w.WeightKg * BodyMass.PoundsPerKg:0} lb bodyweight"), on, Sources.Aft);
        }
    }

    private static void Prefer(
        Dictionary<string, Measurement> measurements, string metric, double value, bool lowerIsBetter, string evidence, LocalDate on, string source)
    {
        if (measurements.TryGetValue(metric, out var current) && current.Basis == Basis.Measured)
        {
            var better = lowerIsBetter ? value < current.Value : value > current.Value;
            if (!better) return;
        }

        measurements[metric] = new Measurement(metric, value, Basis.Measured, evidence, on, source);
    }

    private static BodyReportDto BodyReport(
        IReadOnlyList<BodyMetric> metrics,
        LocalDate since,
        Dictionary<string, Measurement> measurements,
        Dictionary<string, List<DatedValue>> histories)
    {
        foreach (var m in metrics)
        {
            Record(histories, SelectionReadiness.Metrics.BodyweightLb, m.Date, m.WeightKg * BodyMass.PoundsPerKg);
            if (m.BodyFatPercent is { } fat) Record(histories, SelectionReadiness.Metrics.BodyFat, m.Date, fat);
        }

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
                Text($"weigh-in of {latestFat.Date:yyyy-MM-dd}"), latestFat.Date, Sources.WeighIn);
        }

        return new BodyReportDto(
            points,
            bodyFat,
            leanMass,
            bodyFat is { } bf ? BodyComposition.SelectionRateByBodyFat(bf) : null,
            leanMass is { } lm ? BodyComposition.SelectionRateByLeanMass(lm) : null);
    }

    private static Measurement Modeled(string metric, double value, string evidence, LocalDate? on, string source, string model) =>
        new(metric, value, Basis.Modeled, evidence, on, source, model);

    /// <summary>Where the data behind a number came from, in the words of the screen it was entered or synced from.</summary>
    internal static class Sources
    {
        public const string Hevy = "Hevy";
        public const string WeighIn = "a weigh-in entered on the Log tab";
        public const string Aft = "an AFT entered on the Log tab";
        public const string Fbi = "an FBI PFT entered on the Log tab";
    }

    /// <summary>An activity's source as its maker names it.</summary>
    internal static string SourceName(string source) => source switch
    {
        "intervals-icu" => "Garmin, synced through intervals.icu",
        _ when source.StartsWith("garmin", StringComparison.Ordinal) => "Garmin",
        _ when source.StartsWith("hevy", StringComparison.Ordinal) => Sources.Hevy,
        _ => source
    };

    /// <summary>The models behind modeled numbers, each with its author and year.</summary>
    internal static class Models
    {
        public const string Vdot = "Daniels and Gilbert VDOT race equivalence (1979): the time trial's VDOT gives the equal time at this distance";
        public const string LoadCarriage = "Pandolf load-carriage model (1977), run on your VDOT and bodyweight";
        public const string HeartRatePace = "Heart-rate-scaled pace: each easy run's pace times its heart rate over your AeT heart rate, the month's median";

        public static string Epley(int reps) => reps == 1
            ? "Epley one-rep-max estimate (1985) from your best recent set"
            : $"Epley one-rep-max estimate (1985) from your best recent set, read back to {reps} reps";
    }

    private static StandingDto Dto(Measurement m) =>
        new(m.Metric, m.Value, m.Basis.ToString(), m.Evidence, m.On?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), m.Source, m.Model);

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
