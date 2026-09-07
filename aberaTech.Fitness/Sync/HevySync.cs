using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

public sealed record HevySyncOutcome(int Fetched, int Added, string? Error);

/// <summary>One pull from Hevy: every workout, upserted, and the state row updated.</summary>
public sealed class HevySync(
    FitnessDbContext database,
    HevyApiClient hevy,
    PosteriorCache cache,
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

        state.LastRunAt = clock.GetCurrentInstant();

        try
        {
            var activities = await hevy.FetchAllAsync(cancellationToken);
            var outcome = await ActivityStore.UpsertAsync(database, activities, cancellationToken);
            if (outcome.Added > 0) cache.Invalidate();

            state.LastOutcome = $"{activities.Count} workouts, {outcome.Added} new";
            await database.SaveChangesAsync(cancellationToken);
            return new HevySyncOutcome(activities.Count, outcome.Added, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Hevy sync failed.");
            state.LastOutcome = "Hevy did not answer; will try again.";
            await database.SaveChangesAsync(cancellationToken);
            return new HevySyncOutcome(0, 0, state.LastOutcome);
        }
    }
}
