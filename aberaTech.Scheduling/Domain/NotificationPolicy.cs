using NodaTime;

namespace aberaTech.Scheduling.Domain;

public enum NotificationKind
{
    /// <summary>Confirmation that they hold a place in the queue.</summary>
    Joined = 0,

    /// <summary>Their projected start moved by more than the tolerance.</summary>
    TimeChanged = 1,

    /// <summary>They are close enough that they should be on their way.</summary>
    Imminent = 2,

    /// <summary>They are at the front and the host is ready.</summary>
    YourTurn = 3,

    /// <summary>A booked appointment is confirmed.</summary>
    Booked = 4,

    /// <summary>A booked appointment is coming up tomorrow.</summary>
    ReminderDayBefore = 5,

    /// <summary>A booked appointment is coming up shortly.</summary>
    Reminder = 6,

    /// <summary>Their appointment was cancelled.</summary>
    Cancelled = 7,

    /// <summary>Somebody booked. Sent to the host, not the visitor.</summary>
    HostBooked = 8,

    /// <summary>Somebody cancelled. Sent to the host.</summary>
    HostCancelled = 9
}

/// <summary>
/// What a visitor has already been told, so the policy can avoid telling them
/// again.
/// </summary>
/// <param name="LastAnnouncedStart">
/// The projected start in the most recent message they received. Null before
/// the first one.
/// </param>
public sealed record NotificationState(
    Instant? LastAnnouncedStart = null,
    bool ImminentSent = false,
    bool TurnSent = false);

/// <summary>
/// Decides which messages a queue entry has earned, given what changed and what
/// it has already been told.
/// </summary>
/// <remarks>
/// Two independent reasons to be conservative here, and they happen to agree.
///
/// The first is manners. A queue recomputes on every state change, and a naive
/// implementation texts everybody every time, so a busy afternoon buries
/// twenty-eight people in a hundred messages about two minute drifts.
///
/// The second is money and deliverability. Every message costs roughly a cent
/// and counts against a daily carrier cap that a sole proprietor 10DLC
/// registration sets in the low thousands. Chatter spends the budget that the
/// messages people actually need have to come out of.
///
/// So: announce a time only when it has moved enough to change what somebody
/// would do about it, and send the two milestone messages exactly once each.
/// </remarks>
public static class NotificationPolicy
{
    /// <summary>
    /// The default tolerance for re-announcing a projected start. Ten minutes
    /// is roughly the granularity at which a change is worth acting on, and it
    /// is comfortably larger than the jitter a queue produces as appointments
    /// end a minute or two either side of their estimate.
    /// </summary>
    public static readonly Duration DefaultTolerance = Duration.FromMinutes(10);

    /// <summary>
    /// The default lead time for the "you are up soon" message.
    /// </summary>
    public static readonly Duration DefaultImminentLead = Duration.FromMinutes(10);

    public static IReadOnlyList<NotificationKind> Decide(
        NotificationState state,
        Instant projectedStart,
        Instant now,
        bool isFront,
        Duration? tolerance = null,
        Duration? imminentLead = null)
    {
        var slack = tolerance ?? DefaultTolerance;
        var lead = imminentLead ?? DefaultImminentLead;
        var due = new List<NotificationKind>();

        if (state.LastAnnouncedStart is null)
        {
            due.Add(NotificationKind.Joined);
        }
        else if (Abs(projectedStart - state.LastAnnouncedStart.Value) >= slack)
        {
            due.Add(NotificationKind.TimeChanged);
        }

        // Ordered deliberately: somebody who joins already at the front should
        // get their welcome and their call, in that order, rather than a bare
        // "you're up" from a service they have not heard from yet.
        if (!state.ImminentSent && projectedStart - now <= lead)
        {
            due.Add(NotificationKind.Imminent);
        }

        if (!state.TurnSent && isFront)
        {
            due.Add(NotificationKind.YourTurn);
        }

        return due;
    }

    private static Duration Abs(Duration value) => value < Duration.Zero ? -value : value;
}
