using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// The athlete as the model sees them: an anchor, a fitted set of parameters,
/// the week they are currently training, and the history all three came from.
/// </summary>
internal sealed record AthleteSnapshot(
    AthleteSettings Row,
    double AnchorVdot,
    double? ReclaimVdot,
    TrainingDose MeasuredDose,
    int MeasuredSessions,
    double MeasuredWeeks,
    FitResult Fit,
    double? EasyPaceSecPerKm,
    IReadOnlyList<MonthlyAerobicPoint> Trend,
    IReadOnlyList<FitObservation> Observations);

/// <summary>Builds the analysis and prediction views out of stored data and the domain models.</summary>
public static class FitnessReports
{
    /// <summary>Weeks of log that count as "the week you are on now".</summary>
    private const double RecentWeeks = 8;

    /// <summary>Distances every projection reports unless the caller names others.</summary>
    public static IReadOnlyList<double> DefaultDistances { get; } =
        [1.5 * Vdot.MileMeters, 2 * Vdot.MileMeters, 5 * Vdot.MileMeters];

    /// <summary>Horizons every projection reports unless the caller names others.</summary>
    public static IReadOnlyList<double> DefaultHorizons { get; } = [0, 3, 6, 12, 18, 24];

    public static async Task<SummaryDto> SummaryAsync(FitnessDbContext database, CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(database, cancellationToken);
        var weight = await database.BodyMetrics.OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var zone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
        var trend = AerobicAnalysis.MonthlyTrend(
            await SteadyRunsAsync(database, cancellationToken), settings.ReferenceHr);

        var weeks = WeeklyVolumes(await database.Activities
            .Where(a => a.Sport == "run" || a.Sport == "ruck")
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken), zone);

        var strength = await StrengthTrendAsync(database, zone, cancellationToken);

        var highlights = Highlights.Build(
            trend,
            weeks.Select(w => new WeekVolume(LocalDatePattern(w.WeekStart), w.Minutes)).ToArray(),
            settings.PlanMinutesPerWeek,
            strength.Select(s => new E1RmPoint(LocalDatePattern(s.Date), s.Exercise, s.E1RmKg)).ToArray());

        double? spread = null;
        if (settings.LtSecondsPerKm is { } lt && trend.Count > 0)
        {
            spread = AerobicAnalysis.DeficiencySpread(trend[^1].MedianNormalizedSecPerKm, lt);
        }

        var paces = TrainingPaces.For(settings.StartVdot)
            .Select(p => new TrainingPaceDto(
                p.Zone, p.Name, p.Purpose,
                Math.Round(p.FastSecPerKm), Math.Round(p.SlowSecPerKm)))
            .ToArray();

        var (measured, sessions) = await MeasuredDoseAsync(database, settings.StartVdot, cancellationToken);

