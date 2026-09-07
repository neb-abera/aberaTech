using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One day's training load, in easy-hour equivalents.</summary>
/// <param name="Impact">Whether the day carried running or rucking — the load a rest day rests from.</param>
public sealed record DailyLoad(LocalDate Date, double Load, bool Impact);

/// <summary>One logged session as the load model sees it.</summary>
public sealed record LoadedSession(LocalDate Date, string Sport, TrainingDose Split);

/// <summary>
/// How the recent load is being carried, not how much of it there is.
/// </summary>
/// <param name="AcuteLoad">The last seven days' load.</param>
/// <param name="ChronicLoad">The average week over the last twenty-eight days.</param>
/// <param name="Acwr">Acute over chronic, when four weeks of log exist.</param>
/// <param name="Monotony">The week's mean daily load over its spread; null when the week was one flat number.</param>
/// <param name="WeeklyStrain">Acute load times monotony.</param>
/// <param name="ImpactStreakDays">Days in a row with running or rucking, counted back from the latest one.</param>
/// <param name="RestDaysLast7">Days in the last seven with no running or rucking.</param>
public sealed record DurabilityReport(
    double AcuteLoad,
    double ChronicLoad,
    double? Acwr,
    double? Monotony,
    double? WeeklyStrain,
    int ImpactStreakDays,
    int RestDaysLast7,
    int DaysOfLog,
    IReadOnlyList<DailyLoad> Days,
    IReadOnlyList<CalculationStep> Steps);

/// <summary>
/// The load the body is carrying, looked at the way injury-risk work looks at
/// it: this week against the weeks behind it, how evenly it was spread, and
/// how long since a day off.
/// </summary>
/// <remarks>
/// Load is the strain the dose model already uses — hours weighted by what
/// each zone costs to recover from (<see cref="TrainingDose.Strain"/>) — so a
/// week of intervals weighs more than the same hours easy, and lifting counts
/// too. Three readings come out of the daily series:
///
/// <b>Acute:chronic workload ratio.</b> The last seven days over the average
/// of the last twenty-eight. Gabbett's work in team sport put the safe band
/// at roughly 0.8–1.3 and found injury risk climbing steeply past 1.5: the
/// spike, not the volume, is what hurts.
///
/// <b>Monotony and strain.</b> Foster's measures: the week's mean daily load
/// over its standard deviation, and that ratio times the week's load. A week
/// that is the same every day — no hard/easy contrast, no day off — scored
/// above about 2.0 preceded illness and overtraining in his athletes even
/// when the total was modest.
///
/// <b>The streak.</b> Days in a row with running or rucking. Lifting is not a
/// rest day for the legs' connective tissue but it is a rest from impact,
/// which is what a selection athlete's tendons and shins need one of.
///
/// Citations: <see cref="Citations.GabbettWorkload"/>,
/// <see cref="Citations.FosterMonotony"/>.
/// </remarks>
public static class Durability
{
    public const double AcwrSweetLow = 0.8;
    public const double AcwrSweetHigh = 1.3;
    public const double AcwrDanger = 1.5;
    public const double MonotonyLimit = 2.0;

    /// <summary>Days of impact in a row past which the streak is called out.</summary>
    public const int StreakLimit = 10;

    /// <summary>Four weeks of log before the ratio means anything.</summary>
    public const int ChronicDays = 28;
    public const int AcuteDays = 7;

    /// <summary>Less running than this in a day is a stride-out, not an impact day.</summary>
    public const double ImpactFloorHours = 10.0 / 60;

    public static DurabilityReport Build(IEnumerable<LoadedSession> sessions, LocalDate today)
    {
        var all = sessions.ToArray();
        var first = all.Length == 0 ? today : all.Min(s => s.Date);
        var daysOfLog = Math.Max(0, Period.Between(first, today, PeriodUnits.Days).Days + 1);

        var window = today.PlusDays(-(ChronicDays - 1));
        var byDay = all
            .Where(s => s.Date >= window && s.Date <= today)
            .GroupBy(s => s.Date)
            .ToDictionary(g => g.Key, g => (Load: g.Sum(s => s.Split.Strain), Impact: g.Where(IsImpact).Sum(s => s.Split.RunningHours)));

        var days = new List<DailyLoad>();
        for (var day = window; day <= today; day = day.PlusDays(1))
        {
            var (load, impact) = byDay.GetValueOrDefault(day);
            days.Add(new DailyLoad(day, load, impact >= ImpactFloorHours));
        }

        var week = days.Skip(ChronicDays - AcuteDays).ToArray();
        var acute = week.Sum(d => d.Load);
        var chronic = days.Sum(d => d.Load) / (ChronicDays / (double)AcuteDays);

        double? acwr = daysOfLog >= ChronicDays && chronic > 0 ? acute / chronic : null;

        var mean = week.Average(d => d.Load);
        var sd = Math.Sqrt(week.Sum(d => (d.Load - mean) * (d.Load - mean)) / week.Length);
        double? monotony = sd > 0 ? mean / sd : null;
        double? strain = monotony is { } m ? acute * m : null;

        var restDays = week.Count(d => !d.Impact);
        var streak = Streak(all, today);

        var steps = new CalculationTrace()
            .Add(
                "Daily load",
                "hours in each zone × that zone's recovery cost (easy 1, threshold 2.5, interval 4.5, strength 1.5), summed per day",
                Text($"{acute:0.0} easy-hour equivalents this week"),
                Citations.CogganPmc.Id)
            .Add(
                "Acute:chronic ratio",
                Text($"{acute:0.0} (last 7 days) ÷ {chronic:0.0} (average week of the last 28)"),
                acwr is { } r ? Text($"{r:0.00} — safe band {AcwrSweetLow:0.0}–{AcwrSweetHigh:0.0}, risk climbs past {AcwrDanger:0.0}") : Text($"needs {ChronicDays} days of log; has {daysOfLog}"),
                Citations.GabbettWorkload.Id)
            .Add(
                "Monotony and strain",
                Text($"mean daily load {mean:0.00} ÷ its spread {sd:0.00}; strain = weekly load × monotony"),
                monotony is { } mo ? Text($"monotony {mo:0.0}, strain {strain:0.0} — over {MonotonyLimit:0.0} the week has no contrast") : "no spread to divide by",
                Citations.FosterMonotony.Id)
            .Add(
                "Impact streak",
                Text($"days in a row with ≥ {ImpactFloorHours * 60:0} min of running or rucking, back from the latest"),
                Text($"{streak} days; {restDays} rest days in the last 7"),
                Citations.FosterMonotony.Id)
            .Steps;

        return new DurabilityReport(acute, chronic, acwr, monotony, strain, streak, restDays, daysOfLog, days, steps);
    }

