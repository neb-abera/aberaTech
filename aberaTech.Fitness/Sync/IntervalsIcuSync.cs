using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.IntervalsIcu;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

/// <summary>What one pull from intervals.icu did.</summary>
public sealed record IntervalsIcuSyncOutcome(int Fetched, int Added, string? Error);

/// <summary>
/// One pull from intervals.icu: list the last eighteen months, download each
/// unseen activity's original file, store it all.
/// </summary>
public sealed class IntervalsIcuSync(
    FitnessDbContext database,
    IntervalsIcuClient icu,
    PosteriorCache cache,
    IClock clock,
    ILogger<IntervalsIcuSync> logger)
{
    /// <summary>New activities per run. A first sync of years of history takes several runs, on purpose.</summary>
    public const int MaxNewPerRun = 40;

    /// <summary>
    /// How far back every listing reaches; the FIT upload covers anything older.
    /// </summary>
    /// <remarks>
    /// Every run, not only the first. Activities reach intervals.icu out of
    /// order — Garmin forwards new ones the day they happen, and the years
    /// before arrive whenever the athlete uploads the Garmin archive there —
    /// so a listing that only looked past the newest activity seen would
    /// never notice the backfill. The listing is one request whatever its
    /// span, and the known ids keep it from re-reading anything.
    /// </remarks>
    public static readonly Period ListingReach = Period.FromMonths(18);

    public async Task<IntervalsIcuSyncOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var state = await StateAsync(cancellationToken);
        state.LastRunAt = now;

        try
        {
            var today = now.InUtc().Date;
            var summaries = await icu.ActivitiesAsync(today.Minus(ListingReach), today.PlusDays(1), cancellationToken);

            var known = (await database.Activities
                    .Where(a => a.Source == IntervalsIcuMapping.Source && a.ExternalId != null)
                    .Select(a => a.ExternalId!)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var batch = new List<Activity>();
            var fetched = 0;
            foreach (var summary in summaries.OrderBy(s => s.StartDate ?? s.StartDateLocal))
            {
                fetched++;
                if (known.Contains(IntervalsIcuMapping.ExternalId(summary.Id))) continue;
                if (batch.Count >= MaxNewPerRun) break;

                // The original file costs a request; only new activities pay it,
                // and a missing one is a summary, not a failure.
                byte[]? file = null;
                try
                {
                    file = await icu.OriginalFileAsync(summary.Id, cancellationToken)
                           ?? await icu.GeneratedFitAsync(summary.Id, cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    logger.LogWarning(exception, "intervals.icu file for {ActivityId} was not available; storing the summary.", summary.Id);
                }

                if (IntervalsIcuMapping.Map(summary, file) is { } activity) batch.Add(activity);
            }

            var added = 0;
            if (batch.Count > 0)
            {
                var outcome = await ActivityStore.UpsertAsync(database, batch, cancellationToken);
                added = outcome.Added;
                if (added > 0) cache.Invalidate();
            }

            state.LastOutcome = $"{fetched} seen, {added} new";
            await database.SaveChangesAsync(cancellationToken);
            return new IntervalsIcuSyncOutcome(fetched, added, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "intervals.icu sync failed.");
            state.LastOutcome = "intervals.icu did not answer; will try again.";
            await database.SaveChangesAsync(cancellationToken);
            return new IntervalsIcuSyncOutcome(0, 0, state.LastOutcome);
        }
    }

    private async Task<SyncState> StateAsync(CancellationToken cancellationToken)
    {
        var state = await database.SyncStates.SingleOrDefaultAsync(s => s.Source == SyncSchedule.IntervalsIcuSource, cancellationToken);
        if (state is null)
        {
            state = new SyncState { Source = SyncSchedule.IntervalsIcuSource };
            database.SyncStates.Add(state);
        }

        return state;
    }
}
