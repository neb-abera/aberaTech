using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A countdown's event on the Google Calendar the alerts read: made with the
/// countdown, moved and renamed with it, and deleted with it. The event
/// starts at the target and lasts <see cref="AlertCountdowns.EventLength"/>.
/// Its one popup reminder is at the start. Its type on /alerts is alarm when
/// the countdown's Alarm is on and none when it is off, so an alarm rings
/// at the target through the same planner as any calendar alarm.
/// </summary>
public static class CountdownEvents
{
    /// <summary>The type stored for the countdown's event.</summary>
    public static string TypeFor(bool alarm) => alarm ? AlertTypes.Alarm : AlertTypes.None;

    /// <summary>The event Google is asked to make for a countdown.</summary>
    public static NewAlertEvent NewEvent(AlertCountdown countdown) =>
        new(countdown.Label, null, countdown.TargetAt, countdown.TargetAt + AlertCountdowns.EventLength, 0, TypeFor(countdown.Alarm));

    /// <summary>
    /// Makes the countdown's event, stores it so it is planned before the
    /// feed carries it, and stores its type. The countdown with the event's
    /// UID, or why Google did not make it.
    /// </summary>
    public static async Task<(AlertCountdown? Countdown, CreateOutcome Outcome)> CreateAsync(
        AlertCountdown countdown, GoogleAlertEvents google, IAlertStore store, Instant now, CancellationToken cancellationToken)
    {
        var outcome = await google.CreateAsync(NewEvent(countdown), cancellationToken);
        if (outcome.Event is not { } created) return (null, outcome);

        await store.AddCreatedEventAsync(created, now, cancellationToken);
        await store.SetEventTypeAsync(AlertPlanner.EventIdFor(created.EventId), TypeFor(countdown.Alarm), now, cancellationToken);
        return (countdown with { CalendarEventId = created.EventId }, outcome);
    }

    /// <summary>
    /// Brings the event in line with the edited countdown. A countdown with
    /// no event, or whose event was deleted on Google, gets a new one. The
    /// countdown to store with its event's UID, or the problem that stopped
    /// the write, with whether it is a conflict (409) or Google's (502).
    /// MarkProblem says the alarm's #critical mark was not written. The
    /// stored type already rings it.
    /// </summary>
    public static async Task<(AlertCountdown? Countdown, string? Problem, bool Conflict, string? MarkProblem)> UpdateAsync(
        AlertCountdown saved,
        AlertCountdown next,
        AlertsSnapshot snapshot,
        AlertSettings settings,
        GoogleAlertEvents google,
        IAlertStore store,
        Instant now,
        CancellationToken cancellationToken)
    {
        if (saved.CalendarEventId is not { } uid) return Created(await CreateAsync(next, google, store, now, cancellationToken));

        if (saved.Label != next.Label || saved.TargetAt != next.TargetAt)
        {
            var plan = await AlertsPlan.CurrentAsync(snapshot, store, settings, now, cancellationToken);
            var edit = new AlertEventEdit(next.Label, false, null, next.TargetAt, null, null);
            var outcome = await AlertEventWrites.EditAsync(
                Occurrence(saved, uid, plan), plan, false, edit, Zone(next, snapshot), google, store, now, cancellationToken);
            if (outcome.Problem == CalendarWriteMessages.NotFound)
            {
                return Created(await CreateAsync(next, google, store, now, cancellationToken));
            }

            if (outcome.Problem is not null) return (null, outcome.Problem, outcome.Conflict, null);
        }

        var eventId = AlertPlanner.EventIdFor(uid);
        await store.SetEventTypeAsync(eventId, TypeFor(next.Alarm), now, cancellationToken);
        var mark = saved.Alarm == next.Alarm ? null : await google.SetMarkAsync(uid, next.Alarm, cancellationToken);
        return (next with { CalendarEventId = uid }, null, false, mark);
    }

    /// <summary>
    /// Deletes the countdown's event. Null when it is gone, or there never
    /// was one. Otherwise the outcome that says why Google kept it.
    /// </summary>
    public static async Task<EventWriteOutcome?> DeleteAsync(
        AlertCountdown saved,
        AlertsSnapshot snapshot,
        AlertSettings settings,
        GoogleAlertEvents google,
        IAlertStore store,
        AlertDispatcher dispatcher,
        AlertPushWorker pushes,
        Instant now,
        CancellationToken cancellationToken)
    {
        if (saved.CalendarEventId is not { } uid) return null;

        var plan = await AlertsPlan.CurrentAsync(snapshot, store, settings, now, cancellationToken);
        var outcome = await AlertEventWrites.DeleteAsync(
            Occurrence(saved, uid, plan), plan, false, google, store, dispatcher, pushes, now, cancellationToken);
        if (outcome.Problem is not null && outcome.Problem != CalendarWriteMessages.NotFound) return outcome;

        await store.SetEventTypeAsync(AlertPlanner.EventIdFor(uid), null, now, cancellationToken);
        return null;
    }

    /// <summary>
    /// The event as the plan holds it, or as the plan would hold it when its
    /// target is outside the window or the feed has not listed it yet.
    /// </summary>
    public static PlannedAlert Occurrence(AlertCountdown saved, string uid, IReadOnlyList<PlannedAlert> plan)
    {
        var eventId = AlertPlanner.EventIdFor(uid);
        return plan.FirstOrDefault(alert => alert.EventId == eventId && alert.StartsAt == saved.TargetAt)
               ?? new PlannedAlert(
                   AlertPlanner.KeyFor(uid, saved.TargetAt),
                   AlertPlanner.DisplayTitle(saved.Label),
                   null,
                   saved.TargetAt,
                   saved.TargetAt,
                   AlertSource.Reminder,
                   eventId,
                   saved.Alarm,
                   saved.TargetAt + AlertCountdowns.EventLength);
    }

    private static DateTimeZone Zone(AlertCountdown countdown, AlertsSnapshot snapshot) =>
        DateTimeZoneProviders.Tzdb.GetZoneOrNull(countdown.TimeZone) ?? snapshot.Zone;

    private static (AlertCountdown?, string?, bool, string?) Created((AlertCountdown? Countdown, CreateOutcome Outcome) made) =>
        (made.Countdown, made.Outcome.Problem, made.Outcome.Conflict, null);
}
