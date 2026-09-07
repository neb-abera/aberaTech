using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One logged set of a bodyweight movement.</summary>
/// <param name="Date">The day it was done.</param>
/// <param name="Exercise">The name the logging app gave it.</param>
/// <param name="Reps">Repetitions, for counted movements.</param>
/// <param name="DurationSeconds">Hold time, for timed movements such as the plank.</param>
/// <param name="AddedKg">Weight added beyond bodyweight; a weighted pull-up is not a pull-up count.</param>
public sealed record BodyweightSet(
    LocalDate Date,
    string Exercise,
    int Reps,
    double? DurationSeconds = null,
    double AddedKg = 0);

/// <summary>The best of one movement on one day.</summary>
/// <param name="Date">The day.</param>
/// <param name="Metric">The readiness metric the movement scores: pull-ups, hand-release-push-ups, push-ups or plank.</param>
/// <param name="Value">Repetitions, or seconds for the plank.</param>
public sealed record BestSetPoint(LocalDate Date, string Metric, double Value);

/// <summary>
/// The calisthenics every selection gate counts, read out of the strength log.
/// </summary>
/// <remarks>
/// A strength log records bodyweight sets at zero weight, which is exactly the
/// filter the one-rep-max trend applies to leave them out. Selection does not
/// leave them out: the day-one assessment is push-ups, pull-ups and a run, and
/// the sets are already in the log. So they are read by name — whatever the
/// app called the movement — into the four metrics the gates ask for.
/// </remarks>
public static class Calisthenics
{
    public const string PullUps = "pull-ups";
    public const string HandReleasePushUps = "hand-release-push-ups";
    public const string PushUps = "push-ups";
    public const string SitUps = "sit-ups";
    public const string Plank = "plank";

    /// <summary>The metric a logged movement counts towards, or null when it is not one the gates score.</summary>
    public static string? Classify(string exercise)
    {
        var name = exercise.Trim().ToLowerInvariant();
        if (name.Length == 0) return null;

        if (name.Contains("plank"))
        {
            // A side plank or a Copenhagen plank is a different hold; the
            // test counts the front plank only.
            return name.Contains("side") || name.Contains("copenhagen") ? null : Plank;
        }

        var pullUp = name.Contains("pull up") || name.Contains("pull-up") || name.Contains("pullup")
                     || name.Contains("chin up") || name.Contains("chin-up") || name.Contains("chinup");
        if (pullUp)
        {
            // A lat pulldown is a machine, not a pull-up.
            return name.Contains("pulldown") || name.Contains("pull down") ? null : PullUps;
        }

        var handRelease = name.Contains("hand release") || name.Contains("hand-release")
                          || name == "hrp" || name.StartsWith("hrp ") || name.EndsWith(" hrp") || name.Contains(" hrp ");
        if (handRelease) return HandReleasePushUps;

        var pushUp = name.Contains("push up") || name.Contains("push-up") || name.Contains("pushup");
        if (pushUp) return PushUps;

        if (name.Contains("sit up") || name.Contains("sit-up") || name.Contains("situp"))
        {
            return SitUps;
        }

        return null;
    }

    /// <summary>The best set of each scored movement on each day it was logged.</summary>
    public static IReadOnlyList<BestSetPoint> BestPerDay(IEnumerable<BodyweightSet> sets)
    {
        return sets
            .Where(s => s.AddedKg <= 0)
            .Select(s => (Set: s, Metric: Classify(s.Exercise)))
            .Where(x => x.Metric is not null)
            .Select(x => new BestSetPoint(
                x.Set.Date,
                x.Metric!,
                x.Metric == Plank ? x.Set.DurationSeconds ?? 0 : x.Set.Reps))
            .Where(p => p.Value > 0)
            .GroupBy(p => (p.Date, p.Metric))
            .Select(g => new BestSetPoint(g.Key.Date, g.Key.Metric, g.Max(p => p.Value)))
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Metric)
            .ToArray();
    }

    /// <summary>The best of one metric on or after <paramref name="since"/>, if it was logged at all.</summary>
    public static BestSetPoint? Best(IEnumerable<BestSetPoint> points, string metric, LocalDate since)
    {
        return points
            .Where(p => p.Metric == metric && p.Date >= since)
            .OrderByDescending(p => p.Value)
            .ThenByDescending(p => p.Date)
            .FirstOrDefault();
    }
}
