using aberaTech.Scheduling.Alerts;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    private static AlertRoutine Routine(string label, int hour = 6, int minute = 30, int[]? days = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), label, hour, minute, days ?? [1, 2, 3, 4, 5], true, 9, Now);

    [PostgresFact]
    public async Task A_routine_is_a_row_a_new_process_reads_back_whole_updates_in_place_and_deletes()
    {
        var id = Guid.NewGuid();
        await using (var context = Context())
        {
            Assert.True(await new DatabaseAlertStore(context).AddRoutineAsync(
                Routine("Wake up", days: [1, 3, 5], id: id), CancellationToken.None));
        }

        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            var read = Assert.Single(await store.RoutinesAsync(CancellationToken.None));
            Assert.Equal(id, read.Id);
            Assert.Equal("Wake up", read.Label);
            Assert.Equal(6, read.Hour);
            Assert.Equal(30, read.Minute);
            Assert.Equal([1, 3, 5], read.Days);
            Assert.True(read.Enabled);
            Assert.Equal(9, read.SnoozeMinutes);
            Assert.Equal(Now, read.UpdatedAt);

            var later = Now + Duration.FromMinutes(5);
            Assert.True(await store.UpdateRoutineAsync(
                new AlertRoutine(id, "Gym", 5, 0, [], false, 30, later), CancellationToken.None));
            Assert.False(await store.UpdateRoutineAsync(Routine("Nobody"), CancellationToken.None));
        }

        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            var read = Assert.Single(await store.RoutinesAsync(CancellationToken.None));
            Assert.Equal(
                (id, "Gym", 5, 0, false, 30, Now + Duration.FromMinutes(5)),
                (read.Id, read.Label, read.Hour, read.Minute, read.Enabled, read.SnoozeMinutes, read.UpdatedAt));
            Assert.Empty(read.Days);
            var row = await context.AlertRoutines.AsNoTracking().SingleAsync();
            Assert.Equal(Now, row.CreatedAt);

            Assert.True(await store.DeleteRoutineAsync(id, CancellationToken.None));
            Assert.False(await store.DeleteRoutineAsync(id, CancellationToken.None));
            Assert.Empty(await store.RoutinesAsync(CancellationToken.None));
        }
    }

    [PostgresFact]
    public async Task Routines_read_back_by_hour_minute_then_label()
    {
        await using var context = Context();
        var store = new DatabaseAlertStore(context);
        foreach (var routine in new[] { Routine("b", 7, 0), Routine("late", 22, 5), Routine("a", 7, 0), Routine("early", 6, 45) })
        {
            await store.AddRoutineAsync(routine, CancellationToken.None);
        }

        Assert.Equal(["early", "a", "b", "late"], (await store.RoutinesAsync(CancellationToken.None)).Select(routine => routine.Label));
    }

    [PostgresFact]
    public async Task The_51st_routine_is_refused()
    {
        await using var context = Context();
        var store = new DatabaseAlertStore(context);
        for (var n = 0; n < AlertRoutines.MaxRoutines; n++)
        {
            Assert.True(await store.AddRoutineAsync(Routine($"R{n}"), CancellationToken.None));
        }

        Assert.False(await store.AddRoutineAsync(Routine("One more"), CancellationToken.None));
        Assert.Equal(AlertRoutines.MaxRoutines, await context.AlertRoutines.CountAsync());
    }

    [PostgresFact]
    public async Task Twenty_creates_at_once_on_45_kept_make_50_and_no_more()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            for (var n = 0; n < 45; n++) await store.AddRoutineAsync(Routine($"Kept {n}"), CancellationToken.None);
        }

        var contexts = Enumerable.Range(0, 20).Select(_ => Context()).ToList();
        try
        {
            var made = await Task.WhenAll(contexts.Select((context, n) =>
                new DatabaseAlertStore(context).AddRoutineAsync(Routine($"Race {n}"), CancellationToken.None)));

            Assert.Equal(AlertRoutines.MaxRoutines - 45, made.Count(taken => taken));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }

        await using var check = Context();
        Assert.Equal(AlertRoutines.MaxRoutines, await check.AlertRoutines.CountAsync());
    }

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
        var first = new AlertSettings(45, 20, "", 15, 3, 24, true, "", []);
        var second = new AlertSettings(
            120, 30, "siren", 5, 1, 72, false, "Asia/Amman", ["neb@work.example", "neb@home.example"], 1, "bike", "notification", 240);

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
                 INSERT INTO "AlertSettings" ("Id", "RepeatSeconds", "StopAfterMinutes", "Sound",
                     "DefaultLeadMinutes", "PollMinutes", "LookaheadHours", "IncludeAllDay", "TimeZone", "OwnerEmails", "UpdatedAt")
                 VALUES (1, 30, 20, 'persistent', 15, 5, 48, false, '', {Array.Empty<string>()}, {Now})
                 """);
        }

        await using var check = Context();
        var read = await new DatabaseAlertStore(check).SettingsAsync(CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal((30, 20, "persistent"), (read.RepeatSeconds, read.StopAfterMinutes, read.Sound));
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

    [PostgresFact]
    public async Task An_acknowledgement_is_one_row_per_occurrence_and_the_first_one_stands()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            Assert.False(await store.IsAcknowledgedAsync("k", CancellationToken.None));
            Assert.True(await store.AcknowledgeAsync("k", Start, "phone", Now, CancellationToken.None));
            Assert.False(await store.AcknowledgeAsync("k", Start, "browser", Now + Duration.FromMinutes(1), CancellationToken.None));
        }

        await using var restarted = Context();
        var again = new DatabaseAlertStore(restarted);
        Assert.True(await again.IsAcknowledgedAsync("k", CancellationToken.None));
        Assert.Equal(new AlertAcknowledgement(Now, "phone"), (await again.AcknowledgementsAsync(CancellationToken.None))["k"]);
        Assert.Equal(1, await restarted.AlertAcknowledgements.CountAsync());
    }

    [PostgresFact]
    public async Task Two_acknowledgements_at_the_same_moment_store_one_and_only_one_is_first()
    {
        var contexts = Enumerable.Range(0, 6).Select(_ => Context()).ToList();
        try
        {
            var firsts = await Task.WhenAll(contexts.Select(context =>
                new DatabaseAlertStore(context).AcknowledgeAsync("k", Start, "phone", Now, CancellationToken.None)));
            Assert.Equal(1, firsts.Count(first => first));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }
    }

    [PostgresFact]
    public async Task The_receipt_is_kept_with_the_outcome_and_one_that_is_not_letters_and_digits_is_dropped()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.TryClaimAsync("a", Start, Now, CancellationToken.None);
            await store.RecordOutcomeAsync("a", "sent", Now, CancellationToken.None, "abcdefghij0123456789abcdefghij");
            await store.TryClaimAsync("b", Start, Now, CancellationToken.None);
            await store.RecordOutcomeAsync("b", "sent", Now, CancellationToken.None, "../../x");
        }

        await using var check = Context();
        var store2 = new DatabaseAlertStore(check);
        Assert.Equal(new AlertDelivery(Start, "sent", "abcdefghij0123456789abcdefghij"), await store2.DeliveryAsync("a", CancellationToken.None));
        Assert.Null((await store2.DeliveryAsync("b", CancellationToken.None))!.Receipt);
        Assert.Null(await store2.DeliveryAsync("never", CancellationToken.None));
    }

    [PostgresFact]
    public async Task Pruning_forgets_old_acknowledgements_with_the_claims_and_skips()
    {
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.AcknowledgeAsync("old", Start - Duration.FromDays(30), "phone", Now - Duration.FromDays(30), CancellationToken.None);
            await store.AcknowledgeAsync("new", Start, "browser", Now, CancellationToken.None);

            await store.PruneAsync(Now - Duration.FromDays(14), CancellationToken.None);
        }

        await using var check = Context();
        Assert.Equal(["new"], await check.AlertAcknowledgements.Select(row => row.OccurrenceKey).ToListAsync());
    }

    [PostgresFact]
    public async Task A_row_saved_before_the_backup_delay_existed_reads_back_with_no_delay()
    {
        await using (var context = Context())
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "AlertSettings" ("Id", "RepeatSeconds", "StopAfterMinutes", "Sound",
                     "DefaultLeadMinutes", "PollMinutes", "LookaheadHours", "IncludeAllDay", "TimeZone", "OwnerEmails",
                     "NotificationPriority", "NotificationSound", "DefaultType", "UpdatedAt")
                 VALUES (1, 30, 20, 'persistent', 15, 5, 48, false, '', {Array.Empty<string>()}, 0, '', 'none', {Now})
                 """);
        }

        await using var check = Context();
        Assert.Equal(0, (await new DatabaseAlertStore(check).SettingsAsync(CancellationToken.None))!.BackupDelaySeconds);
    }

    [PostgresFact]
    public async Task A_created_event_is_kept_once_read_back_soonest_first_by_a_new_process_and_forgotten_by_uid()
    {
        var later = new CreatedAlertEvent("later@google.com", "Later", null, Start + Duration.FromHours(2), Start + Duration.FromHours(3), 0, false);
        var dentist = new CreatedAlertEvent("dentist@google.com", "Dentist", "Main St", Start, Start + Duration.FromMinutes(30), 20, true);
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.AddCreatedEventAsync(later, Now, CancellationToken.None);
            await store.AddCreatedEventAsync(dentist, Now, CancellationToken.None);
            // A retry of the same create, on this replica or another, keeps one row.
            await store.AddCreatedEventAsync(dentist with { Title = "Changed" }, Now, CancellationToken.None);
        }

        await using (var check = Context())
        {
            var store = new DatabaseAlertStore(check);
            Assert.Equal([dentist, later], await store.CreatedEventsAsync(CancellationToken.None));
            await store.ForgetCreatedEventsAsync(["dentist@google.com", "unknown@google.com"], CancellationToken.None);
        }

        await using var after = Context();
        Assert.Equal([later], await new DatabaseAlertStore(after).CreatedEventsAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task An_edit_and_a_deletion_are_rows_every_replica_reads_back_whole_and_forgets_by_id()
    {
        var edit = new AlertEventChange(
            Guid.NewGuid(), "daily@google.com", Series: true, Start, Deleted: false, "Daily sync", LocationSet: true, null,
            Start + Duration.FromMinutes(90), Start + Duration.FromMinutes(120), 30, 20, "America/New_York", Now,
            AlertAt: Start + Duration.FromMinutes(70), Reminder: true, Critical: true, Recurring: true, PickedLocation: "Room 2");
        var deletion = new AlertEventChange(
            Guid.NewGuid(), "standup@google.com", Series: false, Start, Deleted: true, null, false, null, null, null, null, null, null,
            Now + Duration.FromSeconds(1));

        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.AddEventChangeAsync(deletion, CancellationToken.None);
            await store.AddEventChangeAsync(edit, CancellationToken.None);
        }

        await using (var check = Context())
        {
            var store = new DatabaseAlertStore(check);
            // Oldest first, every field as written.
            Assert.Equal([edit, deletion], await store.EventChangesAsync(CancellationToken.None));
            await store.ForgetEventChangesAsync([edit.Id, Guid.NewGuid()], CancellationToken.None);
        }

        await using var after = Context();
        Assert.Equal([deletion], await new DatabaseAlertStore(after).EventChangesAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task An_edit_of_a_kept_created_event_rewrites_its_row_and_a_gone_row_stays_gone()
    {
        var dentist = new CreatedAlertEvent("dentist@google.com", "Dentist", "Main St", Start, Start + Duration.FromMinutes(30), 20, true);
        await using (var context = Context())
        {
            var store = new DatabaseAlertStore(context);
            await store.AddCreatedEventAsync(dentist, Now, CancellationToken.None);
            await store.UpdateCreatedEventAsync(
                dentist with { Title = "Dentist moved", Location = null, StartsAt = Start + Duration.FromHours(1), EndsAt = Start + Duration.FromHours(2), LeadMinutes = 5 },
                CancellationToken.None);
            await store.UpdateCreatedEventAsync(dentist with { EventId = "gone@google.com" }, CancellationToken.None);
        }

        await using var check = Context();
        var row = Assert.Single(await new DatabaseAlertStore(check).CreatedEventsAsync(CancellationToken.None));
        Assert.Equal(
            dentist with { Title = "Dentist moved", Location = null, StartsAt = Start + Duration.FromHours(1), EndsAt = Start + Duration.FromHours(2), LeadMinutes = 5 },
            row);
    }

    [PostgresFact]
    public async Task A_row_saved_at_priority_0_before_the_migration_reads_back_as_an_alarm_that_repeats()
    {
        using var database = new TestDatabase("alertsmigrate");
        SchedulingDbContext Fresh() =>
            new(new DbContextOptionsBuilder<SchedulingDbContext>()
                .UseNpgsql(database.ConnectionString, npgsql => npgsql.UseNodaTime())
                .Options);

        await using (var context = Fresh())
        {
            // The schema the previous release ran on, and a row it saved with
            // the alarm set to ring once.
            await context.GetService<IMigrator>().MigrateAsync("20260928220345_AlertDevices");
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "AlertSettings" ("Id", "Priority", "RepeatSeconds", "StopAfterMinutes", "Sound",
                     "DefaultLeadMinutes", "PollMinutes", "LookaheadHours", "IncludeAllDay", "TimeZone", "OwnerEmails",
                     "NotificationPriority", "NotificationSound", "DefaultType", "BackupDelaySeconds", "UpdatedAt")
                 VALUES (1, 0, 45, 20, 'bike', 15, 5, 48, false, '', {Array.Empty<string>()}, 1, '', 'none', 0, {Now})
                 """);
        }

        await using (var context = Fresh())
        {
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        await using var check = Fresh();
        var read = (await new DatabaseAlertStore(check).SettingsAsync(CancellationToken.None))!;
        Assert.Equal(new PushoverDelivery(2, 45, 1200, "bike"), read.AlarmDelivery);
        Assert.Equal(1, read.NotificationPriority);
        var columns = await check.Database
            .SqlQuery<string>($"""SELECT column_name AS "Value" FROM information_schema.columns WHERE table_name = 'AlertSettings'""")
            .ToListAsync();
        Assert.DoesNotContain("Priority", columns);
        Assert.Contains("NotificationPriority", columns);
    }

    public void Dispose() => _database?.Dispose();
}
