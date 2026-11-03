using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// An edit or a deletion of an event on the calendar the alerts read, and
/// what the server keeps until the feed shows it. The event editor and the
/// countdowns both write through here, so a moved or deleted event is
/// planned the same way whichever made it.
/// </summary>
public static class AlertEventWrites
{
    /// <summary>
    /// Writes the edit to Google, then stores it, updates a created event
    /// the feed does not have yet, and carries the occurrence's skip, claim
    /// and acknowledgement. Nothing is stored when Google did not take it.
    /// </summary>
    /// <param name="before">The plan before the edit.</param>
    /// <param name="zone">The zone to write a time in when Google names none.</param>
    public static async Task<EventWriteOutcome> EditAsync(
        PlannedAlert alert,
        IReadOnlyList<PlannedAlert> before,
        bool series,
        AlertEventEdit edit,
        DateTimeZone zone,
        GoogleAlertEvents google,
        IAlertStore store,
        Instant now,
        CancellationToken cancellationToken)
    {
        var outcome = await google.EditAsync(alert, series, edit, zone, cancellationToken);
        if (outcome.Problem is not null) return outcome;

        var kept = outcome.Series && outcome.TimeZone is { } named
            ? DateTimeZoneProviders.Tzdb.GetZoneOrNull(named) ?? zone
            : zone;
        var change = AlertEventChange.Edit(
            Guid.NewGuid(), alert, outcome.Series, edit, outcome.Series ? outcome.TimeZone : null, kept, now);
        await store.AddEventChangeAsync(change, cancellationToken);

        // A created event the feed does not have yet is planned from its
        // row, which takes the edit too.
        var created = await store.CreatedEventsAsync(cancellationToken);
        if (created.FirstOrDefault(row => AlertPlanner.EventIdFor(row.EventId) == alert.EventId) is { } row)
        {
            var start = edit.StartsAt;
            await store.UpdateCreatedEventAsync(
                row with
                {
                    Title = edit.Title,
                    Location = edit.LocationSet ? edit.Location : row.Location,
                    StartsAt = start,
                    EndsAt = start + (edit.DurationMinutes is { } minutes ? Duration.FromMinutes(minutes) : row.EndsAt - row.StartsAt),
                    LeadMinutes = edit.LeadMinutes ?? row.LeadMinutes
                },
                cancellationToken);
        }

        await EventMoves.CarryAsync(before, change, kept, store, now, cancellationToken);
        return outcome;
    }

    /// <summary>
    /// Deletes on Google, then stores the deletion, forgets a created event
    /// the feed does not have yet, pushes the phones, and cancels Pushover's
    /// repeats for each occurrence that went. Nothing is stored when Google
    /// did not take it.
    /// </summary>
    /// <param name="plan">The plan before the deletion.</param>
    public static async Task<EventWriteOutcome> DeleteAsync(
        PlannedAlert alert,
        IReadOnlyList<PlannedAlert> plan,
        bool series,
        GoogleAlertEvents google,
        IAlertStore store,
        AlertDispatcher dispatcher,
        AlertPushWorker pushes,
        Instant now,
        CancellationToken cancellationToken)
    {
        var outcome = await google.DeleteAsync(alert, series, cancellationToken);
        if (outcome.Problem is not null) return outcome;

        await store.AddEventChangeAsync(AlertEventChange.Deletion(Guid.NewGuid(), alert, outcome.Series, now), cancellationToken);
        var created = await store.CreatedEventsAsync(cancellationToken);
        var pending = created.Where(row => AlertPlanner.EventIdFor(row.EventId) == alert.EventId).Select(row => row.EventId).ToList();
        if (pending.Count > 0) await store.ForgetCreatedEventsAsync(pending, cancellationToken);

        await pushes.PlanChangedAsync(cancellationToken);
        var gone = outcome.Series ? plan.Where(planned => planned.EventId == alert.EventId).Select(planned => planned.Key) : [alert.Key];
        foreach (var key in gone) await dispatcher.CancelRepeatsAsync(key, cancellationToken);
        return outcome;
    }
}
