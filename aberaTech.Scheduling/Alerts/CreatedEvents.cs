using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// An event created through /alerts (the page or a paired phone), as the
/// server stored it when Google answered.
/// </summary>
/// <param name="EventId">Google's iCalUID: the UID the feed will carry.</param>
/// <param name="LeadMinutes">The popup reminder the event was created with.</param>
/// <param name="Critical">Created as an alarm, with #critical in its description.</param>
public sealed record CreatedAlertEvent(
    string EventId,
    string Title,
    string? Location,
    Instant StartsAt,
    Instant EndsAt,
    int LeadMinutes,
    bool Critical);

/// <summary>
/// Google's secret iCal feed can lag the API by minutes to hours. An event
/// created through /alerts is planned from the stored row until the feed
/// carries its UID or it starts, whichever comes first. From then on the
/// feed's copy is the one planned, under the same key, so a skip, a claim
/// or an acknowledgement carries over.
/// </summary>
public static class CreatedEvents
{
    public const int MaxTitleLength = 200;

    public const int MaxLocationLength = 200;

    public const int MinDurationMinutes = 5;

    public const int MaxDurationMinutes = 1440;

    public const int MinLeadMinutes = 0;

    public const int MaxLeadMinutes = 1440;

    /// <summary>How far ahead an event may start.</summary>
    public static readonly Duration MaxAhead = Duration.FromDays(366);

    /// <summary>
    /// The feed's plan with every stored event it does not have yet, in the
    /// same window and order the planner uses. <paramref name="feedIds"/> is
    /// every UID in the last good read.
    /// </summary>
    public static IReadOnlyList<PlannedAlert> Merge(
        IReadOnlyList<PlannedAlert> feed,
        IReadOnlySet<string> feedIds,
        IEnumerable<CreatedAlertEvent> created,
        Instant now,
        AlertSettings settings)
    {
        var horizon = now + settings.Lookahead;
        var keys = feed.Select(alert => alert.Key).ToHashSet(StringComparer.Ordinal);
        var extra = created
            .Where(stored => !feedIds.Contains(stored.EventId) && stored.StartsAt > now && stored.StartsAt <= horizon)
            .Select(Plan)
            .Where(alert => !keys.Contains(alert.Key))
            .ToList();

        return extra.Count == 0
            ? feed
            : [.. feed.Concat(extra).OrderBy(alert => alert.AlertAt).ThenBy(alert => alert.StartsAt).ThenBy(alert => alert.Key, StringComparer.Ordinal)];
    }

    /// <summary>The stored rows that are done: the feed has them, or they have started.</summary>
    public static IReadOnlyList<string> Done(IEnumerable<CreatedAlertEvent> created, IReadOnlySet<string> feedIds, Instant now) =>
        [.. created.Where(stored => feedIds.Contains(stored.EventId) || stored.StartsAt <= now).Select(stored => stored.EventId)];

    /// <summary>One stored event as the planner would plan it from the feed: its popup reminder is the alert.</summary>
    public static PlannedAlert Plan(CreatedAlertEvent stored) =>
        new(
            AlertPlanner.KeyFor(stored.EventId, stored.StartsAt),
            AlertPlanner.DisplayTitle(stored.Title),
            stored.Location,
            stored.StartsAt,
            stored.StartsAt - Duration.FromMinutes(stored.LeadMinutes),
            AlertSource.Reminder,
            AlertPlanner.EventIdFor(stored.EventId),
            stored.Critical,
            stored.EndsAt);
}

/// <summary>
/// What is planned now: the feed's last good read, the stored events it does
/// not have yet, and the stored edits and deletions it does not show yet.
/// </summary>
public static class AlertsPlan
{
    /// <summary>
    /// Read from the database on every call, so a replica that did not take
    /// the create, the edit or the deletion plans it too.
    /// </summary>
    public static async Task<IReadOnlyList<PlannedAlert>> CurrentAsync(
        AlertsSnapshot snapshot, IAlertStore store, AlertSettings settings, Instant now, CancellationToken cancellationToken) =>
        Merge(
            snapshot,
            await store.CreatedEventsAsync(cancellationToken),
            await store.EventChangesAsync(cancellationToken),
            now,
            settings);

    /// <summary>The same plan from rows already read.</summary>
    public static IReadOnlyList<PlannedAlert> Merge(
        AlertsSnapshot snapshot,
        IEnumerable<CreatedAlertEvent> created,
        IEnumerable<AlertEventChange> changes,
        Instant now,
        AlertSettings settings) =>
        EventChanges.Apply(
            CreatedEvents.Merge(snapshot.Plan, snapshot.FeedEventIds, created, now, settings),
            snapshot.Plan,
            changes,
            snapshot.Zone);
}
