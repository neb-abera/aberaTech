using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One thing the data says, with its evidence.</summary>
/// <param name="Kind">Stable key: aerobic-gain, aerobic-loss, volume-streak, volume-gap, strength-pr, strength-slide.</param>
/// <param name="Headline">The claim, short enough for a card.</param>
/// <param name="Evidence">The numbers behind it, so the claim is checkable.</param>
/// <param name="Positive">Whether this is progress or a warning.</param>
public sealed record Highlight(string Kind, string Headline, string Evidence, bool Positive);

/// <summary>A week of training volume against the plan.</summary>
public sealed record WeekVolume(LocalDate WeekStart, double Minutes);

/// <summary>A dated one-rep-max estimate for one exercise.</summary>
public sealed record E1RmPoint(LocalDate Date, string Exercise, double E1Rm);

/// <summary>
/// Quality highlights: what moved, which way, with the numbers attached.
/// </summary>
/// <remarks>
/// Rules, not vibes. Each highlight fires on a measurable change and carries
/// its evidence. Negative findings are generated on purpose: a tracker that
/// only congratulates is a toy.
/// </remarks>
public static class Highlights
{
    /// <summary>Fractional pace change below which two months are treated as equal.</summary>
    private const double AerobicNoiseFloor = 0.02;

    /// <summary>
    /// How recent a lift session has to be for its PR or slide to be news.
    /// The digest is weekly, so two weeks covers the week it reports and the
    /// one before. On 2026-11-04 the digest called April's deadlift a new PR,
    /// because no deadlift had been logged since.
    /// </summary>
    public const int RecentDays = 14;

    /// <param name="today">
    /// When given, only what happened lately is reported: a lift session or a
    /// month of runs older than that says nothing new. Tests of the rules
    /// alone leave it out.
    /// </param>
    public static IReadOnlyList<Highlight> Build(
        IReadOnlyList<MonthlyAerobicPoint> aerobicTrend,
        IReadOnlyList<WeekVolume> recentWeeks,
        double planMinutesPerWeek,
        IReadOnlyList<E1RmPoint> e1RmHistory,
        LocalDate? today = null)
    {
        var highlights = new List<Highlight>();

        if (today is { } day)
        {
            // A month counts while it is this month or last month.
            var lastMonth = day.PlusMonths(-1);
            if (aerobicTrend.Count > 0
                && !((aerobicTrend[^1].Year == day.Year && aerobicTrend[^1].Month == day.Month)
                     || (aerobicTrend[^1].Year == lastMonth.Year && aerobicTrend[^1].Month == lastMonth.Month)))
            {
                aerobicTrend = [];
            }

            e1RmHistory = e1RmHistory
                .GroupBy(p => p.Exercise)
                .Where(g => g.Max(p => p.Date) >= day.PlusDays(-RecentDays))
                .SelectMany(g => g)
                .ToArray();

            recentWeeks = CompletedWeeks(recentWeeks, day);
        }

        AddAerobic(highlights, aerobicTrend);
        AddVolume(highlights, recentWeeks, planMinutesPerWeek);
        AddStrength(highlights, e1RmHistory);

        return highlights;
    }

    /// <summary>
    /// The weeks up to last week, with a week nothing was logged in counted
    /// as zero. The weekly series stops at the last week with an activity,
    /// so without this a streak or gap from months ago reads as current.
    /// </summary>
    private static IReadOnlyList<WeekVolume> CompletedWeeks(IReadOnlyList<WeekVolume> weeks, LocalDate today)
    {
        var thisWeek = today.PlusDays(-(((int)today.DayOfWeek - 1 + 7) % 7));
        var done = weeks.Where(w => w.WeekStart < thisWeek).ToList();
        if (done.Count == 0) return done;
        for (var week = done[^1].WeekStart.PlusDays(7); week < thisWeek; week = week.PlusDays(7))
        {
            done.Add(new WeekVolume(week, 0));
        }

        return done;
    }

    private static void AddAerobic(List<Highlight> highlights, IReadOnlyList<MonthlyAerobicPoint> trend)
    {
        // Compare the two most recent months that actually contain runs.
        if (trend.Count < 2) return;

        var last = trend[^1];
        var previous = trend[^2];
        var change = (previous.MedianNormalizedSecPerKm - last.MedianNormalizedSecPerKm)
                     / previous.MedianNormalizedSecPerKm;

        if (Math.Abs(change) < AerobicNoiseFloor) return;

        var evidence =
            $"Median HR-normalized pace {Pace(previous.MedianNormalizedSecPerKm)}/km → " +
            $"{Pace(last.MedianNormalizedSecPerKm)}/km ({previous.RunCount} → {last.RunCount} runs).";

        // A treadmill month's pace is the belt's reading; say so next to the number.
        if (last.IndoorRuns * 2 > last.RunCount)
        {
            evidence += $" {last.IndoorRuns} of the latest month's runs were on a treadmill, whose distance is an estimate.";
        }

        highlights.Add(change > 0
            ? new Highlight("aerobic-gain",
                $"Aerobic base up {Format.Percent(change)} month over month", evidence, Positive: true)
            : new Highlight("aerobic-loss",
                $"Aerobic base down {Format.Percent(Math.Abs(change))} month over month", evidence, Positive: false));
    }

