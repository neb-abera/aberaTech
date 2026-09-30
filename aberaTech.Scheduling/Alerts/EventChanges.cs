using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// An edit or a deletion made through /alerts, as the server stored it when
/// Google took it. Google's secret iCal feed can lag the API by minutes to
/// hours, so the plan applies these until the feed agrees.
/// </summary>
/// <remarks>
/// An edit keeps the picked occurrence as it is after the change, whole, so
/// it is planned even once its old start has passed and the feed no longer
/// lists it there. The fields for the rest of a series say how every other
/// occurrence changes.
/// </remarks>
/// <param name="EventId">The event's UID, as the plan's EventId carries it.</param>
/// <param name="Series">Every occurrence of the event. False: the one at <paramref name="OriginalStartsAt"/>.</param>
/// <param name="OriginalStartsAt">The start of the occurrence the owner picked, before the change.</param>
/// <param name="Deleted">A deletion. Every field below is null or false for one.</param>
/// <param name="Title">The new title, as typed.</param>
/// <param name="LocationSet">For the rest of a series: the edit set the location. False leaves each occurrence's own.</param>
/// <param name="Location">For the rest of a series: the new location. Null clears it when <paramref name="LocationSet"/> is true.</param>
/// <param name="StartsAt">The picked occurrence's new start.</param>
/// <param name="EndsAt">The picked occurrence's new end. Null when it has none.</param>
/// <param name="DurationMinutes">For the rest of a series: the new length. Null keeps each occurrence's own.</param>
/// <param name="LeadMinutes">For the rest of a series: the new popup reminder. Null keeps each alert the same time before its start.</param>
/// <param name="TimeZone">For a series: the zone its time of day is kept in, the series' own in Google.</param>
/// <param name="AlertAt">The picked occurrence's new alert time.</param>
/// <param name="Reminder">The picked occurrence's alert time comes from a reminder on the event.</param>
/// <param name="Critical">The picked occurrence is marked #critical in the calendar.</param>
/// <param name="Recurring">The picked occurrence belongs to a series.</param>
/// <param name="PickedLocation">The picked occurrence's location after the change.</param>
public sealed record AlertEventChange(
    Guid Id,
    string EventId,
    bool Series,
    Instant OriginalStartsAt,
    bool Deleted,
    string? Title,
    bool LocationSet,
    string? Location,
    Instant? StartsAt,
    Instant? EndsAt,
    int? DurationMinutes,
    int? LeadMinutes,
    string? TimeZone,
    Instant CreatedAt,
    Instant? AlertAt = null,
    bool Reminder = false,
    bool Critical = false,
    bool Recurring = false,
    string? PickedLocation = null)
{
    /// <summary>The later of the two starts: past it, nothing the change hides or moves can alert.</summary>
    public Instant Until => StartsAt is { } moved && moved > OriginalStartsAt ? moved : OriginalStartsAt;

    /// <summary>An occurrence this change is for: the picked one, or for a series, any of the event's.</summary>
    public bool Picks(PlannedAlert alert) =>
        alert.EventId == EventId && (Series || alert.StartsAt == OriginalStartsAt);

    /// <summary>The picked occurrence as it is after an edit. Null for a deletion.</summary>
    public PlannedAlert? Picked() =>
        Deleted || StartsAt is not { } start || AlertAt is not { } alertAt
            ? null
            : new PlannedAlert(
                EventChanges.KeyAt(EventId, start),
                AlertPlanner.DisplayTitle(Title),
                PickedLocation,
                start,
                alertAt,
                Reminder ? AlertSource.Reminder : AlertSource.DefaultLead,
                EventId,
                Critical,
                EndsAt,
                Recurring);

    /// <summary>
    /// An edit of <paramref name="picked"/>, with the picked occurrence as it
    /// will be listed filled in from the rest.
    /// </summary>
    public static AlertEventChange Edit(
        Guid id,
        PlannedAlert picked,
        bool series,
        AlertEventEdit edit,
        string? timeZone,
        DateTimeZone fallback,
        Instant now)
    {
        var change = new AlertEventChange(
            id,
            picked.EventId,
            series,
            picked.StartsAt,
            Deleted: false,
            edit.Title,
            edit.LocationSet,
            edit.LocationSet ? edit.Location : null,
            edit.StartsAt,
            EndsAt: null,
            edit.DurationMinutes,
            edit.LeadMinutes,
            timeZone,
            now);
        var after = EventChanges.Moved(picked, change, fallback);
        return change with
        {
            EndsAt = after.EndsAt,
            AlertAt = after.AlertAt,
            Reminder = after.Source == AlertSource.Reminder,
            Critical = after.Critical,
            Recurring = after.Recurring,
            PickedLocation = after.Location
        };
    }

    /// <summary>A deletion of <paramref name="picked"/>, or of every occurrence of its event.</summary>
    public static AlertEventChange Deletion(Guid id, PlannedAlert picked, bool series, Instant now) =>
        new(id, picked.EventId, series, picked.StartsAt, Deleted: true, null, false, null, null, null, null, null, null, now);
}

