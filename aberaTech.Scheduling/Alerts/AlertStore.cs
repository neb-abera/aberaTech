using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>Who acknowledged one occurrence, and when.</summary>
public sealed record AlertAcknowledgement(Instant At, string Via);

/// <summary>The claim on one occurrence's send: its start, how it went, and Pushover's receipt for an alarm.</summary>
public sealed record AlertDelivery(Instant StartsAt, string Outcome, string? Receipt);

/// <summary>
/// What the send path and the page share across restarts and replicas:
/// the mute switch, the skipped occurrences, one claim per occurrence, the
/// owner's settings, and the type the owner chose for each event.
/// </summary>
public interface IAlertStore
{
    /// <summary>The saved settings. Null when the owner has never saved.</summary>
    Task<AlertSettings?> SettingsAsync(CancellationToken cancellationToken);

    Task SaveSettingsAsync(AlertSettings settings, Instant now, CancellationToken cancellationToken);

    Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken);

    /// <summary>Null unmutes.</summary>
    Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken);

    Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken);

    Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken);

    Task UnskipAsync(string key, CancellationToken cancellationToken);

    /// <summary>True for exactly one caller per key, ever, whatever process it runs in.</summary>
    Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken);

    /// <param name="receipt">Pushover's receipt for an alarm, kept so an acknowledgement can cancel its repeats.</param>
    Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken, string? receipt = null);

    /// <summary>The claim on one occurrence, or null when nothing was ever claimed for it.</summary>
    Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken);

    /// <summary>Every acknowledged occurrence, by key.</summary>
    Task<IReadOnlyDictionary<string, AlertAcknowledgement>> AcknowledgementsAsync(CancellationToken cancellationToken);

    Task<bool> IsAcknowledgedAsync(string key, CancellationToken cancellationToken);

    /// <summary>True for the first acknowledgement of a key. A second one changes nothing and is false.</summary>
    Task<bool> AcknowledgeAsync(string key, Instant startsAt, string via, Instant now, CancellationToken cancellationToken);

    /// <summary>Forgets claims, skips and acknowledgements from before <paramref name="before"/>. The feed never plans those again.</summary>
    Task PruneAsync(Instant before, CancellationToken cancellationToken);

    /// <summary>Every event the owner chose a type for, by event id.</summary>
    Task<IReadOnlyDictionary<string, string>> EventTypesAsync(CancellationToken cancellationToken);

    /// <summary>The owner's choice for one event, or null when there is none.</summary>
    Task<string?> EventTypeAsync(string eventId, CancellationToken cancellationToken);

    /// <summary>Sets the choice for one event. Null removes it, and the event follows its mark and the default again.</summary>
    Task SetEventTypeAsync(string eventId, string? type, Instant now, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the choices for the events in this read as seen, and forgets
    /// those not seen since <paramref name="forgetBefore"/>.
    /// </summary>
    Task SeenEventsAsync(IReadOnlyCollection<string> eventIds, Instant now, Instant forgetBefore, CancellationToken cancellationToken);

    /// <summary>The events created on /alerts that are still kept, soonest first.</summary>
    Task<IReadOnlyList<CreatedAlertEvent>> CreatedEventsAsync(CancellationToken cancellationToken);

    /// <summary>Keeps one created event. A second add of the same UID changes nothing.</summary>
    Task AddCreatedEventAsync(CreatedAlertEvent created, Instant now, CancellationToken cancellationToken);

    /// <summary>Forgets created events: the feed has them now, or they started.</summary>
    Task ForgetCreatedEventsAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken);
}

/// <summary>The store in the scheduling database, which the site already has.</summary>
public sealed class DatabaseAlertStore(SchedulingDbContext database) : IAlertStore
{
    public async Task<AlertSettings?> SettingsAsync(CancellationToken cancellationToken)
    {
        var row = await database.AlertSettings.AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == AlertSettingsRecord.SingleId, cancellationToken);

