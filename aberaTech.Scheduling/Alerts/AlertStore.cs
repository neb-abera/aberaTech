using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// What the send path and the page share across restarts and replicas:
/// the mute switch, the skipped occurrences, and one claim per occurrence.
/// </summary>
public interface IAlertStore
{
    Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken);

    /// <summary>Null unmutes.</summary>
    Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken);

    Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken);

    Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken);

    Task UnskipAsync(string key, CancellationToken cancellationToken);

    /// <summary>True for exactly one caller per key, ever, whatever process it runs in.</summary>
    Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken);

    Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken);

    /// <summary>Forgets claims and skips from before <paramref name="before"/>. The feed never plans those again.</summary>
    Task PruneAsync(Instant before, CancellationToken cancellationToken);
}

/// <summary>The store in the scheduling database, which the site already has.</summary>
public sealed class DatabaseAlertStore(SchedulingDbContext database) : IAlertStore
{
    public async Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken) =>
        await database.AlertMutes.AsNoTracking()
            .Where(mute => mute.Id == AlertMuteRecord.SingleId)
            .Select(mute => mute.MutedUntil)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken)
    {
        // An upsert in one statement, so two presses on two replicas cannot
        // both insert the row.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "AlertMutes" ("Id", "MutedUntil", "UpdatedAt")
             VALUES ({AlertMuteRecord.SingleId}, {until}, {now})
             ON CONFLICT ("Id") DO UPDATE SET "MutedUntil" = EXCLUDED."MutedUntil", "UpdatedAt" = EXCLUDED."UpdatedAt"
             """,
            cancellationToken);
    }

    public async Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken) =>
        (await database.AlertSkips.AsNoTracking().Select(skip => skip.OccurrenceKey).ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.Ordinal);

    public Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken) =>
        database.AlertSkips.AsNoTracking().AnyAsync(skip => skip.OccurrenceKey == key, cancellationToken);

    public async Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "AlertSkips" ("OccurrenceKey", "StartsAt", "CreatedAt")
             VALUES ({key}, {startsAt}, {now})
             ON CONFLICT ("OccurrenceKey") DO NOTHING
             """,
            cancellationToken);
    }

    public async Task UnskipAsync(string key, CancellationToken cancellationToken) =>
        await database.AlertSkips.Where(skip => skip.OccurrenceKey == key).ExecuteDeleteAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken)
    {
        // The primary key does the deciding. A read-then-insert would let two
        // replicas both read "not sent" and both send.
        var inserted = await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "AlertDeliveries" ("OccurrenceKey", "StartsAt", "ClaimedAt", "Outcome")
             VALUES ({key}, {startsAt}, {now}, 'claimed')
             ON CONFLICT ("OccurrenceKey") DO NOTHING
             """,
            cancellationToken);

        return inserted == 1;
    }

    public async Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken)
    {
        var text = outcome.Length <= 64 ? outcome : outcome[..64];
        await database.AlertDeliveries
            .Where(delivery => delivery.OccurrenceKey == key)
            .ExecuteUpdateAsync(
                set => set.SetProperty(delivery => delivery.Outcome, text).SetProperty(delivery => delivery.CompletedAt, now),
                cancellationToken);
    }

    public async Task PruneAsync(Instant before, CancellationToken cancellationToken)
    {
        await database.AlertDeliveries.Where(delivery => delivery.StartsAt < before).ExecuteDeleteAsync(cancellationToken);
        await database.AlertSkips.Where(skip => skip.StartsAt < before).ExecuteDeleteAsync(cancellationToken);
    }
}
