using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>Normalising overlapping busy periods into a tidy, sorted set.</summary>
public static class BusyMerge
{
    /// <summary>
    /// Coalesces overlapping and touching intervals.
    /// </summary>
    /// <remarks>
    /// Two sources routinely describe the same hour — an appointment booked here
    /// is usually also an event on the host's calendar — and a slot checked
    /// against a hundred overlapping fragments does a hundred comparisons to
    /// reach an answer three would have given. Merging first makes the planner's
    /// work proportional to how busy the host actually is rather than to how
    /// many places said so.
    /// </remarks>
    public static IReadOnlyList<Interval> Coalesce(IEnumerable<Interval> intervals)
    {
        var ordered = intervals
            .Where(interval => interval.End > interval.Start)
            .OrderBy(interval => interval.Start)
            .ToList();

        if (ordered.Count == 0)
        {
            return [];
        }

        var merged = new List<Interval>();
        var current = ordered[0];

        foreach (var next in ordered.Skip(1))
        {
            // Touching counts as mergeable: 09:00-10:00 and 10:00-11:00 are one
            // continuous busy block, and leaving them apart cannot change any
            // answer but does cost an extra comparison for every slot.
            if (next.Start <= current.End)
            {
                current = new Interval(current.Start, next.End > current.End ? next.End : current.End);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }
}