/// <summary>
/// Applies the stored edits and deletions to a plan, and says which of them
/// are done. Pure: plans and changes in, a plan or a list of ids out.
/// </summary>
/// <remarks>
/// Edits apply oldest first, then deletions, so a deletion made on an edited
/// occurrence finds it at its new start. An edit the feed already shows is
/// skipped, so a series is never moved twice.
/// </remarks>
public static class EventChanges
{
    /// <summary>
    /// The plan with every change applied, in the planner's order.
    /// <paramref name="feed"/> is the last good read, without the created
    /// events, and tells which edits Google's feed already shows.
    /// </summary>
    public static IReadOnlyList<PlannedAlert> Apply(
        IReadOnlyList<PlannedAlert> plan,
        IReadOnlyList<PlannedAlert> feed,
        IEnumerable<AlertEventChange> changes,
        DateTimeZone zone)
    {
        var ordered = Ordered(changes);
        if (ordered.Count == 0) return plan;

        var deletions = ordered.Where(change => change.Deleted).ToList();
        var kept = Edited(plan, feed, ordered, zone).Where(alert => !deletions.Any(deletion => deletion.Picks(alert)));

        // One occurrence per key, as the planner keeps it.
        var keys = new HashSet<string>(StringComparer.Ordinal);
        return
        [
            .. kept
                .Where(alert => keys.Add(alert.Key))
                .OrderBy(alert => alert.AlertAt)
                .ThenBy(alert => alert.StartsAt)
                .ThenBy(alert => alert.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// The changes that are done. An edit is done once the feed shows the
    /// picked occurrence at its new start with the new title, or both starts
    /// have passed. A deletion is done once both starts have passed, or,
    /// after a good read, once the plan with every edit applied no longer has
    /// the occurrence (or, for a series, any occurrence of the event).
    /// </summary>
    /// <param name="feedRead">A read of the feed has worked in this process. Before one, the plan is empty and proves nothing.</param>
    public static IReadOnlyList<Guid> Done(
        IReadOnlyList<PlannedAlert> plan,
        IReadOnlyList<PlannedAlert> feed,
        IReadOnlyList<AlertEventChange> changes,
        DateTimeZone zone,
        Instant now,
        bool feedRead)
    {
        var done = changes
            .Where(change => !change.Deleted && (change.Until <= now || Agreed(change, feed)))
            .Select(change => change.Id)
            .ToHashSet();

        var edited = Edited(plan, feed, Ordered(changes.Where(change => !change.Deleted && !done.Contains(change.Id))), zone);
        foreach (var deletion in changes.Where(change => change.Deleted))
        {
            if (deletion.Until <= now || (feedRead && !edited.Any(deletion.Picks))) done.Add(deletion.Id);
        }

        return [.. done];
    }

    /// <summary>
    /// The feed shows the edit. For a moved start: the picked occurrence at
    /// its new start with the new title, and nothing at its old start. For
    /// the same start: the picked occurrence with every new value. Applying
    /// an edit of the same start twice changes nothing, so only a move needs
    /// the first test to be right.
    /// </summary>
    public static bool Agreed(AlertEventChange change, IReadOnlyList<PlannedAlert> feed)
    {
        if (change.Picked() is not { } picked) return false;

        var ofEvent = feed.Where(alert => alert.EventId == change.EventId).ToList();
        if (picked.StartsAt != change.OriginalStartsAt)
        {
            return ofEvent.Any(alert => alert.StartsAt == picked.StartsAt && alert.Title == picked.Title)
                   && !ofEvent.Any(alert => alert.StartsAt == change.OriginalStartsAt);
        }

        return ofEvent.Any(alert =>
            alert.StartsAt == picked.StartsAt
            && alert.Title == picked.Title
            && alert.Location == picked.Location
            && alert.EndsAt == picked.EndsAt
            && alert.AlertAt == picked.AlertAt);
    }

    /// <summary>
    /// A series' new start for one occurrence: the picked occurrence's change
    /// in date and wall-clock time, made on this occurrence's wall clock in
    /// the series' zone. A daily 09:00 moved to 10:00 stays at 10:00 on both
    /// sides of a clock change, as Google Calendar's "All events" edit does.
    /// </summary>
    public static Instant Shift(Instant start, Instant pickedFrom, Instant pickedTo, DateTimeZone zone)
    {
        var period = Period.Between(
            pickedFrom.InZone(zone).LocalDateTime,
            pickedTo.InZone(zone).LocalDateTime,
            PeriodUnits.Days | PeriodUnits.AllTimeUnits);
        return zone.AtLeniently(start.InZone(zone).LocalDateTime + period).ToInstant();
    }

    /// <summary>The key of an occurrence at a new start. A hashed UID cannot be keyed again, and Google never takes a write for one.</summary>
    public static string KeyAt(string eventId, Instant start) => AlertPlanner.KeyFor(eventId, start);

    /// <summary>
    /// One occurrence as an edit changes it. The picked one takes the new
    /// start. Another of a series moves by the same change on the series'
    /// wall clock. A new length or lead applies to each, and none keeps
    /// each one's own.
    /// </summary>
    public static PlannedAlert Moved(PlannedAlert alert, AlertEventChange change, DateTimeZone fallback)
    {
        var movedTo = change.StartsAt ?? alert.StartsAt;
        var zone = change.TimeZone is { } id ? DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) ?? fallback : fallback;
        var start = alert.StartsAt == change.OriginalStartsAt ? movedTo : Shift(alert.StartsAt, change.OriginalStartsAt, movedTo, zone);
        Instant? end = change.DurationMinutes is { } minutes
            ? start + Duration.FromMinutes(minutes)
            : alert.EndsAt + (start - alert.StartsAt);
        var (alertAt, source) = change.LeadMinutes is { } lead
            ? (start - Duration.FromMinutes(lead), AlertSource.Reminder)
            : (start - (alert.StartsAt - alert.AlertAt), alert.Source);

        return alert with
        {
            Key = start == alert.StartsAt ? alert.Key : KeyAt(alert.EventId, start),
            Title = AlertPlanner.DisplayTitle(change.Title),
            Location = change.LocationSet ? change.Location : alert.Location,
            StartsAt = start,
            EndsAt = end,
            AlertAt = alertAt,
            Source = source
        };
    }

    private static List<AlertEventChange> Ordered(IEnumerable<AlertEventChange> changes) =>
        [.. changes.OrderBy(change => change.CreatedAt).ThenBy(change => change.Id)];

    /// <summary>
    /// The plan with the edits applied. The picked occurrence is the one the
    /// edit kept, whether or not the feed still lists it at its old start.
    /// </summary>
    private static List<PlannedAlert> Edited(
        IReadOnlyList<PlannedAlert> plan,
        IReadOnlyList<PlannedAlert> feed,
        IReadOnlyList<AlertEventChange> ordered,
        DateTimeZone fallback)
    {
        var alerts = plan.ToList();
        foreach (var change in ordered)
        {
            if (change.Deleted || change.Picked() is not { } picked || Agreed(change, feed)) continue;

            var next = new List<PlannedAlert>(alerts.Count + 1);
            foreach (var alert in alerts)
            {
                if (!change.Picks(alert)) next.Add(alert);
                else if (change.Series && alert.StartsAt != change.OriginalStartsAt) next.Add(Moved(alert, change, fallback));
            }

            if (!next.Any(alert => alert.Key == picked.Key)) next.Add(picked);
            alerts = next;
        }

        return alerts;
    }
}

/// <summary>
/// What an occurrence keeps when an edit moves its start and so its key.
/// The type is kept under the event's UID and needs nothing.
/// </summary>
public static class EventMoves
{
    /// <summary>
    /// Each occurrence the edit is for, before and after. A skip goes with
    /// the occurrence. So does the send claim and the acknowledgement when
    /// the new alert time has already come: the alert went, and the moved
    /// occurrence would otherwise send it a second time at once. A new alert
    /// time still ahead alerts at that time.
    /// </summary>
    public static async Task CarryAsync(
        IReadOnlyList<PlannedAlert> before,
        AlertEventChange change,
        DateTimeZone zone,
        IAlertStore store,
        Instant now,
        CancellationToken cancellationToken)
    {
        foreach (var from in before.Where(change.Picks))
        {
            var to = from.StartsAt == change.OriginalStartsAt && change.Picked() is { } picked
                ? picked
                : EventChanges.Moved(from, change, zone);
            if (from.Key == to.Key) continue;

            if (await store.IsSkippedAsync(from.Key, cancellationToken)) await store.SkipAsync(to.Key, to.StartsAt, now, cancellationToken);
            if (to.AlertAt > now) continue;

            if (await store.DeliveryAsync(from.Key, cancellationToken) is { } delivery
                && await store.TryClaimAsync(to.Key, to.StartsAt, now, cancellationToken))
            {
                await store.RecordOutcomeAsync(to.Key, delivery.Outcome, now, cancellationToken, delivery.Receipt);
            }

            var acknowledged = await store.AcknowledgementsAsync(cancellationToken);
            if (acknowledged.GetValueOrDefault(from.Key) is { } acknowledgement)
            {
                await store.AcknowledgeAsync(to.Key, to.StartsAt, acknowledgement.Via, acknowledgement.At, cancellationToken);
            }
        }
    }
}
