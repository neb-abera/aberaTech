using aberaTech.Scheduling.Alerts;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The rules the send path leans on, held by Postgres itself: one claim per
/// occurrence however many replicas ask at once, and mute, skip and the
/// settings that survive a restart because they are rows.
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
    public async Task Settings_are_one_row_that_a_new_process_reads_back_whole()
    {
        var first = new AlertSettings(0, 45, 20, "", 15, 3, 24, true, "", []);
        var second = new AlertSettings(
            2, 120, 30, "siren", 5, 1, 72, false, "Asia/Amman", ["neb@work.example", "neb@home.example"], 1, "bike", "notification");

        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            Assert.Null(await store.SettingsAsync(CancellationToken.None));
            await store.SaveSettingsAsync(first, Now, CancellationToken.None);
            await store.SaveSettingsAsync(second, Now + Duration.FromMinutes(1), CancellationToken.None);
        }

        await using var restarted = Context();
        var read = await new DatabaseAlertStore(restarted).SettingsAsync(CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(second with { OwnerEmails = read.OwnerEmails }, read);
        Assert.Equal(["neb@work.example", "neb@home.example"], read.OwnerEmails);
        Assert.Equal(1, await restarted.AlertSettings.CountAsync());
        Assert.Equal(Now + Duration.FromMinutes(1), (await restarted.AlertSettings.SingleAsync()).UpdatedAt);
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

    [PostgresFact]
    public async Task A_row_saved_before_the_notification_columns_existed_reads_back_sending_nothing_for_unmarked_events()
    {
        // What the previous release writes, and what the row held before the
        // migration: none of the three new columns.
        await using (var context = Context())
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "AlertSettings" ("Id", "Priority", "RepeatSeconds", "StopAfterMinutes", "Sound",
                     "DefaultLeadMinutes", "PollMinutes", "LookaheadHours", "IncludeAllDay", "TimeZone", "OwnerEmails", "UpdatedAt")
                 VALUES (1, 2, 30, 20, 'persistent', 15, 5, 48, false, '', {Array.Empty<string>()}, {Now})
                 """);
        }

        await using var check = Context();
        var read = await new DatabaseAlertStore(check).SettingsAsync(CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal((2, 30, 20, "persistent"), (read.Priority, read.RepeatSeconds, read.StopAfterMinutes, read.Sound));
        Assert.Equal(0, read.NotificationPriority);
        Assert.Equal("", read.NotificationSound);
        Assert.Equal(AlertTypes.None, read.DefaultType);
    }

    [PostgresFact]
    public async Task An_events_type_is_one_row_per_uid_replaced_in_place_and_removed_by_default()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            Assert.Null(await store.EventTypeAsync("standup@google.com", CancellationToken.None));
            await store.SetEventTypeAsync("standup@google.com", AlertTypes.Alarm, Now, CancellationToken.None);
            await store.SetEventTypeAsync("standup@google.com", AlertTypes.Notification, Now, CancellationToken.None);
            await store.SetEventTypeAsync("review@google.com", AlertTypes.None, Now, CancellationToken.None);
            await store.SetEventTypeAsync("drill@google.com", AlertTypes.Alarm, Now, CancellationToken.None);
            await store.SetEventTypeAsync("drill@google.com", null, Now, CancellationToken.None);
            await store.SetEventTypeAsync("never-set@google.com", null, Now, CancellationToken.None);
        }

        await using var restarted = Context();
        var again = new DatabaseAlertStore(restarted);
        Assert.Equal(AlertTypes.Notification, await again.EventTypeAsync("standup@google.com", CancellationToken.None));
        Assert.Null(await again.EventTypeAsync("drill@google.com", CancellationToken.None));
        var all = await again.EventTypesAsync(CancellationToken.None);
        Assert.Equal(["review@google.com", "standup@google.com"], all.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(AlertTypes.None, all["review@google.com"]);
    }

    [PostgresFact]
    public async Task A_choice_for_an_event_still_in_the_feed_is_kept_and_one_gone_for_sixty_days_is_forgotten()
    {
        var longAgo = Now - Duration.FromDays(61);
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.SetEventTypeAsync("still-there@google.com", AlertTypes.Alarm, longAgo, CancellationToken.None);
            await store.SetEventTypeAsync("deleted@google.com", AlertTypes.Alarm, longAgo, CancellationToken.None);
            await store.SetEventTypeAsync("recent@google.com", AlertTypes.Alarm, Now - Duration.FromDays(3), CancellationToken.None);

            await store.SeenEventsAsync(["still-there@google.com", "unchosen@google.com"], Now, Now - Duration.FromDays(60), CancellationToken.None);
        }

        await using var check = Context();
        var rows = await check.AlertEventTypes.OrderBy(row => row.EventId).ToListAsync();
        Assert.Equal(["recent@google.com", "still-there@google.com"], rows.Select(row => row.EventId));
        Assert.Equal(Now, rows[1].LastSeenAt);
        Assert.Equal(longAgo, rows[1].UpdatedAt);
    }

    public void Dispose() => _database?.Dispose();
}