        return row is null
            ? null
            : new AlertSettings(
                row.RepeatSeconds,
                row.StopAfterMinutes,
                row.Sound,
                row.DefaultLeadMinutes,
                row.PollMinutes,
                row.LookaheadHours,
                row.IncludeAllDay,
                row.TimeZone,
                row.OwnerEmails,
                row.NotificationPriority,
                row.NotificationSound,
                row.DefaultType,
                row.BackupDelaySeconds);
    }

    public async Task SaveSettingsAsync(AlertSettings settings, Instant now, CancellationToken cancellationToken)
    {
        // One statement, like the mute: two saves on two replicas cannot both insert.
        var emails = settings.OwnerEmails.ToArray();
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertSettings" ("Id", "RepeatSeconds", "StopAfterMinutes", "Sound",
                 "DefaultLeadMinutes", "PollMinutes", "LookaheadHours", "IncludeAllDay", "TimeZone", "OwnerEmails",
                 "NotificationPriority", "NotificationSound", "DefaultType", "BackupDelaySeconds", "UpdatedAt")
             VALUES ({AlertSettingsRecord.SingleId}, {settings.RepeatSeconds}, {settings.StopAfterMinutes},
                 {settings.Sound}, {settings.DefaultLeadMinutes}, {settings.PollMinutes}, {settings.LookaheadHours},
                 {settings.IncludeAllDay}, {settings.TimeZone}, {emails},
                 {settings.NotificationPriority}, {settings.NotificationSound}, {settings.DefaultType},
                 {settings.BackupDelaySeconds}, {now})
             ON CONFLICT ("Id") DO UPDATE SET
                 "RepeatSeconds" = EXCLUDED."RepeatSeconds",
                 "StopAfterMinutes" = EXCLUDED."StopAfterMinutes",
                 "Sound" = EXCLUDED."Sound",
                 "DefaultLeadMinutes" = EXCLUDED."DefaultLeadMinutes",
                 "PollMinutes" = EXCLUDED."PollMinutes",
                 "LookaheadHours" = EXCLUDED."LookaheadHours",
                 "IncludeAllDay" = EXCLUDED."IncludeAllDay",
                 "TimeZone" = EXCLUDED."TimeZone",
                 "OwnerEmails" = EXCLUDED."OwnerEmails",
                 "NotificationPriority" = EXCLUDED."NotificationPriority",
                 "NotificationSound" = EXCLUDED."NotificationSound",
                 "DefaultType" = EXCLUDED."DefaultType",
                 "BackupDelaySeconds" = EXCLUDED."BackupDelaySeconds",
                 "UpdatedAt" = EXCLUDED."UpdatedAt"
             """,
            cancellationToken);
    }

    public async Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken) =>
        await database.AlertMutes.AsNoTracking()
            .Where(mute => mute.Id == AlertMuteRecord.SingleId)
            .Select(mute => mute.MutedUntil)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken)
    {
        // An upsert in one statement, so two presses on two replicas cannot
        // both insert the row.
        await database.Database.ExecuteSqlAsync(
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
        await database.Database.ExecuteSqlAsync(
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
        var inserted = await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertDeliveries" ("OccurrenceKey", "StartsAt", "ClaimedAt", "Outcome")
             VALUES ({key}, {startsAt}, {now}, 'claimed')
             ON CONFLICT ("OccurrenceKey") DO NOTHING
             """,
            cancellationToken);

        return inserted == 1;
    }

    public async Task RecordOutcomeAsync(
        string key, string outcome, Instant now, CancellationToken cancellationToken, string? receipt = null)
    {
        var text = outcome.Length <= 64 ? outcome : outcome[..64];
        var kept = PushoverClient.IsReceipt(receipt) ? receipt : null;
        await database.AlertDeliveries
            .Where(delivery => delivery.OccurrenceKey == key)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(delivery => delivery.Outcome, text)
                    .SetProperty(delivery => delivery.CompletedAt, now)
                    .SetProperty(delivery => delivery.Receipt, kept),
                cancellationToken);
    }

    public Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken) =>
        database.AlertDeliveries.AsNoTracking()
            .Where(delivery => delivery.OccurrenceKey == key)
            .Select(delivery => new AlertDelivery(delivery.StartsAt, delivery.Outcome, delivery.Receipt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, AlertAcknowledgement>> AcknowledgementsAsync(CancellationToken cancellationToken) =>
        await database.AlertAcknowledgements.AsNoTracking()
            .ToDictionaryAsync(
                row => row.OccurrenceKey,
                row => new AlertAcknowledgement(row.AcknowledgedAt, row.Via),
                StringComparer.Ordinal,
                cancellationToken);

    public Task<bool> IsAcknowledgedAsync(string key, CancellationToken cancellationToken) =>
        database.AlertAcknowledgements.AsNoTracking().AnyAsync(row => row.OccurrenceKey == key, cancellationToken);

    public async Task<bool> AcknowledgeAsync(
        string key, Instant startsAt, string via, Instant now, CancellationToken cancellationToken)
    {
        // The key decides, like the claim: the phone and a browser pressing
        // at once record one acknowledgement, and only the first cancels.
        var inserted = await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertAcknowledgements" ("OccurrenceKey", "StartsAt", "AcknowledgedAt", "Via")
             VALUES ({key}, {startsAt}, {now}, {via})
             ON CONFLICT ("OccurrenceKey") DO NOTHING
             """,
            cancellationToken);

        return inserted == 1;
    }

    public async Task PruneAsync(Instant before, CancellationToken cancellationToken)
    {
        await database.AlertDeliveries.Where(delivery => delivery.StartsAt < before).ExecuteDeleteAsync(cancellationToken);
        await database.AlertSkips.Where(skip => skip.StartsAt < before).ExecuteDeleteAsync(cancellationToken);
        await database.AlertAcknowledgements.Where(row => row.StartsAt < before).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> EventTypesAsync(CancellationToken cancellationToken) =>
        await database.AlertEventTypes.AsNoTracking()
            .ToDictionaryAsync(choice => choice.EventId, choice => choice.Type, StringComparer.Ordinal, cancellationToken);

    public Task<string?> EventTypeAsync(string eventId, CancellationToken cancellationToken) =>
        database.AlertEventTypes.AsNoTracking()
            .Where(choice => choice.EventId == eventId)
            .Select(choice => choice.Type)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SetEventTypeAsync(string eventId, string? type, Instant now, CancellationToken cancellationToken)
    {
        if (type is null)
        {
            await database.AlertEventTypes.Where(choice => choice.EventId == eventId).ExecuteDeleteAsync(cancellationToken);
            return;
        }

        // One statement, like the mute: two presses on two replicas cannot both insert.
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertEventTypes" ("EventId", "Type", "UpdatedAt", "LastSeenAt")
             VALUES ({eventId}, {type}, {now}, {now})
             ON CONFLICT ("EventId") DO UPDATE SET
                 "Type" = EXCLUDED."Type", "UpdatedAt" = EXCLUDED."UpdatedAt", "LastSeenAt" = EXCLUDED."LastSeenAt"
             """,
            cancellationToken);
    }

    public async Task SeenEventsAsync(
        IReadOnlyCollection<string> eventIds, Instant now, Instant forgetBefore, CancellationToken cancellationToken)
    {
        var ids = eventIds.ToArray();
        await database.AlertEventTypes
            .Where(choice => ids.Contains(choice.EventId))
            .ExecuteUpdateAsync(set => set.SetProperty(choice => choice.LastSeenAt, now), cancellationToken);
        await database.AlertEventTypes.Where(choice => choice.LastSeenAt < forgetBefore).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreatedAlertEvent>> CreatedEventsAsync(CancellationToken cancellationToken) =>
        await database.AlertCreatedEvents.AsNoTracking()
            .OrderBy(row => row.StartsAt)
            .Select(row => new CreatedAlertEvent(
                row.EventId, row.Title, row.Location, row.StartsAt, row.EndsAt, row.LeadMinutes, row.Critical))
            .ToListAsync(cancellationToken);

    public async Task AddCreatedEventAsync(CreatedAlertEvent created, Instant now, CancellationToken cancellationToken)
    {
        // The UID is the key, so a retried create on two replicas keeps one row.
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertCreatedEvents" ("EventId", "Title", "Location", "StartsAt", "EndsAt", "LeadMinutes", "Critical", "CreatedAt")
             VALUES ({created.EventId}, {created.Title}, {created.Location}, {created.StartsAt}, {created.EndsAt},
                 {created.LeadMinutes}, {created.Critical}, {now})
             ON CONFLICT ("EventId") DO NOTHING
             """,
            cancellationToken);
    }

    public async Task ForgetCreatedEventsAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken)
    {
        var ids = eventIds.ToArray();
        await database.AlertCreatedEvents.Where(row => ids.Contains(row.EventId)).ExecuteDeleteAsync(cancellationToken);
    }
}
