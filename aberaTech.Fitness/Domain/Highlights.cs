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
/// Rules, not vibes — each highlight fires on a measurable change and carries
/// its evidence. Negative findings are generated on purpose: a tracker that
/// only congratulates is a toy.
/// </remarks>
public static class Highlights
{
    /// <summary>Fractional pace change below which two months are treated as equal.</summary>
    private const double AerobicNoiseFloor = 0.02;

    public static IReadOnlyList<Highlight> Build(
        IReadOnlyList<MonthlyAerobicPoint> aerobicTrend,
        IReadOnlyList<WeekVolume> recentWeeks,
        double planMinutesPerWeek,
        IReadOnlyList<E1RmPoint> e1RmHistory)
    {
        var highlights = new List<Highlight>();

        AddAerobic(highlights, aerobicTrend);
        AddVolume(highlights, recentWeeks, planMinutesPerWeek);
        AddStrength(highlights, e1RmHistory);

        return highlights;
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

        highlights.Add(change > 0
            ? new Highlight("aerobic-gain",
                $"Aerobic base up {change:P0} month over month", evidence, Positive: true)
            : new Highlight("aerobic-loss",
                $"Aerobic base down {Math.Abs(change):P0} month over month", evidence, Positive: false));
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
                    $"Last week was {share:P0} of plan volume",
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
                    $"{exercise.Key}: new estimated 1RM {latest.E1Rm:0}",
                    $"Previous best {bestBefore:0}; Epley estimate from {latest.Date:yyyy-MM-dd}.",
                    Positive: true));
            }
            else if (latest.E1Rm < bestBefore * 0.93)
            {
                highlights.Add(new Highlight("strength-slide",
                    $"{exercise.Key}: estimated 1RM down {1 - latest.E1Rm / bestBefore:P0} from peak",
                    $"Peak {bestBefore:0}, latest {latest.E1Rm:0} on {latest.Date:yyyy-MM-dd}. Strength holds on ~2 sessions/week.",
                    Positive: false));
            }
        }
    }

    private static string Pace(double secPerKm)
    {
        var s = (int)Math.Round(secPerKm);
        return $"{s / 60}:{s % 60:00}";
    }
}
