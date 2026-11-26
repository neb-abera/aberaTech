using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>Who acknowledged one occurrence, and when.</summary>
public sealed record AlertAcknowledgement(Instant At, string Via);

/// <summary>Where an acknowledgment came from.</summary>
public static class AlertAcknowledgementVia
{
    /// <summary>A paired phone's alarm.</summary>
    public const string Phone = "phone";

    /// <summary>The page's Ring in this browser.</summary>
    public const string Browser = "browser";

    /// <summary>The Pushover app. Only Pushover's callback records it: POST /ack never takes it.</summary>
    public const string Pushover = "pushover";

    /// <summary>What a client may send to POST /ack.</summary>
    public static bool FromClient(string? via) => via is Phone or Browser;
}

/// <summary>The claim on one occurrence's send: its start, how it went, and Pushover's receipt for an alarm.</summary>
public sealed record AlertDelivery(Instant StartsAt, string Outcome, string? Receipt);

/// <summary>The occurrence a Pushover receipt was sent for: its key and its start.</summary>
public sealed record ReceiptDelivery(string Key, Instant StartsAt);

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

    /// <summary>The owner's rules for what an event sends, in order.</summary>
    Task<IReadOnlyList<AlertTypeRule>> TypeRulesAsync(CancellationToken cancellationToken);

    /// <summary>Replaces every rule with <paramref name="rules"/>, in one transaction.</summary>
    Task SaveTypeRulesAsync(IReadOnlyList<AlertTypeRule> rules, Instant now, CancellationToken cancellationToken);

    Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken);

    /// <summary>Null unmutes.</summary>
    Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken);

    Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken);

    Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken);

    Task UnskipAsync(string key, CancellationToken cancellationToken);

    /// <summary>True for exactly one caller per key, ever, whatever process it runs in.</summary>
    Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken);

    /// <param name="receipt">Pushover's receipt for an alarm, kept so an acknowledgment can cancel its repeats.</param>
    Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken, string? receipt = null);

    /// <summary>The claim on one occurrence, or null when nothing was ever claimed for it.</summary>
    Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken);

    /// <summary>The occurrence whose send Pushover gave this receipt, or null when none did.</summary>
    Task<ReceiptDelivery?> DeliveryByReceiptAsync(string receipt, CancellationToken cancellationToken);

    /// <summary>Every acknowledged occurrence, by key.</summary>
    Task<IReadOnlyDictionary<string, AlertAcknowledgement>> AcknowledgementsAsync(CancellationToken cancellationToken);

    Task<bool> IsAcknowledgedAsync(string key, CancellationToken cancellationToken);

    /// <summary>True for the first acknowledgment of a key. A second one changes nothing and is false.</summary>
    Task<bool> AcknowledgeAsync(string key, Instant startsAt, string via, Instant now, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets claims, skips, acknowledgments and held rings from before
    /// <paramref name="before"/>, and completions, outcomes and delays whose cycle rang before it,
    /// photos with them. The feed never plans those again.
    /// </summary>
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

    /// <summary>Every lead kept here, by event UID or occurrence key.</summary>
    Task<IReadOnlyDictionary<string, int>> EventLeadsAsync(CancellationToken cancellationToken);

    /// <summary>Keeps a lead for an event UID or one occurrence's key.</summary>
    Task SetEventLeadAsync(string key, int leadMinutes, Instant now, CancellationToken cancellationToken);

    /// <summary>The events created on /alerts that are still kept, soonest first.</summary>
    Task<IReadOnlyList<CreatedAlertEvent>> CreatedEventsAsync(CancellationToken cancellationToken);

    /// <summary>Keeps one created event. A second add of the same UID changes nothing.</summary>
    Task AddCreatedEventAsync(CreatedAlertEvent created, Instant now, CancellationToken cancellationToken);

    /// <summary>Forgets created events: the feed has them now, or they started.</summary>
    Task ForgetCreatedEventsAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken);

    /// <summary>Replaces a kept created event's fields, after an edit. Nothing when the row is gone.</summary>
    Task UpdateCreatedEventAsync(CreatedAlertEvent updated, CancellationToken cancellationToken);

    /// <summary>The edits and deletions made on /alerts that the feed may not show yet, oldest first.</summary>
    Task<IReadOnlyList<AlertEventChange>> EventChangesAsync(CancellationToken cancellationToken);

    /// <summary>Keeps one edit or deletion.</summary>
    Task AddEventChangeAsync(AlertEventChange change, CancellationToken cancellationToken);

    /// <summary>Forgets edits and deletions: the feed shows them, or their occurrences passed.</summary>
    Task ForgetEventChangesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Every routine alarm, by hour, minute and label.</summary>
    Task<IReadOnlyList<AlertRoutine>> RoutinesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new routine alarm. False, and nothing stored, when
    /// <see cref="AlertRoutines.MaxRoutines"/> are already kept, however many
    /// creates race.
    /// </summary>
    Task<bool> AddRoutineAsync(AlertRoutine routine, CancellationToken cancellationToken);

    /// <summary>Replaces every field but the id and the creation time, and every step. False when there is no such routine.</summary>
    Task<bool> UpdateRoutineAsync(AlertRoutine routine, CancellationToken cancellationToken);

    /// <summary>False when there was no such routine.</summary>
    Task<bool> DeleteRoutineAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The rings held through an edit of their routine (<see cref="RoutineRings"/>).</summary>
    Task<IReadOnlyList<RoutineRing>> HeldRingsAsync(CancellationToken cancellationToken);

    /// <summary>Holds rings that were ringing when their routine was edited. A ring already held stays as it was.</summary>
    Task HoldRingsAsync(IReadOnlyCollection<RoutineRing> rings, CancellationToken cancellationToken);

    /// <summary>Forgets the held rings of one routine: it was switched off or deleted.</summary>
    Task ForgetHeldRingsAsync(Guid routineId, CancellationToken cancellationToken);

    /// <summary>The completions whose cycle rang at or after <paramref name="cyclesFrom"/>, without their photos.</summary>
    Task<IReadOnlyList<RoutineCompletion>> CompletionsAsync(Instant cyclesFrom, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the completion of one step in one cycle, or replaces the one
    /// already stored, which keeps its id. <paramref name="keepPhoto"/> keeps
    /// the stored photo. Otherwise <paramref name="photo"/> replaces it, and
    /// null removes it.
    /// </summary>
    Task SaveCompletionAsync(RoutineCompletion completion, byte[]? photo, bool keepPhoto, CancellationToken cancellationToken);

    /// <summary>False when that step of that cycle was not checked off.</summary>
    Task<bool> DeleteCompletionAsync(Guid routineId, Guid stepId, string cycle, CancellationToken cancellationToken);

    /// <summary>A completion's photo, or null when it has none or there is no such completion.</summary>
    Task<byte[]?> CompletionPhotoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The outcomes whose cycle rang at or after <paramref name="cyclesFrom"/>, by cycle.</summary>
    Task<IReadOnlyList<RoutineOutcome>> OutcomesAsync(Instant cyclesFrom, CancellationToken cancellationToken);

    /// <summary>Stores the outcome of one cycle, or replaces the one already stored.</summary>
    Task SaveOutcomeAsync(RoutineOutcome outcome, CancellationToken cancellationToken);

    /// <summary>False when that cycle had no outcome.</summary>
    Task<bool> DeleteOutcomeAsync(Guid routineId, string cycle, CancellationToken cancellationToken);

    /// <summary>The delays whose cycle rang at or after <paramref name="cyclesFrom"/>, by cycle.</summary>
    Task<IReadOnlyList<RoutineDelay>> DelaysAsync(Instant cyclesFrom, CancellationToken cancellationToken);

    /// <summary>Stores the delay of one cycle, or replaces the one already stored.</summary>
    Task SaveDelayAsync(RoutineDelay delay, CancellationToken cancellationToken);

    /// <summary>False when that cycle had no delay.</summary>
    Task<bool> DeleteDelayAsync(Guid routineId, string cycle, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the acknowledgment and the claim of each key, so its ring
    /// rings again and Pushover sends it again. For rings a delay moved
    /// after they rang.
    /// </summary>
    Task ReleaseRingsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);

    /// <summary>Every countdown, by target and label.</summary>
    Task<IReadOnlyList<AlertCountdown>> CountdownsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new countdown. False, and nothing stored, when
    /// <see cref="AlertCountdowns.MaxCountdowns"/> are already kept, however
    /// many creates race.
    /// </summary>
    Task<bool> AddCountdownAsync(AlertCountdown countdown, CancellationToken cancellationToken);

    /// <summary>Replaces every field but the id and the creation time. False when there is no such countdown.</summary>
    Task<bool> UpdateCountdownAsync(AlertCountdown countdown, CancellationToken cancellationToken);

    /// <summary>False when there was no such countdown.</summary>
    Task<bool> DeleteCountdownAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>The store in the scheduling database, which the site already has.</summary>
public sealed class DatabaseAlertStore(SchedulingDbContext database) : IAlertStore
{
    /// <summary>Held for the count and the insert, so two creates at once cannot make a 51st routine.</summary>
    private const long RoutinesLock = 0x6161747275746e; // "aatrutn"

    /// <summary>Held for the count and the insert, so two creates at once cannot make a 51st countdown.</summary>
    private const long CountdownsLock = 0x616174636e7464; // "aatcntd"

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
                row.BackupDelaySeconds,
                row.PhoneSound,
                row.PhoneSnoozeMinutes);
    }

    public async Task SaveSettingsAsync(AlertSettings settings, Instant now, CancellationToken cancellationToken)
    {
        // One statement, like the mute: two saves on two replicas cannot both insert.
        var emails = settings.OwnerEmails.ToArray();
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertSettings" ("Id", "RepeatSeconds", "StopAfterMinutes", "Sound",
                 "DefaultLeadMinutes", "PollMinutes", "LookaheadHours", "IncludeAllDay", "TimeZone", "OwnerEmails",
                 "NotificationPriority", "NotificationSound", "DefaultType", "BackupDelaySeconds",
                 "PhoneSound", "PhoneSnoozeMinutes", "UpdatedAt")
             VALUES ({AlertSettingsRecord.SingleId}, {settings.RepeatSeconds}, {settings.StopAfterMinutes},
                 {settings.Sound}, {settings.DefaultLeadMinutes}, {settings.PollMinutes}, {settings.LookaheadHours},
                 {settings.IncludeAllDay}, {settings.TimeZone}, {emails},
                 {settings.NotificationPriority}, {settings.NotificationSound}, {settings.DefaultType},
                 {settings.BackupDelaySeconds}, {settings.PhoneSound}, {settings.PhoneSnoozeMinutes}, {now})
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
                 "PhoneSound" = EXCLUDED."PhoneSound",
                 "PhoneSnoozeMinutes" = EXCLUDED."PhoneSnoozeMinutes",
                 "UpdatedAt" = EXCLUDED."UpdatedAt"
             """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AlertTypeRule>> TypeRulesAsync(CancellationToken cancellationToken) =>
        await database.AlertTypeRules.AsNoTracking()
            .OrderBy(rule => rule.Position)
            .Select(rule => new AlertTypeRule(rule.Field, rule.Text, rule.Type))
            .ToListAsync(cancellationToken);

    public async Task SaveTypeRulesAsync(IReadOnlyList<AlertTypeRule> rules, Instant now, CancellationToken cancellationToken)
    {
        // A save from the page and one from the phone at once: the lock
        // makes the second wait for the first, and the last one stands whole.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"AlertTypeRules\" IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        await database.AlertTypeRules.ExecuteDeleteAsync(cancellationToken);
        database.AlertTypeRules.AddRange(rules.Select((rule, position) => new AlertTypeRuleRecord
        {
            Position = position,
            Field = rule.Field,
            Text = rule.Text,
            Type = rule.Type,
            UpdatedAt = now,
        }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
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
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Pushover's callback names the receipt alone, so one delivery holds
        // it (a unique index): the newest send.
        if (kept is not null)
        {
            await database.AlertDeliveries
                .Where(delivery => delivery.Receipt == kept && delivery.OccurrenceKey != key)
                .ExecuteUpdateAsync(set => set.SetProperty(delivery => delivery.Receipt, (string?)null), cancellationToken);
        }

        await database.AlertDeliveries
            .Where(delivery => delivery.OccurrenceKey == key)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(delivery => delivery.Outcome, text)
                    .SetProperty(delivery => delivery.CompletedAt, now)
                    .SetProperty(delivery => delivery.Receipt, kept),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken) =>
        database.AlertDeliveries.AsNoTracking()
            .Where(delivery => delivery.OccurrenceKey == key)
            .Select(delivery => new AlertDelivery(delivery.StartsAt, delivery.Outcome, delivery.Receipt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ReceiptDelivery?> DeliveryByReceiptAsync(string receipt, CancellationToken cancellationToken) =>
        database.AlertDeliveries.AsNoTracking()
            .Where(delivery => delivery.Receipt == receipt)
            .Select(delivery => new ReceiptDelivery(delivery.OccurrenceKey, delivery.StartsAt))
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
        // at once record one acknowledgment, and only the first cancels.
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
        await database.AlertHeldRings.Where(row => row.StartsAt < before).ExecuteDeleteAsync(cancellationToken);
        await database.AlertRoutineCompletions.Where(row => row.CycleAt < before).ExecuteDeleteAsync(cancellationToken);
        await database.AlertRoutineOutcomes.Where(row => row.CycleAt < before).ExecuteDeleteAsync(cancellationToken);
        await database.AlertRoutineDelays.Where(row => row.CycleAt < before).ExecuteDeleteAsync(cancellationToken);
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
        await database.AlertEventLeads
            .Where(lead => ids.Contains(lead.Key))
            .ExecuteUpdateAsync(set => set.SetProperty(lead => lead.LastSeenAt, now), cancellationToken);
        await database.AlertEventLeads.Where(lead => lead.LastSeenAt < forgetBefore).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> EventLeadsAsync(CancellationToken cancellationToken) =>
        await database.AlertEventLeads.AsNoTracking()
            .ToDictionaryAsync(lead => lead.Key, lead => lead.LeadMinutes, StringComparer.Ordinal, cancellationToken);

    public async Task SetEventLeadAsync(string key, int leadMinutes, Instant now, CancellationToken cancellationToken)
    {
        var updated = await database.AlertEventLeads
            .Where(lead => lead.Key == key)
            .ExecuteUpdateAsync(
                set => set.SetProperty(lead => lead.LeadMinutes, leadMinutes).SetProperty(lead => lead.UpdatedAt, now)
                    .SetProperty(lead => lead.LastSeenAt, now),
                cancellationToken);
        if (updated > 0) return;
        database.AlertEventLeads.Add(new AlertEventLeadRecord { Key = key, LeadMinutes = leadMinutes, UpdatedAt = now, LastSeenAt = now });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request kept one first. This one's lead is the later choice.
            database.ChangeTracker.Clear();
            await database.AlertEventLeads
                .Where(lead => lead.Key == key)
                .ExecuteUpdateAsync(set => set.SetProperty(lead => lead.LeadMinutes, leadMinutes), cancellationToken);
        }
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

    public async Task UpdateCreatedEventAsync(CreatedAlertEvent updated, CancellationToken cancellationToken) =>
        await database.AlertCreatedEvents
            .Where(row => row.EventId == updated.EventId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.Title, updated.Title)
                    .SetProperty(row => row.Location, updated.Location)
                    .SetProperty(row => row.StartsAt, updated.StartsAt)
                    .SetProperty(row => row.EndsAt, updated.EndsAt)
                    .SetProperty(row => row.LeadMinutes, updated.LeadMinutes),
                cancellationToken);

    public async Task<IReadOnlyList<AlertEventChange>> EventChangesAsync(CancellationToken cancellationToken) =>
        await database.AlertEventChanges.AsNoTracking()
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Select(row => new AlertEventChange(
                row.Id,
                row.EventId,
                row.Series,
                row.OriginalStartsAt,
                row.Deleted,
                row.Title,
                row.LocationSet,
                row.Location,
                row.StartsAt,
                row.EndsAt,
                row.DurationMinutes,
                row.LeadMinutes,
                row.TimeZone,
                row.CreatedAt,
                row.AlertAt,
                row.Reminder,
                row.Critical,
                row.Recurring,
                row.PickedLocation))
            .ToListAsync(cancellationToken);

    public async Task AddEventChangeAsync(AlertEventChange change, CancellationToken cancellationToken)
    {
        // The id is new for each request, so two replicas never write one row.
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertEventChanges" ("Id", "EventId", "Series", "OriginalStartsAt", "Deleted", "Title",
                 "LocationSet", "Location", "StartsAt", "EndsAt", "DurationMinutes", "LeadMinutes", "TimeZone", "CreatedAt",
                 "AlertAt", "Reminder", "Critical", "Recurring", "PickedLocation")
             VALUES ({change.Id}, {change.EventId}, {change.Series}, {change.OriginalStartsAt}, {change.Deleted},
                 {change.Title}, {change.LocationSet}, {change.Location}, {change.StartsAt}, {change.EndsAt},
                 {change.DurationMinutes}, {change.LeadMinutes}, {change.TimeZone}, {change.CreatedAt},
                 {change.AlertAt}, {change.Reminder}, {change.Critical}, {change.Recurring}, {change.PickedLocation})
             """,
            cancellationToken);
    }

    public async Task ForgetEventChangesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var kept = ids.ToArray();
        await database.AlertEventChanges.Where(row => kept.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertRoutine>> RoutinesAsync(CancellationToken cancellationToken)
    {
        var steps = (await database.AlertRoutineSteps.AsNoTracking()
                .OrderBy(step => step.RoutineId)
                .ThenBy(step => step.Position)
                .ToListAsync(cancellationToken))
            .ToLookup(step => step.RoutineId);
        return AlertRoutines.Sorted(
            (await database.AlertRoutines.AsNoTracking().ToListAsync(cancellationToken))
            .Select(row => new AlertRoutine(
                row.Id,
                row.Label,
                row.Hour,
                row.Minute,
                [.. row.Days.Select(day => (int)day)],
                row.Enabled,
                row.SnoozeMinutes,
                row.UpdatedAt)
            {
                CreatedAt = row.CreatedAt,
                Steps =
                [
                    .. steps[row.Id].Select(step => new AlertRoutineStep(step.Id, step.Label, step.OffsetMinutes, step.CreatedAt)
                    {
                        EveryDays = step.EveryDays,
                        StartsOn = step.StartsOn,
                    }),
                ],
            }));
    }

    /// <summary>Inserts the routine's steps in order. The caller holds the transaction and has removed the old ones.</summary>
    private async Task InsertStepsAsync(AlertRoutine routine, CancellationToken cancellationToken)
    {
        for (var position = 0; position < routine.Steps.Count; position++)
        {
            var step = routine.Steps[position];
            await database.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "AlertRoutineSteps" ("RoutineId", "Id", "Position", "Label", "OffsetMinutes", "CreatedAt", "EveryDays", "StartsOn")
                 VALUES ({routine.Id}, {step.Id}, {(short)position}, {step.Label}, {(short)step.OffsetMinutes}, {step.CreatedAt},
                         {(short)step.EveryDays}, {step.StartsOn})
                 """,
                cancellationToken);
        }
    }

    public async Task<bool> AddRoutineAsync(AlertRoutine routine, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // An advisory lock needs no privilege beyond connecting, so the
        // runtime role can take it, as it does for pairing a phone.
        await database.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({RoutinesLock})", cancellationToken);

        if (await database.AlertRoutines.CountAsync(cancellationToken) >= AlertRoutines.MaxRoutines) return false;

        var days = Days(routine);
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertRoutines" ("Id", "Label", "Hour", "Minute", "Days", "Enabled", "SnoozeMinutes", "CreatedAt", "UpdatedAt")
             VALUES ({routine.Id}, {routine.Label}, {(short)routine.Hour}, {(short)routine.Minute}, {days},
                 {routine.Enabled}, {(short)routine.SnoozeMinutes}, {routine.CreatedAt}, {routine.UpdatedAt})
             """,
            cancellationToken);
        await InsertStepsAsync(routine, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateRoutineAsync(AlertRoutine routine, CancellationToken cancellationToken)
    {
        var days = Days(routine);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var updated = await database.AlertRoutines
            .Where(row => row.Id == routine.Id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.Label, routine.Label)
                    .SetProperty(row => row.Hour, (short)routine.Hour)
                    .SetProperty(row => row.Minute, (short)routine.Minute)
                    .SetProperty(row => row.Days, days)
                    .SetProperty(row => row.Enabled, routine.Enabled)
                    .SetProperty(row => row.SnoozeMinutes, (short)routine.SnoozeMinutes)
                    .SetProperty(row => row.UpdatedAt, routine.UpdatedAt),
                cancellationToken) == 1;
        if (!updated) return false;

        // The whole list, as sent: the creation times came from the stored steps.
        await database.AlertRoutineSteps.Where(step => step.RoutineId == routine.Id).ExecuteDeleteAsync(cancellationToken);
        await InsertStepsAsync(routine, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteRoutineAsync(Guid id, CancellationToken cancellationToken) =>
        await database.AlertRoutines.Where(row => row.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private static short[] Days(AlertRoutine routine) => [.. routine.Days.Select(day => (short)day)];

    public async Task<IReadOnlyList<RoutineRing>> HeldRingsAsync(CancellationToken cancellationToken) =>
        await database.AlertHeldRings.AsNoTracking()
            .OrderBy(row => row.AlertAt)
            .Select(row => new RoutineRing(row.OccurrenceKey, row.RoutineId, row.Label, row.AlertAt, row.StartsAt))
            .ToListAsync(cancellationToken);

    public async Task HoldRingsAsync(IReadOnlyCollection<RoutineRing> rings, CancellationToken cancellationToken)
    {
        // The key decides, like the claims: a ring held twice stays as first held.
        foreach (var ring in rings)
        {
            await database.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "AlertHeldRings" ("OccurrenceKey", "RoutineId", "Label", "AlertAt", "StartsAt")
                 VALUES ({ring.Key}, {ring.RoutineId}, {ring.Label}, {ring.AlertAt}, {ring.StartsAt})
                 ON CONFLICT ("OccurrenceKey") DO NOTHING
                 """,
                cancellationToken);
        }
    }

    public async Task ForgetHeldRingsAsync(Guid routineId, CancellationToken cancellationToken) =>
        await database.AlertHeldRings.Where(row => row.RoutineId == routineId).ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<RoutineCompletion>> CompletionsAsync(Instant cyclesFrom, CancellationToken cancellationToken) =>
        await database.AlertRoutineCompletions.AsNoTracking()
            .Where(row => row.CycleAt >= cyclesFrom)
            .OrderBy(row => row.CycleAt)
            .ThenBy(row => row.StepAt)
            .Select(row => new RoutineCompletion(
                row.Id,
                row.RoutineId,
                row.StepId,
                row.Cycle,
                row.RoutineLabel,
                row.StepLabel,
                row.CycleAt,
                row.StepAt,
                row.CompletedAt,
                row.Note,
                row.Photo != null))
            .ToListAsync(cancellationToken);

    public async Task SaveCompletionAsync(
        RoutineCompletion completion, byte[]? photo, bool keepPhoto, CancellationToken cancellationToken)
    {
        // The unique index decides, like the claims: the phone and the page
        // checking one step off at once store one row, and the later wins.
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertRoutineCompletions" ("Id", "RoutineId", "StepId", "Cycle", "RoutineLabel", "StepLabel",
                 "CycleAt", "StepAt", "CompletedAt", "Note", "Photo")
             VALUES ({completion.Id}, {completion.RoutineId}, {completion.StepId}, {completion.Cycle}, {completion.RoutineLabel},
                 {completion.StepLabel}, {completion.CycleAt}, {completion.StepAt}, {completion.CompletedAt}, {completion.Note}, {photo})
             ON CONFLICT ("RoutineId", "StepId", "Cycle") DO UPDATE SET
                 "RoutineLabel" = EXCLUDED."RoutineLabel",
                 "StepLabel" = EXCLUDED."StepLabel",
                 "CycleAt" = EXCLUDED."CycleAt",
                 "StepAt" = EXCLUDED."StepAt",
                 "CompletedAt" = EXCLUDED."CompletedAt",
                 "Note" = EXCLUDED."Note",
                 "Photo" = CASE WHEN {keepPhoto} THEN "AlertRoutineCompletions"."Photo" ELSE EXCLUDED."Photo" END
             """,
            cancellationToken);
    }

    public async Task<bool> DeleteCompletionAsync(Guid routineId, Guid stepId, string cycle, CancellationToken cancellationToken) =>
        await database.AlertRoutineCompletions
            .Where(row => row.RoutineId == routineId && row.StepId == stepId && row.Cycle == cycle)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<byte[]?> CompletionPhotoAsync(Guid id, CancellationToken cancellationToken) =>
        await database.AlertRoutineCompletions.AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => row.Photo)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RoutineOutcome>> OutcomesAsync(Instant cyclesFrom, CancellationToken cancellationToken) =>
        await database.AlertRoutineOutcomes.AsNoTracking()
            .Where(row => row.CycleAt >= cyclesFrom)
            .OrderBy(row => row.CycleAt)
            .ThenBy(row => row.RoutineId)
            .Select(row => new RoutineOutcome(row.RoutineId, row.Cycle, row.Status, row.RoutineLabel, row.CycleAt, row.At, row.Note))
            .ToListAsync(cancellationToken);

    public async Task SaveOutcomeAsync(RoutineOutcome outcome, CancellationToken cancellationToken)
    {
        // The key decides, as for completions: the phone and the page marking
        // one cycle at once store one row, and the later wins.
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertRoutineOutcomes" ("RoutineId", "Cycle", "Status", "RoutineLabel", "CycleAt", "At", "Note")
             VALUES ({outcome.RoutineId}, {outcome.Cycle}, {outcome.Status}, {outcome.RoutineLabel}, {outcome.CycleAt}, {outcome.At}, {outcome.Note})
             ON CONFLICT ("RoutineId", "Cycle") DO UPDATE SET
                 "Status" = EXCLUDED."Status",
                 "RoutineLabel" = EXCLUDED."RoutineLabel",
                 "CycleAt" = EXCLUDED."CycleAt",
                 "At" = EXCLUDED."At",
                 "Note" = EXCLUDED."Note"
             """,
            cancellationToken);
    }

    public async Task<bool> DeleteOutcomeAsync(Guid routineId, string cycle, CancellationToken cancellationToken) =>
        await database.AlertRoutineOutcomes
            .Where(row => row.RoutineId == routineId && row.Cycle == cycle)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<IReadOnlyList<RoutineDelay>> DelaysAsync(Instant cyclesFrom, CancellationToken cancellationToken) =>
        await database.AlertRoutineDelays.AsNoTracking()
            .Where(row => row.CycleAt >= cyclesFrom)
            .OrderBy(row => row.CycleAt)
            .ThenBy(row => row.RoutineId)
            .Select(row => new RoutineDelay(row.RoutineId, row.Cycle, row.Minutes, row.CycleAt, row.At))
            .ToListAsync(cancellationToken);

    public async Task SaveDelayAsync(RoutineDelay delay, CancellationToken cancellationToken)
    {
        // The key decides, as for outcomes: the later save wins.
        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertRoutineDelays" ("RoutineId", "Cycle", "Minutes", "CycleAt", "At")
             VALUES ({delay.RoutineId}, {delay.Cycle}, {delay.Minutes}, {delay.CycleAt}, {delay.At})
             ON CONFLICT ("RoutineId", "Cycle") DO UPDATE SET
                 "Minutes" = EXCLUDED."Minutes",
                 "CycleAt" = EXCLUDED."CycleAt",
                 "At" = EXCLUDED."At"
             """,
            cancellationToken);
    }

    public async Task<bool> DeleteDelayAsync(Guid routineId, string cycle, CancellationToken cancellationToken) =>
        await database.AlertRoutineDelays
            .Where(row => row.RoutineId == routineId && row.Cycle == cycle)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task ReleaseRingsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0) return;
        var list = keys.ToList();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.AlertAcknowledgements.Where(row => list.Contains(row.OccurrenceKey)).ExecuteDeleteAsync(cancellationToken);
        await database.AlertDeliveries.Where(row => list.Contains(row.OccurrenceKey)).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertCountdown>> CountdownsAsync(CancellationToken cancellationToken) =>
        AlertCountdowns.Sorted(
            (await database.AlertCountdowns.AsNoTracking().ToListAsync(cancellationToken))
            .Select(row => new AlertCountdown(
                row.Id, row.Label, row.TargetAt, row.TimeZone, row.UpdatedAt, row.Alarm, row.CalendarEventId, row.Repeat, row.RepeatEvery)));

    public async Task<bool> AddCountdownAsync(AlertCountdown countdown, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // The same advisory lock pattern as the routines: no privilege beyond connecting.
        await database.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CountdownsLock})", cancellationToken);

        if (await database.AlertCountdowns.CountAsync(cancellationToken) >= AlertCountdowns.MaxCountdowns) return false;

        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertCountdowns"
                 ("Id", "Label", "TargetAt", "TimeZone", "Alarm", "CalendarEventId", "Repeat", "RepeatEvery", "CreatedAt", "UpdatedAt")
             VALUES ({countdown.Id}, {countdown.Label}, {countdown.TargetAt}, {countdown.TimeZone},
                 {countdown.Alarm}, {countdown.CalendarEventId}, {countdown.Repeat}, {countdown.RepeatEvery}, {countdown.UpdatedAt}, {countdown.UpdatedAt})
             """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateCountdownAsync(AlertCountdown countdown, CancellationToken cancellationToken) =>
        await database.AlertCountdowns
            .Where(row => row.Id == countdown.Id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.Label, countdown.Label)
                    .SetProperty(row => row.TargetAt, countdown.TargetAt)
                    .SetProperty(row => row.TimeZone, countdown.TimeZone)
                    .SetProperty(row => row.Alarm, countdown.Alarm)
                    .SetProperty(row => row.CalendarEventId, countdown.CalendarEventId)
                    .SetProperty(row => row.Repeat, countdown.Repeat)
                    .SetProperty(row => row.RepeatEvery, countdown.RepeatEvery)
                    .SetProperty(row => row.UpdatedAt, countdown.UpdatedAt),
                cancellationToken) == 1;

    public async Task<bool> DeleteCountdownAsync(Guid id, CancellationToken cancellationToken) =>
        await database.AlertCountdowns.Where(row => row.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