    private static void AddVolume(List<Highlight> highlights, IReadOnlyList<WeekVolume> weeks, double planMinutes)
    {
        if (weeks.Count == 0 || planMinutes <= 0) return;

        var hitStreak = weeks.Reverse().TakeWhile(w => w.Minutes >= planMinutes).Count();
        var zeroStreak = weeks.Reverse().TakeWhile(w => w.Minutes == 0).Count();

        if (hitStreak >= 2)
        {
            highlights.Add(new Highlight("volume-streak",
                $"{hitStreak} straight weeks at or above plan volume",
                $"Plan {planMinutes:0} min/week; last {hitStreak} weeks all met it.",
                Positive: true));
        }
        else if (zeroStreak >= 2)
        {
            highlights.Add(new Highlight("volume-gap",
                $"{zeroStreak} straight weeks with no training logged",
                $"Plan {planMinutes:0} min/week; the trajectory model assumes the dose actually happens.",
                Positive: false));
        }
        else
        {
            var recent = weeks[^1];
            var share = recent.Minutes / planMinutes;
            if (share < 0.5)
            {
                highlights.Add(new Highlight("volume-gap",
                    $"Last week was {Format.Percent(share)} of plan volume",
                    $"{recent.Minutes:0} of {planMinutes:0} planned minutes in the week of {recent.WeekStart:yyyy-MM-dd}.",
                    Positive: false));
            }
        }
    }

    private static void AddStrength(List<Highlight> highlights, IReadOnlyList<E1RmPoint> history)
    {
        foreach (var exercise in history.GroupBy(p => p.Exercise))
        {
            var ordered = exercise.OrderBy(p => p.Date).ToArray();
            if (ordered.Length < 2) continue;

            var latest = ordered[^1];
            var bestBefore = ordered[..^1].Max(p => p.E1Rm);

            if (latest.E1Rm > bestBefore)
            {
                highlights.Add(new Highlight("strength-pr",
                    $"{exercise.Key}: new estimated 1RM {latest.E1Rm * BodyMass.PoundsPerKg:0} lb",
                    $"Previous best {bestBefore * BodyMass.PoundsPerKg:0} lb; Epley estimate from {latest.Date:yyyy-MM-dd}.",
                    Positive: true));
            }
            else if (latest.E1Rm < bestBefore * 0.93)
            {
                highlights.Add(new Highlight("strength-slide",
                    $"{exercise.Key}: estimated 1RM down {Format.Percent(1 - latest.E1Rm / bestBefore)} from peak",
                    $"Peak {bestBefore * BodyMass.PoundsPerKg:0} lb, latest {latest.E1Rm * BodyMass.PoundsPerKg:0} lb on {latest.Date:yyyy-MM-dd}. Strength holds on ~2 sessions/week.",
                    Positive: false));
            }
        }
    }

    /// <summary>Days after which a time trial no longer says where the athlete is.</summary>
    public const int AnchorStaleAfterDays = 90;

    /// <summary>
    /// Whether the anchor every projection launches from is worth launching from.
    /// </summary>
    /// <remarks>
    /// The console projected for weeks from a VDOT that was the default, and
    /// said so only in a caption nobody read. A model is only as good as its
    /// last measured anchor, so an unmeasured or stale one is a finding on the
    /// dashboard, next to the trends it is quietly distorting.
    /// </remarks>
    public static Highlight? Anchor(LocalDate? measuredOn, LocalDate today)
    {
        if (measuredOn is not { } measured)
        {
            return new Highlight("anchor-missing",
                "Your anchor is the default, not a measurement",
                "Every projection launches from the default VDOT because no race, time trial, or AFT is recorded. Run a time trial at any distance and name the run for it, such as \"5 mile time trial\", or enter a race on the Log tab.",
                Positive: false);
        }

        var age = Period.Between(measured, today, PeriodUnits.Days).Days;
        if (age <= AnchorStaleAfterDays) return null;

        return new Highlight("anchor-stale",
            $"Your anchor is {age} days old",
            $"Your anchor test was on {measured:yyyy-MM-dd}. After {AnchorStaleAfterDays} days of training it no longer says where you are. Run a new race or time trial.",
            Positive: false);
    }

    private static string Pace(double secPerKm)
    {
        var s = (int)Math.Round(secPerKm);
        return $"{s / 60}:{s % 60:00}";
    }
}
