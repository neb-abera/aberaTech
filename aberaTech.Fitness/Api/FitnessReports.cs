using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

public sealed record AerobicPointDto(string Month, double MedianSecPerKm, int Runs);

public sealed record WeekVolumeDto(string WeekStart, double Minutes);

public sealed record E1RmDto(string Date, string Exercise, double E1RmKg);

public sealed record HighlightDto(string Kind, string Headline, string Evidence, bool Positive);

public sealed record SettingsDto(
    int ReferenceHr,
    double? LtSecondsPerKm,
    double PlanMinutesPerWeek,
    double StartVdot,
    string? VdotMeasuredOn,
    double? CurrentWeightKg,
    int? BirthYear,
    double? PastPeakDistanceMeters,
    double? PastPeakSeconds,
    int? PastPeakYear,
    double HomeAltitudeMeters,
    double? PastAltitudeMeters);

public sealed record TrainingPaceDto(
    string Zone,
    string Name,
    string Purpose,
    double FastSecPerKm,
    double SlowSecPerKm);

public sealed record SummaryDto(
    SettingsDto Settings,
    IReadOnlyList<AerobicPointDto> AerobicTrend,
    IReadOnlyList<WeekVolumeDto> WeeklyVolume,
    IReadOnlyList<E1RmDto> StrengthTrend,
    IReadOnlyList<HighlightDto> Highlights,
    IReadOnlyList<TrainingPaceDto> TrainingPaces,
    double? DeficiencySpread,
    int ActivityCount);

public sealed record ProjectionPointDto(double Months, double Vdot);

public sealed record RaceTimesDto(double Months, double Vdot, double TwoMileSeconds, double FiveMileSeconds, double OneAndAHalfMileSeconds);

public sealed record GoalOutlookDto(string Metric, double TargetValue, double TargetVdot, string TargetDate, double? MonthsToReach, bool Reachable);

public sealed record RealityCheckDto(
    double? MeasuredPacePercent,
    int MeasuredOverDays,
    double ModelPacePercentNext90Days);

public sealed record PredictionDto(
    double EffectiveHours,
    double Ceiling,
    double WeightAdjustedStartVdot,
    double? ReclaimVdot,
    double AltitudePenaltyPercent,
    IReadOnlyList<ProjectionPointDto> Curve,
    IReadOnlyList<RaceTimesDto> Checkpoints,
    IReadOnlyList<GoalOutlookDto> Goals,
    RealityCheckDto RealityCheck,
    IReadOnlyList<string> Assumptions);

public sealed record RequiredDoseDto(
    double StartVdot,
    double TargetVdot,
    double MonthsAvailable,
    double? RequiredEffectiveHours,
    double? RequiredWeeklyHoursAtCompliance,
    string Verdict);

/// <summary>Builds the analysis and prediction views out of stored data and the domain models.</summary>
public static class FitnessReports
{
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

