using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Data;

/// <summary>What one import did.</summary>
/// <param name="Added">Rows that did not exist before.</param>
/// <param name="Skipped">Rows the export already describes better, so not stored.</param>
/// <param name="Superseded">Rows replaced by an export's account of the same session.</param>
public sealed record UpsertOutcome(int Added, int Skipped, int Superseded);

/// <summary>Idempotent writes: the same external record always lands on the same row.</summary>
public static class ActivityStore
{
    /// <summary>
    /// Upserts by (Source, ExternalId). Existing rows are replaced wholesale —
    /// the source of truth is the source system, not whatever this database had
    /// before.
    ///
    /// Two files from the same service can describe one session under different
    /// identifiers, which is why this is not only a keyed upsert: an export's
    /// account of a run, which carries a real clock, replaces the same run read
    /// from a CSV that carries only a wall clock, and a CSV row is not stored
    /// at all when the export already covers it. Without that, uploading both
    /// of the files Garmin offers doubles the weekly volume chart — quietly,
    /// and in the direction that flatters.
    /// </summary>
    /// <remarks>
    /// Two queries for the whole batch, not two per row. A Garmin export is a
    /// few thousand activities (see <c>Import.MaxEntries</c>), and asking the
    /// database once per activity made an import cost four statements each: a
    /// window query for the same-session rule and a three-statement split query
    /// for the keyed lookup. Twelve thousand round trips to a managed Postgres
    /// is minutes of latency inside one HTTP request. Both sets are bounded by
    /// the batch, so both are read up front and matched in memory.
    ///
    /// The candidate list is held sorted by start time and searched by
    /// bisection rather than scanned, so the cost does not grow with the length
    /// of the log already stored.
    /// </remarks>
    public static async Task<UpsertOutcome> UpsertAsync(
        FitnessDbContext database,
        IReadOnlyList<Activity> incoming,
        CancellationToken cancellationToken)
    {
        if (incoming.Count == 0) return new UpsertOutcome(0, 0, 0);

        var existingByKey = await KeyedAsync(database, incoming, cancellationToken);
        var candidates = await NearbyAsync(database, incoming, cancellationToken);

        var added = 0;
        var skipped = 0;
        var superseded = 0;

        // A row already dropped for one incoming activity must not be counted
        // again for the next: the delete is not saved until the end, so the
        // in-memory candidate list still holds it.
        var removed = new HashSet<Guid>();

        foreach (var activity in incoming)
        {
            if (SameSession.LocalClock.Contains(activity.Source)
                && candidates.Matching(activity, SameSession.TrueClock).Any())
            {
                skipped++;
                continue;
            }

            if (SameSession.TrueClock.Contains(activity.Source))
            {
                foreach (var stale in candidates.Matching(activity, SameSession.LocalClock))
                {
                    if (!removed.Add(stale.Id)) continue;
                    database.Activities.Remove(stale);
                    superseded++;
                }
            }

            var existing = activity.ExternalId is null
                ? null
                : existingByKey.GetValueOrDefault((activity.Source, activity.ExternalId));

            if (existing is null)
            {
                database.Activities.Add(activity);
                added++;

                // So a batch carrying the same external id twice updates the
                // row it just added rather than inserting a duplicate and
                // losing the whole import to the unique index.
                if (activity.ExternalId is { } key)
                {
                    existingByKey[(activity.Source, key)] = activity;
                }
            }
            else
            {
                Overwrite(existing, activity);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        return new UpsertOutcome(added, skipped, superseded);
    }

    /// <summary>Copy an incoming record over the row it identifies.</summary>
    private static void Overwrite(Activity existing, Activity activity)
    {
        existing.StartedAt = activity.StartedAt;
        existing.Sport = activity.Sport;
        existing.Name = activity.Name;
        existing.DistanceMeters = activity.DistanceMeters;
        existing.DurationSeconds = activity.DurationSeconds;
        existing.AverageHr = activity.AverageHr;
        existing.MaxHr = activity.MaxHr;

        // The load is the one field the source never knew. A file that names it
        // may set it; a file that does not must not erase what the owner typed
        // on the page.
        existing.LoadKg = activity.LoadKg ?? existing.LoadKg;
        existing.Indoor = activity.Indoor ?? existing.Indoor;

        // Laps travel with the record that carries them; a file without laps
        // must not erase the ones a richer file brought.
        if (activity.Laps.Count > 0)
        {
            existing.Laps.Clear();
            foreach (var lap in activity.Laps)
            {
                existing.Laps.Add(new Lap
                {
                    Id = Guid.NewGuid(),
                    ActivityId = existing.Id,
                    Index = lap.Index,
                    DistanceMeters = lap.DistanceMeters,
                    Seconds = lap.Seconds,
                    AverageHr = lap.AverageHr
                });
            }
        }

        existing.Sets.Clear();
        foreach (var set in activity.Sets)
        {
            existing.Sets.Add(new StrengthSet
            {
                Id = Guid.NewGuid(),
                ActivityId = existing.Id,
                Exercise = set.Exercise,
                SetIndex = set.SetIndex,
                WeightKg = set.WeightKg,
                Reps = set.Reps,
                DurationSeconds = set.DurationSeconds,
                DistanceMeters = set.DistanceMeters
            });
        }
    }

    /// <summary>
    /// Every stored row the batch identifies by (Source, ExternalId), tracked,
    /// because the ones that match are about to be rewritten.
    /// </summary>
    /// <remarks>
    /// Matched on the two key columns as a pair would not translate to SQL, so
    /// the sources and the identifiers go down separately and the pairing is
    /// re-applied here. That over-fetches only when one import carries two
    /// sources, and never fetches a row outside the batch's identifiers.
    ///
    /// Two collections in one statement multiply: every set comes back once per
    /// lap. Split, so they do not.
    /// </remarks>
    private static async Task<Dictionary<(string Source, string ExternalId), Activity>> KeyedAsync(
        FitnessDbContext database,
        IReadOnlyList<Activity> incoming,
        CancellationToken cancellationToken)
    {
        var wanted = incoming
            .Where(a => a.ExternalId is not null)
            .Select(a => (a.Source, ExternalId: a.ExternalId!))
            .ToHashSet();

        if (wanted.Count == 0) return new Dictionary<(string, string), Activity>();

        var sources = wanted.Select(k => k.Source).Distinct().ToArray();
        var identifiers = wanted.Select(k => k.ExternalId).Distinct().ToArray();

        var rows = await database.Activities
            .Include(a => a.Sets)
            .Include(a => a.Laps)
            .AsSplitQuery()
            .Where(a => a.ExternalId != null
                        && sources.Contains(a.Source)
                        && identifiers.Contains(a.ExternalId!))
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => wanted.Contains((row.Source, row.ExternalId!)))
            .ToDictionary(row => (row.Source, row.ExternalId!));
    }

    /// <summary>
    /// Everything from the same-session sources close enough in time to any of
    /// the batch to be worth comparing in full.
    /// </summary>
    /// <remarks>
    /// The window does the coarse work in the database so the exact rule stays
    /// in one readable place. One window covers the whole batch, which is why
    /// the fine filtering happens in <see cref="Candidates"/>.
    /// </remarks>
    private static async Task<Candidates> NearbyAsync(
        FitnessDbContext database,
        IReadOnlyList<Activity> incoming,
        CancellationToken cancellationToken)
    {
        string[] sources = [.. SameSession.TrueClock, .. SameSession.LocalClock];

        var from = incoming.Min(a => a.StartedAt) - SameSession.ClockTolerance;
        var to = incoming.Max(a => a.StartedAt) + SameSession.ClockTolerance;

        var rows = await database.Activities
            .Where(a => sources.Contains(a.Source) && a.StartedAt >= from && a.StartedAt <= to)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);

        return new Candidates(rows);
    }

