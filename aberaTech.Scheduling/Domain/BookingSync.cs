using aberaTech.Scheduling.Calendar;
using NodaTime;

namespace aberaTech.Scheduling.Domain;

/// <summary>What a booking does about its Google Calendar event.</summary>
public enum BookingSyncAction
{
    Keep = 0,

    /// <summary>The visitor declined the invite. The booking is canceled and the event withdrawn.</summary>
    CancelDeclined = 1,

    /// <summary>The host deleted the event. The booking is canceled.</summary>
    CancelDeleted = 2,

    /// <summary>The host moved the event. The booking and its reminders move with it.</summary>
    Move = 3
}

public readonly record struct BookingSyncDecision(BookingSyncAction Action, Instant StartsAt, Instant EndsAt);

/// <summary>
/// Decides what a booking does when its calendar event differs from it.
/// </summary>
/// <remarks>
/// Google Calendar is where a visitor declines or proposes a new time, and
/// where the host accepts a proposal by moving the event. The booking follows
/// the event, so the page's free times and the reminder texts agree with the
/// calendar. Pure, so every case is asserted without a database or a network.
/// </remarks>
public static class BookingSync
{
    public static BookingSyncDecision Decide(Instant startsAt, Instant endsAt, BookingEvent google)
    {
        switch (google.State)
        {
            case BookingEventState.Deleted:
                return new(BookingSyncAction.CancelDeleted, startsAt, endsAt);

            case BookingEventState.Scheduled:
                // A decline wins over a move: a meeting the visitor will not
                // attend is off whatever time the event now shows.
                if (google.Declined)
                {
                    return new(BookingSyncAction.CancelDeclined, startsAt, endsAt);
                }

                return google.StartsAt == startsAt && google.EndsAt == endsAt
                    ? new(BookingSyncAction.Keep, startsAt, endsAt)
                    : new(BookingSyncAction.Move, google.StartsAt, google.EndsAt);

            default:
                // No answer is no reason to change anything.
                return new(BookingSyncAction.Keep, startsAt, endsAt);
        }
    }
}