        return new SummaryDto(
            settings with { CurrentWeightKg = weight?.WeightKg },
            trend.Select(p => new AerobicPointDto($"{p.Year:0000}-{p.Month:00}", p.MedianNormalizedSecPerKm, p.RunCount)).ToArray(),
            weeks,
            strength,
            highlights.Select(h => new HighlightDto(h.Kind, h.Headline, h.Evidence, h.Positive)).ToArray(),
            paces,
            spread,
            await database.Activities.CountAsync(cancellationToken));
    }

    public static async Task<PredictionDto> PredictionsAsync(
        FitnessDbContext database,
        double weeklyHours,
        double compliance,
        double? targetWeightKg,
        int currentYear,
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(database, cancellationToken);
        var currentWeight = (await database.BodyMetrics.OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync(cancellationToken))?.WeightKg;

        var altitudePenalty = Altitude.Penalty(settings.HomeAltitudeMeters);

        var assumptions = new List<string>
        {
            "Trajectory: fitness climbs toward a dose-set ceiling, C = 38 + 1.6 × effective weekly hours, calibrated to documented aerobic-deficiency recoveries [banister-impulse-response, uphill-athlete-aet, seiler-polarized].",
            "Race equivalencies via Daniels VDOT [daniels-vdot].",
        };

        var startVdot = settings.StartVdot;
        if (targetWeightKg is { } target && currentWeight is { } current && Math.Abs(target - current) > 0.01)
        {
            startVdot = Domain.BodyMass.AdjustVdot(startVdot, current, target);
            assumptions.Add(
                $"Bodyweight {current:0.0} → {target:0.0} kg scales VDOT by mass ratio (fat-mass change, absolute VO2 preserved; clamped to ±10%) [cureton-added-mass]. Assumed reached by the horizon.");
        }

        var row = await database.Settings.SingleOrDefaultAsync(r => r.Id == 1, cancellationToken)
                  ?? new AthleteSettings { Id = 1 };
        var reclaimVdot = ReclaimVdotFrom(row, currentYear);
        if (reclaimVdot is not null)
        {
            assumptions.Add(
                $"You have held VDOT {reclaimVdot:0.0} before (age-adjusted [wma-age-grading]); fitness up to that level is reclaimed at {Retraining.ReclaimRateMultiplier:0.0}× the de-novo rate — detrained athletes are not beginners [mujika-retraining, muscle-memory]. Only territory beyond the lifetime best moves at the slow rate.");
        }

        if (altitudePenalty > 0)
        {
            assumptions.Add(
                $"Times shown for where you are now ({settings.HomeAltitudeMeters:0} m): aerobic races run ~{altitudePenalty:P1} slower there than at sea level [peronnet-altitude]. "
                + (settings.PastAltitudeMeters is { } past
                    ? $"The anchor and lifetime best are scored at {past:0} m, where they were run."
                    : "The anchor and past peak are assumed run at the same altitude."));
        }

        var p = new TrajectoryParameters(startVdot, reclaimVdot);
        var effective = Trajectory.EffectiveHours(weeklyHours, compliance);
        var ceiling = Trajectory.Ceiling(p, effective);

        var curve = new List<ProjectionPointDto>();
        for (double m = 0; m <= 30; m += 0.5)
        {
            curve.Add(new ProjectionPointDto(m, Trajectory.VdotAt(p, effective, m)));
        }

        var checkpoints = new[] { 0.0, 6, 12, 18, 24 }
            .Select(m =>
            {
                var v = Trajectory.VdotAt(p, effective, m);
                return new RaceTimesDto(
                    m, v,
                    Altitude.AtAltitude(Vdot.MinutesFor(2 * Vdot.MileMeters, v) * 60, settings.HomeAltitudeMeters),
                    Altitude.AtAltitude(Vdot.MinutesFor(5 * Vdot.MileMeters, v) * 60, settings.HomeAltitudeMeters),
                    Altitude.AtAltitude(Vdot.MinutesFor(1.5 * Vdot.MileMeters, v) * 60, settings.HomeAltitudeMeters));
            })
            .ToArray();

        var goals = new List<GoalOutlookDto>();
        foreach (var goal in await database.Goals.OrderBy(g => g.Metric).ToListAsync(cancellationToken))
        {
            if (RunGoalDistanceMeters(goal.Metric) is not { } distance) continue;

            // The goal time will be run at home altitude, so its difficulty in
            // VDOT terms is its sea-level equivalent.
            var targetVdot = Vdot.FromRace(
                distance,
                Altitude.ToSeaLevel(goal.TargetValue, settings.HomeAltitudeMeters) / 60.0);
            var months = Trajectory.MonthsToReach(p, effective, targetVdot);
            goals.Add(new GoalOutlookDto(
                goal.Metric, goal.TargetValue, targetVdot, goal.TargetDate.ToString("yyyy-MM-dd", null),
                months, months is not null));
        }

        var realityCheck = await RealityCheckAsync(database, settings.ReferenceHr, p, effective, cancellationToken);

        return new PredictionDto(
            effective, ceiling, startVdot, reclaimVdot, altitudePenalty * 100,
            curve, checkpoints, goals, realityCheck, assumptions);
    }

    /// <summary>
    /// The model against the athlete's own data: measured normalized-pace
    /// improvement between the first and last month with runs, next to what
    /// the current sliders project for the coming 90 days. Pace and VDOT move
    /// nearly proportionally over these ranges, so the two percentages are
    /// comparable.
    /// </summary>
    private static async Task<RealityCheckDto> RealityCheckAsync(
        FitnessDbContext database,
        int referenceHr,
        TrajectoryParameters p,
        double effectiveHours,
        CancellationToken cancellationToken)
    {
        var trend = AerobicAnalysis.MonthlyTrend(await SteadyRunsAsync(database, cancellationToken), referenceHr);

        double? measured = null;
        var days = 0;
        if (trend.Count >= 2)
        {
            var first = trend[0];
            var last = trend[^1];
            measured = (first.MedianNormalizedSecPerKm - last.MedianNormalizedSecPerKm)
                       / first.MedianNormalizedSecPerKm * 100;
            days = ((last.Year - first.Year) * 12 + (last.Month - first.Month)) * 30;
        }

        var now = Vdot.MinutesFor(2 * Vdot.MileMeters, p.StartVdot);
        var inNinety = Vdot.MinutesFor(2 * Vdot.MileMeters, Trajectory.VdotAt(p, effectiveHours, 3));
        var model = (now - inNinety) / now * 100;

        return new RealityCheckDto(measured, days, model);
    }

    public static RequiredDoseDto RequiredDose(
        double startVdot,
        double? reclaimVdot,
        double homeAltitudeMeters,
        double distanceMeters,
        double targetSeconds,
        double monthsAvailable,
        double compliance)
    {
        var targetVdot = Vdot.FromRace(
            distanceMeters, Altitude.ToSeaLevel(targetSeconds, homeAltitudeMeters) / 60.0);
        var p = new TrajectoryParameters(startVdot, reclaimVdot);
        var requiredEffective = Trajectory.HoursToReach(p, targetVdot, monthsAvailable);

        string verdict;
        double? weekly = null;
        if (requiredEffective is null)
        {
            verdict = "No realistic dose reaches this in the time available. Move the date, the target, or both.";
        }
        else if (requiredEffective == 0)
        {
            verdict = "Already at or past this target.";
        }
        else
        {
            weekly = compliance > 0 ? requiredEffective / compliance : null;
            verdict = requiredEffective > 12
                ? "Reachable on paper, but the required volume is elite-athlete territory — treat the date as aggressive."
                : "Reachable at this dose, held consistently.";
        }

        return new RequiredDoseDto(startVdot, targetVdot, monthsAvailable, requiredEffective, weekly, verdict);
    }

    /// <summary>The age-adjusted reclaimable peak from stored settings, if any.</summary>
    internal static double? ReclaimVdotFrom(AthleteSettings row, int currentYear)
    {
        if (row.PastPeakDistanceMeters is not { } distance || row.PastPeakSeconds is not { } seconds)
        {
            return null;
        }

        var peakVdot = Vdot.FromRace(
            distance, Altitude.ToSeaLevel(seconds, row.PastAltitudeOrHome) / 60.0);

        if (row is { BirthYear: { } birthYear, PastPeakYear: { } peakYear })
        {
            return Retraining.AgeAdjustedPeak(
                peakVdot,
                ageAtPeak: Math.Max(0, peakYear - birthYear),
                ageNow: Math.Max(peakYear - birthYear, currentYear - birthYear));
        }

        return peakVdot;
    }

    internal static double? RunGoalDistanceMeters(string metric) => metric switch
    {
        "run-1.5mi" => 1.5 * Vdot.MileMeters,
        "run-2mi" => 2 * Vdot.MileMeters,
        "run-5mi" => 5 * Vdot.MileMeters,
        "run-10mi" => 10 * Vdot.MileMeters,
        _ => null
    };

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
            row.VdotMeasuredOn?.ToString("yyyy-MM-dd", null),
            null,
            row.BirthYear,
            row.PastPeakDistanceMeters,
            row.PastPeakSeconds,
            row.PastPeakYear,
            row.HomeAltitudeMeters,
            row.PastAltitudeMeters);
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
                week.ToString("yyyy-MM-dd", null),
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
                g.Key.Date.ToString("yyyy-MM-dd", null),
                g.Key.Exercise,
                Math.Round(g.Max(x => OneRepMax.Epley(x.s.WeightKg, x.s.Reps)), 1)))
            .OrderBy(e => e.Date)
            .ToArray();
    }

    private static LocalDate LocalDatePattern(string isoDate)
    {
        var parts = isoDate.Split('-');
        return new LocalDate(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }
}
