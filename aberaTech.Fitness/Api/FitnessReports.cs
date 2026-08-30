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
    double? CurrentWeightKg);

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

public sealed record PredictionDto(
    double EffectiveHours,
    double Ceiling,
    double WeightAdjustedStartVdot,
    IReadOnlyList<ProjectionPointDto> Curve,
    IReadOnlyList<RaceTimesDto> Checkpoints,
    IReadOnlyList<GoalOutlookDto> Goals,
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

        var endurance = await database.Activities
            .Where(a => (a.Sport == "run" || a.Sport == "ruck") && a.DistanceMeters != null && a.AverageHr != null)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);

        var zone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
        var steadyRuns = endurance
            .Where(a => a.Sport == "run")
            .Select(a => new SteadyRun(
                a.StartedAt.InZone(zone).Date,
                a.DistanceMeters!.Value,
                a.DurationSeconds,
                a.AverageHr!.Value))
            .ToArray();

        var trend = AerobicAnalysis.MonthlyTrend(steadyRuns, settings.ReferenceHr);

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
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(database, cancellationToken);
        var currentWeight = (await database.BodyMetrics.OrderByDescending(m => m.Date)
            .FirstOrDefaultAsync(cancellationToken))?.WeightKg;

        var assumptions = new List<string>
        {
            "Trajectory: V(t) = C − (C − V0)·e^(−kt), the constant-dose solution of the Banister impulse-response family [banister-impulse-response].",
            $"Ceiling C = 38 + 1.6 × effective weekly hours, calibrated to documented aerobic-deficiency recoveries [uphill-athlete-aet, seiler-polarized].",
            "Race equivalencies via Daniels VDOT [daniels-vdot].",
        };

        var startVdot = settings.StartVdot;
        if (targetWeightKg is { } target && currentWeight is { } current && Math.Abs(target - current) > 0.01)
        {
            startVdot = Domain.BodyMass.AdjustVdot(startVdot, current, target);
            assumptions.Add(
                $"Bodyweight {current:0.0} → {target:0.0} kg scales VDOT by mass ratio (fat-mass change, absolute VO2 preserved; clamped to ±10%) [cureton-added-mass]. Assumed reached by the horizon.");
        }

        var p = new TrajectoryParameters(startVdot);
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
                    Vdot.MinutesFor(2 * Vdot.MileMeters, v) * 60,
                    Vdot.MinutesFor(5 * Vdot.MileMeters, v) * 60,
                    Vdot.MinutesFor(1.5 * Vdot.MileMeters, v) * 60);
            })
            .ToArray();

        var goals = new List<GoalOutlookDto>();
        foreach (var goal in await database.Goals.OrderBy(g => g.Metric).ToListAsync(cancellationToken))
        {
            if (RunGoalDistanceMeters(goal.Metric) is not { } distance) continue;

            var targetVdot = Vdot.FromRace(distance, goal.TargetValue / 60.0);
            var months = Trajectory.MonthsToReach(p, effective, targetVdot);
            goals.Add(new GoalOutlookDto(
                goal.Metric, goal.TargetValue, targetVdot, goal.TargetDate.ToString("yyyy-MM-dd", null),
                months, months is not null));
        }

        return new PredictionDto(effective, ceiling, startVdot, curve, checkpoints, goals, assumptions);
    }

    public static RequiredDoseDto RequiredDose(
        double startVdot,
        double distanceMeters,
        double targetSeconds,
        double monthsAvailable,
        double compliance)
    {
        var targetVdot = Vdot.FromRace(distanceMeters, targetSeconds / 60.0);
        var p = new TrajectoryParameters(startVdot);
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

    internal static double? RunGoalDistanceMeters(string metric) => metric switch
    {
        "run-1.5mi" => 1.5 * Vdot.MileMeters,
        "run-2mi" => 2 * Vdot.MileMeters,
        "run-5mi" => 5 * Vdot.MileMeters,
        "run-10mi" => 10 * Vdot.MileMeters,
        _ => null
    };

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
            null);
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