        return new SummaryDto(
            settings with { CurrentWeightKg = weight?.WeightKg },
            trend.Select(p => new AerobicPointDto($"{p.Year:0000}-{p.Month:00}", p.MedianNormalizedSecPerKm, p.RunCount)).ToArray(),
            weeks,
            strength,
            highlights.Select(h => new HighlightDto(h.Kind, h.Headline, h.Evidence, h.Positive)).ToArray(),
            paces,
            Dose(measured),
            Steps(SessionMix.Explain(measured, RecentWeeks, sessions)),
            spread,
            await database.Activities.CountAsync(cancellationToken));
    }

    public static async Task<PredictionDto> PredictionsAsync(
        FitnessDbContext database,
        TrainingDose plan,
        double compliance,
        double? targetWeightKg,
        IReadOnlyList<double> distances,
        IReadOnlyList<double> horizons,
        int currentYear,
        CancellationToken cancellationToken)
    {
        var athlete = await SnapshotAsync(database, currentYear, cancellationToken);
        var row = athlete.Row;
        var currentWeight = (await database.BodyMetrics.OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync(cancellationToken))?.WeightKg;

        var altitudePenalty = Altitude.Penalty(row.HomeAltitudeMeters);
        var trace = new CalculationTrace();

        // The weight every number below is quoted at: what the athlete asked
        // for, or what they weigh now.
        var raceWeight = targetWeightKg ?? currentWeight;

        var startVdot = athlete.AnchorVdot;
        if (raceWeight is { } target && currentWeight is { } current && Math.Abs(target - current) > 0.01)
        {
            startVdot = BodyMass.AdjustVdot(startVdot, current, target);
            trace.Add(
                "Race weight, on the anchor",
                Text($"VDOT {athlete.AnchorVdot:0.0} × {current:0.0} ÷ {target:0.0} kg (absolute VO2 preserved, clamped to ±10%)"),
                Text($"VDOT {startVdot:0.0}"),
                Citations.CuretonSparling.Id);
        }

        // And on the ceiling. A lifetime best was run at a bodyweight too, so
        // moving the anchor without moving the peak shrinks the reclaim runway
        // and makes racing lighter look worse than racing heavy.
        var reclaimVdot = BodyMass.AtRaceWeight(athlete.ReclaimVdot, athlete.Row.PastPeakWeightKg, raceWeight);
        if (reclaimVdot is { } reclaimed && athlete.ReclaimVdot is { } asRun
            && Math.Abs(reclaimed - asRun) > 0.01 && athlete.Row.PastPeakWeightKg is { } peakWeight
            && raceWeight is { } racing)
        {
            trace.Add(
                "Race weight, on the reclaimable peak",
                Text($"VDOT {asRun:0.0} × {peakWeight:0.0} ÷ {racing:0.0} kg — the best was set at {peakWeight * BodyMass.PoundsPerKg:0} lb"),
                Text($"VDOT {reclaimed:0.0}"),
                Citations.CuretonSparling.Id);
        }
        else if (athlete.ReclaimVdot is not null && athlete.Row.PastPeakWeightKg is null)
        {
            trace.Add(
                "Race weight, on the reclaimable peak",
                "not applied — the weight the lifetime best was set at is not recorded",
                "peak left as run",
                Citations.CuretonSparling.Id);
        }

        var p = new TrajectoryParameters(
            startVdot, reclaimVdot, athlete.Fit.RatePerMonth.Value, athlete.Fit.Responsiveness.Value);

        var effective = plan.Scale(compliance);
        var schedule = new DoseSchedule(effective, athlete.MeasuredDose);
        var allocation = new DoseResponse.Allocation(
            effective,
            DoseResponse.Marginal(effective, TrainingZone.Easy, p.Responsiveness),
            0);

        trace
            .Add(
                "Compliance",
                Text($"{plan.RunningHours:0.0} h planned × {Format.Percent(compliance)} kept"),
                Text($"{effective.RunningHours:0.0} h/week actually trained"),
                Citations.CogganPmc.Id)
            .AddRange(DoseResponse.Explain(effective, p.Responsiveness))
            .Add(
                "Building to it",
                Text($"from your logged {athlete.MeasuredDose.RunningHours:0.0} h/week at 8% a week"),
                Text($"{schedule.MonthsToFullDose():0.0} months of ramp"),
                Citations.GabbettWorkload.Id)
            .AddRange(athlete.Fit.Steps);

        var curve = new List<BandDto>();
        for (double m = 0; m <= 30; m += 0.5)
        {
            curve.Add(BandOf(Forecast.At(p, athlete.Fit, schedule, m)));
        }

        var checkpoints = horizons
            .Select(m =>
            {
                var band = Forecast.At(p, athlete.Fit, schedule, m);
                return new CheckpointDto(
                    m, band.Vdot, band.Low, band.High,
                    distances.Select(d => Race(d, band, row.HomeAltitudeMeters)).ToArray());
            })
            .ToArray();

        var goals = new List<GoalOutlookDto>();
        foreach (var goal in await database.Goals.OrderBy(g => g.Metric).ToListAsync(cancellationToken))
        {
            if (GoalDistanceMeters(goal) is not { } distance) continue;

            var targetVdot = Vdot.FromRace(
                distance, Altitude.ToSeaLevel(goal.TargetValue, row.HomeAltitudeMeters) / 60.0);
            var monthsAway = MonthsUntil(goal.TargetDate);
            var report = Feasibility.Assess(
                Context(athlete, p, plan.RunningHours),
                new GoalRequest(distance, goal.TargetValue, Math.Max(0.5, monthsAway)),
                athlete.EasyPaceSecPerKm);

            goals.Add(new GoalOutlookDto(
                goal.Metric,
                goal.Label ?? Label(goal.Metric, distance),
                distance,
                goal.TargetValue,
                targetVdot,
                goal.TargetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                monthsAway,
                Trajectory.MonthsToReach(p, schedule, targetVdot),
                Forecast.Probability(p, athlete.Fit, schedule, targetVdot, Math.Max(0.5, monthsAway)),
                report.Verdict.ToString(),
                report.Headline));
        }

        var assumptions = Assumptions(athlete, p, altitudePenalty, row);

        return new PredictionDto(
            Dose(plan),
            Dose(athlete.MeasuredDose),
            Dose(effective),
            Trajectory.Ceiling(p, effective),
            allocation.HourPrice,
            allocation.StrainPrice,
            schedule.MonthsToFullDose(),
            athlete.AnchorVdot,
            startVdot,
            reclaimVdot,
            altitudePenalty * 100,
            curve,
            checkpoints,
            goals,
            FitOf(athlete.Fit),
            RealityCheck(athlete, p, schedule),
            Steps(trace.Steps),
            assumptions);
    }

    /// <summary>The full assessment of one goal, from any distance and any date.</summary>
    public static async Task<FeasibilityDto> GoalAsync(
        FitnessDbContext database,
        double distanceMeters,
        double targetSeconds,
        double monthsAvailable,
        double availableHours,
        int currentYear,
        CancellationToken cancellationToken)
    {
        var athlete = await SnapshotAsync(database, currentYear, cancellationToken);
        var p = new TrajectoryParameters(
            athlete.AnchorVdot,
            athlete.ReclaimVdot,
            athlete.Fit.RatePerMonth.Value,
            athlete.Fit.Responsiveness.Value);

        var report = Feasibility.Assess(
            Context(athlete, p, availableHours),
            new GoalRequest(distanceMeters, targetSeconds, monthsAvailable),
            athlete.EasyPaceSecPerKm);

        return new FeasibilityDto(
            report.Verdict.ToString(),
            report.Headline,
            report.Detail,
            report.BindingConstraint,
            distanceMeters,
            targetSeconds,
            monthsAvailable,
            report.TargetVdot,
            report.StartVdot,
            report.Grade,
            report.GradeBand,
            report.RecordEquivalentSeconds,
            report.RecordHolder,
            report.CeilingReachable,
            report.Prescription is { } prescription
                ? new PrescriptionDto(
                    Dose(prescription.Dose),
                    prescription.HourPrice,
                    prescription.StrainPrice,
                    prescription.RampMonths,
                    prescription.WeeklyMiles)
                : null,
            report.MonthsAtHoursAvailable,
            report.EarliestMonths,
            report.ProbabilityByDate,
            report.MonthsForEvenOdds,
            report.AchievableSecondsByDate,
            Steps(report.Steps).Concat(Steps(athlete.Fit.Steps)).ToArray());
    }

    /// <summary>
    /// The athlete as the solver needs them: the cached posterior plus the
    /// fixed facts every what-if holds constant.
    /// </summary>
    public static async Task<SolverContext> SolverContextAsync(
        FitnessDbContext database,
        PosteriorCache cache,
        int currentYear,
        CancellationToken cancellationToken)
    {
        var athlete = await SnapshotAsync(database, currentYear, cancellationToken);
        var observations = athlete.Observations;

        var posterior = await cache.GetAsync(
            observations,
            new Posterior.Priors(
                observations.Count > 0 ? observations[0].ObservedVdot : athlete.AnchorVdot),
            athlete.ReclaimVdot,
            cancellationToken);

        var mass = (await database.BodyMetrics.OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync(cancellationToken))?.WeightKg;

        return new SolverContext(
            posterior,
            athlete.AnchorVdot,
            athlete.ReclaimVdot,
            athlete.MeasuredDose,
            mass,
            athlete.Row.HomeAltitudeMeters,
            new DoseLimits(),
            athlete.Row.PastPeakWeightKg);
    }

    private static AthleteContext Context(AthleteSnapshot athlete, TrajectoryParameters p, double availableHours) =>
        new(
            p,
            athlete.Fit,
            athlete.MeasuredDose,
            availableHours,
            athlete.Row.BirthYear is { } year ? DateTime.UtcNow.Year - year : null,
            athlete.Row.Female ?? false,
            athlete.Row.HomeAltitudeMeters);

    /// <summary>
    /// Assembles the athlete: the measured anchor, the reclaimable peak, the
    /// week they are training now, and the model fitted to their history.
    /// </summary>
    internal static async Task<AthleteSnapshot> SnapshotAsync(
        FitnessDbContext database, int currentYear, CancellationToken cancellationToken)
    {
        var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
                  ?? new AthleteSettings { Id = 1 };

        var trend = AerobicAnalysis.MonthlyTrend(
            await SteadyRunsAsync(database, cancellationToken), row.ReferenceHr);

        var (measured, sessions) = await MeasuredDoseAsync(database, row.StartVdot, cancellationToken);
        var observations = await ObservationsAsync(database, row, trend, cancellationToken);

        var fit = ModelFit.Fit(
            observations,
            new ModelFit.Priors(observations.Count > 0 ? observations[0].ObservedVdot : row.StartVdot));

        var easyPace = TrainingPaces.For(row.StartVdot).Single(b => b.Zone == "E");

        return new AthleteSnapshot(
            row,
            row.StartVdot,
            ReclaimVdotFrom(row, currentYear),
            measured,
            sessions,
            RecentWeeks,
            fit,
            (easyPace.FastSecPerKm + easyPace.SlowSecPerKm) / 2,
            trend,
            observations);
    }

    /// <summary>
    /// The athlete's fitness month by month, in VDOT, from the pace they hold
    /// at a fixed heart rate.
    /// </summary>
    /// <remarks>
    /// Normalized pace is a fitness signal, not a race result, so it is turned
    /// into a score by ratio to the most recent month — which is the month the
    /// measured anchor belongs to — and raised to the elasticity of VDOT with
    /// respect to speed (<see cref="Vdot.SpeedElasticity"/>), computed at the
    /// athlete's own pace rather than assumed to be one.
    /// </remarks>
    private static async Task<IReadOnlyList<FitObservation>> ObservationsAsync(
        FitnessDbContext database,
        AthleteSettings row,
        IReadOnlyList<MonthlyAerobicPoint> trend,
        CancellationToken cancellationToken)
    {
        if (trend.Count < ModelFit.MinimumObservations) return [];

        var zone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
        var activities = await database.Activities.OrderBy(a => a.StartedAt).ToListAsync(cancellationToken);

        var latest = trend[^1];
        var reference = latest.MedianNormalizedSecPerKm;
        var elasticity = Vdot.SpeedElasticity(5000, reference * 5 / 60.0);
        var first = trend[0];

        return trend
            .Select(point =>
            {
                var months = (point.Year - first.Year) * 12 + (point.Month - first.Month);
                var vdot = row.StartVdot * Math.Pow(reference / point.MedianNormalizedSecPerKm, elasticity);

                var sessions = activities
                    .Where(a =>
                    {
                        var date = a.StartedAt.InZone(zone).Date;
                        return date.Year == point.Year && date.Month == point.Month;
                    })
                    .Select(a => new LoggedSession(a.Sport, a.DistanceMeters, a.DurationSeconds))
                    .ToArray();

                return new FitObservation(
                    months, vdot, SessionMix.WeeklyDose(sessions, DoseSchedule.WeeksPerMonth, row.StartVdot));
            })
            .ToArray();
    }

    /// <summary>The week the athlete is actually training, read out of the log.</summary>
    private static async Task<(TrainingDose Dose, int Sessions)> MeasuredDoseAsync(
        FitnessDbContext database, double vdot, CancellationToken cancellationToken)
    {
        var since = SystemClock.Instance.GetCurrentInstant()
            .Minus(Duration.FromDays(RecentWeeks * 7));

        var recent = await database.Activities
            .Where(a => a.StartedAt >= since)
            .Select(a => new { a.Sport, a.DistanceMeters, a.DurationSeconds })
            .ToListAsync(cancellationToken);

        var sessions = recent
            .Select(a => new LoggedSession(a.Sport, a.DistanceMeters, a.DurationSeconds))
            .ToArray();

        return (SessionMix.WeeklyDose(sessions, RecentWeeks, vdot), sessions.Length);
    }

    private static RealityCheckDto RealityCheck(
        AthleteSnapshot athlete, TrajectoryParameters p, DoseSchedule schedule)
    {
        double? measured = null;
        var days = 0;
        if (athlete.Trend.Count >= 2)
        {
            var first = athlete.Trend[0];
            var last = athlete.Trend[^1];
            measured = (first.MedianNormalizedSecPerKm - last.MedianNormalizedSecPerKm)
                       / first.MedianNormalizedSecPerKm * 100;
            days = ((last.Year - first.Year) * 12 + (last.Month - first.Month)) * 30;
        }

        var now = Vdot.MinutesFor(2 * Vdot.MileMeters, p.StartVdot);
        var inNinety = Vdot.MinutesFor(2 * Vdot.MileMeters, Trajectory.VdotAt(p, schedule, 3));

        return new RealityCheckDto(measured, days, (now - inNinety) / now * 100);
    }

    private static IReadOnlyList<string> Assumptions(
        AthleteSnapshot athlete, TrajectoryParameters p, double altitudePenalty, AthleteSettings row)
    {
        var assumptions = new List<string>
        {
            Text($"Fitness climbs towards a ceiling set by the training week, zone by zone with each zone saturating; the split is the one that maximises that ceiling [banister-impulse-response, seiler-polarized, ronnestad-strength]."),
            "Race equivalencies via Daniels VDOT [daniels-vdot].",
            Text($"Recovery is priced: an hour at threshold costs 2.5 easy hours and an hour of intervals 4.5, and the week is built from your logged volume at 8% a week rather than started at [coggan-training-load, gabbett-workload].")
        };

        assumptions.Add(athlete.Fit.Observations >= ModelFit.MinimumObservations
            ? Text($"Approach rate and responsiveness fitted to your own {athlete.Fit.Observations} months (R² {athlete.Fit.RSquared:0.00}, {Format.Percent(athlete.Fit.DataWeight)} of the answer from your data rather than the priors) [seber-wild-nls].")
            : Text($"Fewer than {ModelFit.MinimumObservations} months of imported runs, so the rate and responsiveness are the literature priors, not yours [seber-wild-nls]."));

        if (athlete.ReclaimVdot is { } reclaim)
        {
            assumptions.Add(Text(
                $"You have held VDOT {reclaim:0.0} before (age-adjusted [wma-age-grading]); fitness up to that level is reclaimed at {Retraining.ReclaimRateMultiplier:0.0}× the de-novo rate [mujika-retraining, muscle-memory]."));
        }

        if (altitudePenalty > 0)
        {
            assumptions.Add(Text(
                $"Times shown for your home altitude ({row.HomeAltitudeMeters:0} m): aerobic races run ~{Format.Percent(altitudePenalty, 1)} slower there than at sea level [peronnet-altitude]."));
        }

        assumptions.Add(row.Female is null
            ? "Sex is unstated, so targets are graded against the open men's record book — the most permissive bound available [wma-age-grading]."
            : Text($"Targets are graded against the {(row.Female.Value ? "women's" : "men's")} record book, age-adjusted [wma-age-grading]."));

        return assumptions;
    }

    private static BandDto BandOf(Band band) =>
        new(band.Months, band.Vdot, band.Low, band.High, band.StandardDeviation);

    /// <summary>A band in VDOT, expressed as the race times it stands for.</summary>
    private static RaceTimeDto Race(double distanceMeters, Band band, double altitudeMeters)
    {
        double Seconds(double vdot) =>
            Altitude.AtAltitude(Vdot.MinutesFor(distanceMeters, vdot) * 60, altitudeMeters);

        // A higher VDOT is a faster time, so the band inverts.
        return new RaceTimeDto(distanceMeters, Seconds(band.Vdot), Seconds(band.High), Seconds(band.Low));
    }

    private static DoseDto Dose(TrainingDose dose) =>
        new(
            dose.EasyHours, dose.ThresholdHours, dose.IntervalHours, dose.StrengthHours,
            dose.RunningHours, dose.Strain, dose.EasyShare,
            TrainingDose.Zones
                .Select(z => new ZoneHoursDto(
                    z.ToString(),
                    dose[z],
                    TrainingDose.StrainWeight(z) * dose[z],
                    DoseResponse.Marginal(dose, z)))
                .ToArray());

    private static FitDto FitOf(FitResult fit) =>
        new(
            fit.StartVdot.Value,
            fit.RatePerMonth.Value,
            fit.RatePerMonth.StandardError,
            fit.Responsiveness.Value,
            fit.Responsiveness.StandardError,
            fit.ResidualSd,
            fit.RSquared,
            fit.Observations,
            fit.DataWeight,
            Steps(fit.Steps));

    private static IReadOnlyList<StepDto> Steps(IReadOnlyList<CalculationStep> steps) =>
        steps.Select(s => new StepDto(s.Label, s.Expression, s.Value, s.CitationId)).ToArray();

    private static double MonthsUntil(LocalDate date)
    {
        var today = SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Etc/UTC"]).Date;
        return Period.Between(today, date, PeriodUnits.Days).Days / 30.4375;
    }

    /// <summary>The distance a goal is over: the one it stores, or the one its key implies.</summary>
    internal static double? GoalDistanceMeters(Goal goal) =>
        goal.DistanceMeters ?? RunGoalDistanceMeters(goal.Metric);

    internal static double? RunGoalDistanceMeters(string metric) => metric switch
    {
        "run-1.5mi" => 1.5 * Vdot.MileMeters,
        "run-2mi" => 2 * Vdot.MileMeters,
        "run-5mi" => 5 * Vdot.MileMeters,
        "run-10mi" => 10 * Vdot.MileMeters,
        _ => null
    };

    private static string Label(string metric, double distanceMeters) => metric switch
    {
        "run-1.5mi" => "1.5-mile run",
        "run-2mi" => "2-mile run",
        "run-5mi" => "5-mile run",
        "run-10mi" => "10-mile run",
        _ => Text($"{distanceMeters / Vdot.MileMeters:0.##}-mile run")
    };

    /// <summary>The age-adjusted reclaimable peak from stored settings, if any.</summary>
    internal static double? ReclaimVdotFrom(AthleteSettings row, int currentYear)
    {
        if (row.PastPeakDistanceMeters is not { } distance || row.PastPeakSeconds is not { } seconds)
        {
            return null;
        }

        var peakVdot = Vdot.FromRace(distance, Altitude.ToSeaLevel(seconds, row.HomeAltitudeMeters) / 60.0);

        if (row is { BirthYear: { } birthYear, PastPeakYear: { } peakYear })
        {
            return Retraining.AgeAdjustedPeak(
                peakVdot,
                ageAtPeak: Math.Max(0, peakYear - birthYear),
                ageNow: Math.Max(peakYear - birthYear, currentYear - birthYear));
        }

        return peakVdot;
    }

    private static async Task<IReadOnlyList<SteadyRun>> SteadyRunsAsync(
        FitnessDbContext database, CancellationToken cancellationToken)
    {
        var zone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
        var runs = await database.Activities
            .Where(a => a.Sport == "run" && a.DistanceMeters != null && a.AverageHr != null)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);

        return runs
            .Select(a => new SteadyRun(
                a.StartedAt.InZone(zone).Date,
                a.DistanceMeters!.Value,
                a.DurationSeconds,
                a.AverageHr!.Value))
            .ToArray();
    }

    private static async Task<SettingsDto> SettingsAsync(FitnessDbContext database, CancellationToken cancellationToken)
    {
        var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
                  ?? new AthleteSettings { Id = 1 };

        return new SettingsDto(
            row.ReferenceHr,
            row.LtSecondsPerKm,
            row.PlanMinutesPerWeek,
            row.StartVdot,
            row.VdotMeasuredOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            null,
            row.BirthYear,
            row.Female,
            row.AvailableHoursPerWeek,
            row.PastPeakDistanceMeters,
            row.PastPeakSeconds,
            row.PastPeakYear,
            row.PastPeakWeightKg,
            row.GoalWeightKg,
            BodyMass.MaxAdjustmentFraction,
            row.HomeAltitudeMeters);
    }

    private static IReadOnlyList<WeekVolumeDto> WeeklyVolumes(List<Activity> endurance, DateTimeZone zone)
    {
        if (endurance.Count == 0) return [];

        static LocalDate WeekStart(LocalDate date) =>
            date.PlusDays(-(((int)date.DayOfWeek - 1 + 7) % 7));

        var byWeek = endurance
            .GroupBy(a => WeekStart(a.StartedAt.InZone(zone).Date))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.DurationSeconds) / 60.0);

        var first = byWeek.Keys.Min();
        var last = byWeek.Keys.Max();

        var weeks = new List<WeekVolumeDto>();
        for (var week = first; week <= last; week = week.PlusDays(7))
        {
            weeks.Add(new WeekVolumeDto(
                week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                byWeek.TryGetValue(week, out var minutes) ? Math.Round(minutes, 1) : 0));
        }

        return weeks;
    }

    private static async Task<IReadOnlyList<E1RmDto>> StrengthTrendAsync(
        FitnessDbContext database, DateTimeZone zone, CancellationToken cancellationToken)
    {
        var sets = await database.StrengthSets
            .Join(database.Activities, s => s.ActivityId, a => a.Id, (s, a) => new { s, a.StartedAt })
            .Where(x => x.s.WeightKg > 0 && x.s.Reps >= 1 && x.s.Reps <= OneRepMax.MaxTrustworthyReps)
            .ToListAsync(cancellationToken);

        return sets
            .GroupBy(x => (Exercise: x.s.Exercise, Date: x.StartedAt.InZone(zone).Date))
            .Select(g => new E1RmDto(
                g.Key.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Key.Exercise,
                Math.Round(g.Max(x => OneRepMax.Epley(x.s.WeightKg, x.s.Reps)), 1)))
            .OrderBy(e => e.Date)
            .ToArray();
    }

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