    /// <summary>
    /// The stored rows a batch might collide with, searched by start time.
    /// </summary>
    /// <remarks>
    /// Held sorted so the window around one incoming activity is found by
    /// bisection. Scanning instead would make an import cost the size of the
    /// batch times the size of the log, which is the shape the round trips were
    /// removed to avoid.
    /// </remarks>
    private sealed class Candidates(IReadOnlyList<Activity> ordered)
    {
        private readonly long[] _starts = [.. ordered.Select(a => a.StartedAt.ToUnixTimeTicks())];

        /// <summary>Rows from the given sources that describe the same session.</summary>
        public IEnumerable<Activity> Matching(Activity activity, string[] sources)
        {
            var tolerance = SameSession.ClockTolerance.BclCompatibleTicks;
            var start = activity.StartedAt.ToUnixTimeTicks();
            var last = start + tolerance;

            for (var i = LowerBound(start - tolerance); i < ordered.Count; i++)
            {
                if (_starts[i] > last) yield break;
                if (!sources.Contains(ordered[i].Source)) continue;
                if (SameSession.Matches(ordered[i], activity)) yield return ordered[i];
            }
        }

        /// <summary>The first index whose start is at or after <paramref name="ticks"/>.</summary>
        private int LowerBound(long ticks)
        {
            var low = 0;
            var high = _starts.Length;

            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (_starts[middle] < ticks) low = middle + 1;
                else high = middle;
            }

            return low;
        }
    }
}
