using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.IntervalsIcu;
using Microsoft.AspNetCore.OutputCaching;
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
    IOutputCacheStore pages,
    IClock clock,
    ILogger<IntervalsIcuSync> logger)
{
    /// <summary>New activities per run. A first sync of years of history takes several runs, on purpose.</summary>
    public const int MaxNewPerRun = 40;

    /// <summary>Older runs re-read per run to add the recording's halves.</summary>
    public const int MaxBackfillPerRun = 20;

    /// <summary>
    /// How far back every listing reaches; the FIT upload covers anything older.
    /// </summary>
    /// <remarks>
    /// Every run, not only the first. Activities reach intervals.icu out of
    /// order. Garmin forwards new ones the day they happen, and the years
    /// before arrive whenever the athlete uploads the Garmin archive there.
    /// So a listing that only looked past the newest activity seen would
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

            // Runs stored before the current reading of the records (the
            // halves, then the best efforts) are read again from their file,
            // a few a run, so older runs are analyzed like new ones.
            var withoutHalves = (await database.Activities
                    .Where(a => a.Source == IntervalsIcuMapping.Source && a.ExternalId != null
                                && (a.Sport == "run" || a.Sport == "ruck") && a.AverageHr != null
                                && a.RecordsVersion < Ingest.FitImport.CurrentRecordsVersion)
                    .OrderByDescending(a => a.StartedAt)
                    .Select(a => a.ExternalId!)
                    .Take(MaxBackfillPerRun)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
            known.ExceptWith(withoutHalves);

            var renamed = await RenameAsync(summaries, known, cancellationToken);

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

            if (batch.Count > 0 || renamed > 0)
            {
                if (renamed > 0) cache.Invalidate();
                await pages.EvictFitnessAsync();
            }

            state.LastOutcome = renamed > 0
                ? $"{fetched} seen, {added} new, {renamed} renamed"
                : $"{fetched} seen, {added} new";
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

    /// <summary>
    /// Takes the listing's name for every stored activity it has renamed. A
    /// stored activity is never downloaded again, so the listing is the only
    /// place a rename can arrive: a run saved as "Treadmill Running" and
    /// renamed "HR drift test" once it was uploaded.
    /// </summary>
    private async Task<int> RenameAsync(IReadOnlyList<IcuActivity> summaries, HashSet<string> known, CancellationToken cancellationToken)
    {
        var listed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var summary in summaries)
        {
            var id = IntervalsIcuMapping.ExternalId(summary.Id);
            if (known.Contains(id)) listed[id] = summary.Name ?? "";
        }

        if (listed.Count == 0) return 0;

        var stored = await database.Activities
            .AsNoTracking()
            .Where(a => a.Source == IntervalsIcuMapping.Source && a.ExternalId != null)
            .Select(a => new { ExternalId = a.ExternalId!, a.Name })
            .ToListAsync(cancellationToken);
        var stale = stored
            .Where(a => listed.TryGetValue(a.ExternalId, out var name) && name != a.Name)
            .Select(a => a.ExternalId)
            .ToHashSet();
        if (stale.Count == 0) return 0;

        var activities = await database.Activities
            .Where(a => a.Source == IntervalsIcuMapping.Source && a.ExternalId != null && stale.Contains(a.ExternalId))
            .ToListAsync(cancellationToken);
        foreach (var activity in activities) IntervalsIcuMapping.Name(activity, listed[activity.ExternalId!]);
        return activities.Count;
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
