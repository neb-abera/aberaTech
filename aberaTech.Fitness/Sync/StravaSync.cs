using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Strava;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

/// <summary>What one pull from Strava did.</summary>
public sealed record StravaSyncOutcome(int Fetched, int Added, string? Error);

/// <summary>
/// One pull from Strava: refresh the grant, list what started since the last
/// activity seen, fetch each new one's laps, store it all.
/// </summary>
public sealed class StravaSync(
    FitnessDbContext database,
    StravaClient strava,
    IDataProtectionProvider protection,
    PosteriorCache cache,
    IClock clock,
    ILogger<StravaSync> logger)
{
    /// <summary>The purpose string that ties the stored token to this use and nothing else.</summary>
    public const string ProtectionPurpose = "aberaTech.Fitness.StravaRefreshToken";

    /// <summary>Pages per run. A first sync of years of history takes several runs, on purpose.</summary>
    private const int MaxPagesPerRun = 5;

    public string Protect(string refreshToken) =>
        protection.CreateProtector(ProtectionPurpose).Protect(refreshToken);

    public async Task<StravaSyncOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var connection = await database.StravaConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);
        if (connection is null) return new StravaSyncOutcome(0, 0, "Strava is not connected.");

        var now = clock.GetCurrentInstant();
        var state = await StateAsync(cancellationToken);
        state.LastRunAt = now;

        try
        {
            string refreshToken;
            try
            {
                refreshToken = protection.CreateProtector(ProtectionPurpose).Unprotect(connection.ProtectedRefreshToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The stored Strava refresh token could not be unprotected.");
                return await FailAsync(state, "The stored Strava token could not be read; reconnect Strava.", cancellationToken);
            }

            var tokens = await strava.RefreshAsync(refreshToken, cancellationToken);
            if (tokens is null)
            {
                return await FailAsync(state, "Strava refused to refresh the token; reconnect Strava.", cancellationToken);
            }

            // Strava may rotate the refresh token; keep whichever it hands back.
            if (tokens.RefreshToken != refreshToken)
            {
                connection.ProtectedRefreshToken = Protect(tokens.RefreshToken);
            }

            var lastSeen = await database.Activities
                .Where(a => a.Source == StravaMapping.Source)
                .OrderByDescending(a => a.StartedAt)
                .Select(a => (Instant?)a.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var after = SyncSchedule.ListFrom(lastSeen).ToUnixTimeSeconds();
            var fetched = 0;
            var added = 0;

            for (var page = 1; page <= MaxPagesPerRun; page++)
            {
                var summaries = await strava.ActivitiesAsync(tokens.AccessToken, after, page, cancellationToken);
                if (summaries.Count == 0) break;

                var known = await database.Activities
                    .Where(a => a.Source == StravaMapping.Source)
                    .Select(a => a.ExternalId!)
                    .ToListAsync(cancellationToken);
                var knownIds = known.ToHashSet();

                var batch = new List<Activity>();
                foreach (var summary in summaries)
                {
                    fetched++;
                    if (knownIds.Contains($"strava:{summary.Id}")) continue;

                    // Laps cost a request each, so only new activities pay it.
                    IReadOnlyList<StravaLap> laps = [];
                    try
                    {
                        laps = await strava.LapsAsync(tokens.AccessToken, summary.Id, cancellationToken);
                    }
                    catch (HttpRequestException exception)
                    {
                        logger.LogWarning(exception, "Strava laps for {ActivityId} were not available; storing without them.", summary.Id);
                    }

                    if (StravaMapping.Map(summary, laps) is { } activity) batch.Add(activity);
                }

                if (batch.Count > 0)
                {
                    var outcome = await ActivityStore.UpsertAsync(database, batch, cancellationToken);
                    added += outcome.Added;
                }

                if (summaries.Count < StravaClient.PageSize) break;
            }

            if (added > 0) cache.Invalidate();

            connection.LastSyncedAt = now;
            connection.LastError = null;
            state.LastOutcome = $"{fetched} seen, {added} new";
            await database.SaveChangesAsync(cancellationToken);

            return new StravaSyncOutcome(fetched, added, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Strava sync failed.");
            return await FailAsync(state, "Strava did not answer; will try again.", cancellationToken);
        }
    }

    private async Task<StravaSyncOutcome> FailAsync(SyncState state, string error, CancellationToken cancellationToken)
    {
        state.LastOutcome = error;
        var connection = await database.StravaConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);
        if (connection is not null) connection.LastError = error;
        await database.SaveChangesAsync(cancellationToken);
        return new StravaSyncOutcome(0, 0, error);
    }

    private async Task<SyncState> StateAsync(CancellationToken cancellationToken)
    {
        var state = await database.SyncStates.SingleOrDefaultAsync(s => s.Source == SyncSchedule.StravaSource, cancellationToken);
        if (state is null)
        {
            state = new SyncState { Source = SyncSchedule.StravaSource };
            database.SyncStates.Add(state);
        }

        return state;
    }
}
