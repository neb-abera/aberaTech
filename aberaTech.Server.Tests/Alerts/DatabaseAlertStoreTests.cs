using aberaTech.Scheduling.Alerts;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The rules the send path leans on, held by Postgres itself: one claim per
/// occurrence however many replicas ask at once, and mute and skip that
/// survive a restart because they are rows.
/// </summary>
public sealed class DatabaseAlertStoreTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 28, 12, 0);
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 28, 13, 0);

    private readonly TestDatabase? _database;

    public DatabaseAlertStoreTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("alerts");
        using var context = Context();
        context.Database.Migrate();
    }

    private SchedulingDbContext Context() =>
        new(new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(_database!.ConnectionString, npgsql => npgsql.UseNodaTime())
            .Options);

    [PostgresFact]
    public async Task An_occurrence_is_claimed_once_however_many_replicas_ask_at_the_same_moment()
    {
        var contexts = Enumerable.Range(0, 8).Select(_ => Context()).ToList();
        try
        {
            var claims = await Task.WhenAll(contexts.Select(context =>
                new DatabaseAlertStore(context).TryClaimAsync("standup|20261028T130000Z", Start, Now, CancellationToken.None)));

            Assert.Equal(1, claims.Count(claimed => claimed));

            await using var later = Context();
            Assert.False(await new DatabaseAlertStore(later).TryClaimAsync("standup|20261028T130000Z", Start, Now, CancellationToken.None));
            Assert.True(await new DatabaseAlertStore(later).TryClaimAsync("standup|20261029T130000Z", Start, Now, CancellationToken.None));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }
    }

    [PostgresFact]
    public async Task The_outcome_is_recorded_against_the_claim()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.TryClaimAsync("k", Start, Now, CancellationToken.None);
            await store.RecordOutcomeAsync("k", "failed: HTTP 400", Now, CancellationToken.None);
        }

        await using var check = Context();
        var row = await check.AlertDeliveries.SingleAsync();
        Assert.Equal("failed: HTTP 400", row.Outcome);
        Assert.Equal(Now, row.ClaimedAt);
    }

    [PostgresFact]
    public async Task Mute_is_one_row_that_a_new_process_reads_back()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            Assert.Null(await store.MutedUntilAsync(CancellationToken.None));
            await store.SetMutedUntilAsync(Now + Duration.FromHours(1), Now, CancellationToken.None);
            await store.SetMutedUntilAsync(Now + Duration.FromHours(2), Now, CancellationToken.None);
        }

        await using (var restarted = Context())
        {
            var store = new DatabaseAlertStore(restarted);
            Assert.Equal(Now + Duration.FromHours(2), await store.MutedUntilAsync(CancellationToken.None));
            await store.SetMutedUntilAsync(null, Now, CancellationToken.None);
        }

        await using var cleared = Context();
        Assert.Null(await new DatabaseAlertStore(cleared).MutedUntilAsync(CancellationToken.None));
        Assert.Equal(1, await cleared.AlertMutes.CountAsync());
    }

    [PostgresFact]
    public async Task Skip_and_undo_are_rows_and_skipping_twice_is_one_row()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.SkipAsync("a", Start, Now, CancellationToken.None);
            await store.SkipAsync("a", Start, Now, CancellationToken.None);
            await store.SkipAsync("b", Start, Now, CancellationToken.None);
            await store.UnskipAsync("b", CancellationToken.None);
            await store.UnskipAsync("never-skipped", CancellationToken.None);
        }

        await using var restarted = Context();
        var again = new DatabaseAlertStore(restarted);
        Assert.True(await again.IsSkippedAsync("a", CancellationToken.None));
        Assert.False(await again.IsSkippedAsync("b", CancellationToken.None));
        Assert.Equal(["a"], (await again.SkippedAsync(CancellationToken.None)).Order());
    }

    [PostgresFact]
    public async Task Pruning_removes_what_is_long_past_and_keeps_the_rest()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.TryClaimAsync("old", Start - Duration.FromDays(30), Now - Duration.FromDays(30), CancellationToken.None);
            await store.TryClaimAsync("new", Start, Now, CancellationToken.None);
            await store.SkipAsync("old", Start - Duration.FromDays(30), Now - Duration.FromDays(30), CancellationToken.None);
            await store.SkipAsync("new", Start, Now, CancellationToken.None);

            await store.PruneAsync(Now - Duration.FromDays(14), CancellationToken.None);
        }

        await using var check = Context();
        Assert.Equal(["new"], await check.AlertDeliveries.Select(row => row.OccurrenceKey).ToListAsync());
        Assert.Equal(["new"], await check.AlertSkips.Select(row => row.OccurrenceKey).ToListAsync());
    }

    public void Dispose() => _database?.Dispose();
}