    /// <summary>What the report says that the athlete should hear.</summary>
    public static IReadOnlyList<Highlight> Highlights(DurabilityReport report)
    {
        var highlights = new List<Highlight>();

        if (report.Acwr is { } acwr)
        {
            if (acwr > AcwrDanger)
            {
                highlights.Add(new Highlight("durability-spike",
                    Text($"This week's load is {acwr:0.0}× the four-week average"),
                    Text($"Acute {report.AcuteLoad:0.0} over chronic {report.ChronicLoad:0.0}; past {AcwrDanger:0.0} is where injury risk climbs steeply. Hold or cut the week rather than build on it."),
                    Positive: false));
            }
            else if (acwr > AcwrSweetHigh)
            {
                highlights.Add(new Highlight("durability-ramp",
                    Text($"Load ramping: {acwr:0.00}× the four-week average"),
                    Text($"Above the {AcwrSweetLow:0.0}–{AcwrSweetHigh:0.0} band. One more step up and it is a spike; a flat week now keeps the chronic load catching up."),
                    Positive: false));
            }
            else if (acwr < AcwrSweetLow && report.ChronicLoad > 0)
            {
                highlights.Add(new Highlight("durability-undercut",
                    Text($"Load {acwr:0.00}× the four-week average"),
                    Text($"Below the {AcwrSweetLow:0.0}–{AcwrSweetHigh:0.0} band. Fine as a planned down week; as a habit it lets the chronic load fall and the next normal week becomes the spike."),
                    Positive: false));
            }
        }

        if (report.Monotony is { } monotony && monotony > MonotonyLimit && report.AcuteLoad > 0)
        {
            highlights.Add(new Highlight("durability-monotony",
                Text($"Monotonous week: every day much the same ({monotony:0.0})"),
                Text($"Foster's monotony over {MonotonyLimit:0.0}: no hard/easy contrast and no day off. Strain {report.WeeklyStrain:0.0}. Make one day clearly harder and one clearly off."),
                Positive: false));
        }

        if (report.ImpactStreakDays >= StreakLimit)
        {
            highlights.Add(new Highlight("durability-streak",
                Text($"{report.ImpactStreakDays} days of running or rucking without a day off"),
                "Connective tissue adapts slower than the engine; the rest day is where it catches up. Take one before it is taken.",
                Positive: false));
        }

        if (highlights.Count == 0 && report.Acwr is { } steady)
        {
            highlights.Add(new Highlight("durability-steady",
                Text($"Load carried well: {steady:0.00}× the four-week average"),
                Text($"Inside the {AcwrSweetLow:0.0}–{AcwrSweetHigh:0.0} band, monotony {(report.Monotony is { } m ? m.ToString("0.0", CultureInfo.InvariantCulture) : "—")}, {report.RestDaysLast7} rest days in the last 7. Keep the build to this shape."),
                Positive: true));
        }

        return highlights;
    }

    /// <summary>Days in a row with impact, counted back from the latest impact day if that was today or yesterday.</summary>
    internal static int Streak(IReadOnlyList<LoadedSession> sessions, LocalDate today)
    {
        var impactDays = sessions
            .Where(IsImpact)
            .GroupBy(s => s.Date)
            .Where(g => g.Sum(s => s.Split.RunningHours) >= ImpactFloorHours)
            .Select(g => g.Key)
            .ToHashSet();

        // A day with nothing logged yet is not a rest day until it is over.
        var day = impactDays.Contains(today) ? today : today.PlusDays(-1);
        var streak = 0;
        while (impactDays.Contains(day))
        {
            streak++;
            day = day.PlusDays(-1);
        }

        return streak;
    }

    private static bool IsImpact(LoadedSession session) => session.Sport is "run" or "ruck";

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
