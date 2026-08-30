using Microsoft.EntityFrameworkCore;

namespace aberaTech.Fitness.Data;

/// <summary>Idempotent writes: the same external record always lands on the same row.</summary>
public static class ActivityStore
{
    /// <summary>
    /// Upserts by (Source, ExternalId); returns how many were new. Existing
    /// rows are replaced wholesale — the source of truth is the source system,
    /// not whatever this database had before.
    /// </summary>
    public static async Task<int> UpsertAsync(
        FitnessDbContext database,
        IReadOnlyList<Activity> incoming,
        CancellationToken cancellationToken)
    {
        if (incoming.Count == 0) return 0;

        var added = 0;

        foreach (var activity in incoming)
        {
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
        return added;
    }
}
