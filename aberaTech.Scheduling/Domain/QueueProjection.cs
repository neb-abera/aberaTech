using NodaTime;

namespace aberaTech.Scheduling.Domain;

public enum QueueEntryState
{
    /// <summary>Waiting to be called.</summary>
    Waiting = 0,

    /// <summary>Currently with the host.</summary>
    Serving = 1,

    /// <summary>Finished, whether early, late or on time.</summary>
    Done = 2,

    /// <summary>Withdrawn by the visitor or removed by the host.</summary>
    Cancelled = 3,

    /// <summary>Called, but did not appear.</summary>
    NoShow = 4
}

/// <summary>One person in the queue.</summary>
/// <param name="Expected">
/// How long the host expects to spend with them. An estimate, not a promise:
/// the projection uses it only to look forward, and a real start time always
/// overrides it once known.
/// </param>
public sealed record QueueEntry(
    Guid Id,
    int Position,
    Duration Expected,
    QueueEntryState State,
    Instant? StartedAt = null);

/// <summary>What the queue currently expects to happen, and when.</summary>
/// <param name="BeyondClose">
/// True when this person is not expected to be reached before the host stops.
/// Surfaced rather than hidden: somebody waiting in a corridor deserves to be
/// told they are not going to be seen, and telling them is the entire
/// difference between a queue and a trap.
/// </param>
public sealed record ProjectedEntry(Guid Id, int Position, Instant ProjectedStart, bool BeyondClose = false)
{
    public Duration WaitFrom(Instant now) => ProjectedStart <= now ? Duration.Zero : ProjectedStart - now;
}

/// <summary>
/// Projects when each waiting person will actually be seen.
/// </summary>
/// <remarks>
/// This is the piece that off-the-shelf booking tools bolt on and get wrong.
/// The queue is a stream, not a grid of slots: when somebody finishes early or
/// drops out, everybody behind them moves up, and the only honest answer to
/// "when am I on?" is recomputed from the front of the line every time
/// something changes.
///
/// Pure and total. Given the same queue it returns the same projection, which
/// is what lets the notification layer compare a fresh projection against the
/// last one it told somebody about.
/// </remarks>
public static class QueueProjection
{
    /// <summary>
    /// Projects start times for everyone still waiting, in queue order.
    /// </summary>
    /// <param name="entries">
    /// The whole queue in any order. Cancelled, done and no-show entries are
    /// ignored, which is precisely what makes the line move up when one of them
    /// leaves.
    /// </param>
    /// <param name="now">
    /// The current instant. Also the floor for every projection: a queue that
    /// is running late must not claim somebody was seen in the past.
    /// </param>
    /// <param name="busy">
    /// Periods the host is already committed elsewhere. The line does not run
    /// through them: a queue that projects somebody into a meeting already on
    /// the calendar will text them to arrive during it.
    /// </param>
    /// <param name="closesAt">
    /// When the host stops. Entries projected past it are flagged rather than
    /// dropped, because they are still in the queue and still need an answer.
    /// </param>
    public static IReadOnlyList<ProjectedEntry> Project(
        IEnumerable<QueueEntry> entries,
        Instant now,
        IReadOnlyList<Interval>? busy = null,
        Instant? closesAt = null)
    {
        var ordered = entries
            .Where(entry => entry.State is QueueEntryState.Waiting or QueueEntryState.Serving)
            .OrderBy(entry => entry.Position)
            .ToList();

        var projections = new List<ProjectedEntry>(ordered.Count);

        // Coalesced once rather than per entry: overlapping blocks would
        // otherwise make the skip below depend on which fragment it met first.
        var blocked = busy is null or { Count: 0 }
            ? []
            : Calendar.BusyMerge.Coalesce(busy);

        // The front of the line anchors everything behind it. If somebody is
        // already being seen, the next person waits out the remainder of their
        // expected time — measured from when the appointment actually began,
        // not from now, so an appointment that has already overrun stops
        // pushing the estimate further into the future on every recalculation.
        var cursor = now;

        foreach (var entry in ordered)
        {
            if (entry.State == QueueEntryState.Serving)
            {
                var startedAt = entry.StartedAt ?? now;
                var expectedEnd = startedAt + entry.Expected;

                // Never project into the past: an overrunning appointment ends
                // no earlier than now, whatever the estimate said.
                cursor = expectedEnd > now ? expectedEnd : now;
                continue;
            }

            var start = SkipBusy(cursor, entry.Expected, blocked);

            projections.Add(new ProjectedEntry(
                entry.Id,
                entry.Position,
                start,
                closesAt is { } close && start >= close));

            cursor = start + entry.Expected;
        }

        return projections;
    }

    /// <summary>
    /// Moves a start time forward until the whole appointment fits in free time.
    /// </summary>
    /// <remarks>
    /// Repeated rather than done once, because clearing one commitment can land
    /// on the next: a fifteen minute conversation pushed past a meeting that
    /// ends at two may still collide with another starting at ten past.
    /// </remarks>
    private static Instant SkipBusy(Instant from, Duration length, IReadOnlyList<Interval> busy)
    {
        var start = from;
        var moved = true;

        while (moved)
        {
            moved = false;

            foreach (var block in busy)
            {
                if (start < block.End && block.Start < start + length)
                {
                    start = block.End;
                    moved = true;
                }
            }
        }

        return start;
    }
}
