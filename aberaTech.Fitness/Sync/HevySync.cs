using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

public sealed record HevySyncOutcome(int Fetched, int Added, string? Error);

/// <summary>One pull from Hevy: every workout, upserted, and the state row updated.</summary>
public sealed class HevySync(
    FitnessDbContext database,
    HevyApiClient hevy,
    PosteriorCache cache,
    IOutputCacheStore pages,
    IClock clock,
    ILogger<HevySync> logger)
{
    public async Task<HevySyncOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var state = await database.SyncStates.SingleOrDefaultAsync(s => s.Source == SyncSchedule.HevySource, cancellationToken);
        if (state is null)
        {
            state = new SyncState { Source = SyncSchedule.HevySource };
            database.SyncStates.Add(state);
        }

        var now = clock.GetCurrentInstant();
        state.LastRunAt = now;

        try
        {
            var activities = await hevy.FetchAllAsync(cancellationToken);
            var outcome = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
            if (outcome.Added > 0) cache.Invalidate();

            // Wider than the posterior: a workout already stored is rewritten
            // with whatever Hevy says now, and the cached pages read its sets.
            await pages.EvictFitnessAsync();

            state.LastSuccessAt = now;
            state.LastOutcome = $"{activities.Count} workouts, {outcome.Added} new";
            await database.SaveChangesAsync(cancellationToken);
            return new HevySyncOutcome(activities.Count, outcome.Added, null);
        }
        catch (Exception exception) when (SyncFailure.Counts(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Hevy sync failed.");
            var outcome = SyncFailure.Describe("Hevy", exception);
            await SyncFailure.RecordAsync(database, SyncSchedule.HevySource, now, outcome, cancellationToken);
            return new HevySyncOutcome(0, 0, outcome);
        }
    }
}
