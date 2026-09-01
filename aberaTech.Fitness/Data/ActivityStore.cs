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
    public static async Task<UpsertOutcome> UpsertAsync(
        FitnessDbContext database,
        IReadOnlyList<Activity> incoming,
        CancellationToken cancellationToken)
    {
        if (incoming.Count == 0) return new UpsertOutcome(0, 0, 0);

        var added = 0;
        var skipped = 0;
        var superseded = 0;

        foreach (var activity in incoming)
        {
            if (SameSession.LocalClock.Contains(activity.Source)
                && await CoveredByAnExportAsync(database, activity, cancellationToken))
            {
                skipped++;
                continue;
            }

            if (SameSession.TrueClock.Contains(activity.Source))
            {
                superseded += await RemoveGuessedClockCopiesAsync(database, activity, cancellationToken);
            }

            var existing = activity.ExternalId is null
                ? null
                : await database.Activities
                    .Include(a => a.Sets)
                    .SingleOrDefaultAsync(
                        a => a.Source == activity.Source && a.ExternalId == activity.ExternalId,
                        cancellationToken);

            if (existing is null)
            {
                database.Activities.Add(activity);
                added++;
            }
            else
            {
                existing.StartedAt = activity.StartedAt;
                existing.Sport = activity.Sport;
                existing.Name = activity.Name;
                existing.DistanceMeters = activity.DistanceMeters;
                existing.DurationSeconds = activity.DurationSeconds;
                existing.AverageHr = activity.AverageHr;
                existing.MaxHr = activity.MaxHr;

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
                        Reps = set.Reps
                    });
                }
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        return new UpsertOutcome(added, skipped, superseded);
    }

    /// <summary>Is this wall-clock record a run an export already told us about?</summary>
    private static async Task<bool> CoveredByAnExportAsync(
        FitnessDbContext database,
        Activity activity,
        CancellationToken cancellationToken)
    {
        var candidates = await NearbyAsync(database, activity, SameSession.TrueClock, cancellationToken);
        return candidates.Any(candidate => SameSession.Matches(candidate, activity));
    }

    /// <summary>Drop the wall-clock copies of a run now that its real record has arrived.</summary>
    private static async Task<int> RemoveGuessedClockCopiesAsync(
        FitnessDbContext database,
        Activity activity,
        CancellationToken cancellationToken)
    {
        var candidates = await NearbyAsync(database, activity, SameSession.LocalClock, cancellationToken);
        var stale = candidates.Where(candidate => SameSession.Matches(candidate, activity)).ToList();

        database.Activities.RemoveRange(stale);
        return stale.Count;
    }

    /// <summary>
    /// Everything from the given sources close enough in time to be worth
    /// comparing in full. The window does the coarse work in the database so
    /// the exact rule stays in one readable place.
    /// </summary>
    private static Task<List<Activity>> NearbyAsync(
        FitnessDbContext database,
        Activity activity,
        string[] sources,
        CancellationToken cancellationToken)
    {
        var from = activity.StartedAt - SameSession.ClockTolerance;
        var to = activity.StartedAt + SameSession.ClockTolerance;

        return database.Activities
            .Where(a => sources.Contains(a.Source) && a.StartedAt >= from && a.StartedAt <= to)
            .ToListAsync(cancellationToken);
    }
}
