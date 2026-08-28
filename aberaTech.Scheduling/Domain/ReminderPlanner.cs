using NodaTime;

namespace aberaTech.Scheduling.Domain;

/// <summary>One reminder a booking earned, and the exact instant it is due.</summary>
public readonly record struct PlannedReminder(NotificationKind Kind, Instant DueAt);

/// <summary>
/// Decides which reminders a new booking gets.
/// </summary>
/// <remarks>
/// Two reminders, at the two moments they are useful for different things: a
/// day out is the last point at which somebody can still rearrange their day,
/// and an hour out tells them to set off.
///
/// A reminder whose moment has already passed is not planned at all. It would
/// go out immediately, on the heels of the confirmation, and a message that
/// repeats the one before it teaches people to stop reading them.
///
/// Leads are absolute durations on the instant line, not wall-clock
/// arithmetic: "a day before" means twenty-four hours, even when a DST change
/// makes that a different time on the recipient's clock. The alternative —
/// same wall-clock time yesterday — is exactly the calculation that fires an
/// hour early or late twice a year.
/// </remarks>
public static class ReminderPlanner
{
    public static IReadOnlyList<PlannedReminder> Plan(
        Instant now,
        Instant startsAt,
        Duration earlyLead,
        Duration finalLead)
    {
        var planned = new List<PlannedReminder>(2);

        Add(planned, NotificationKind.ReminderDayBefore, startsAt - earlyLead, now);
        Add(planned, NotificationKind.Reminder, startsAt - finalLead, now);

        return planned;
    }

    private static void Add(List<PlannedReminder> planned, NotificationKind kind, Instant dueAt, Instant now)
    {
        if (dueAt <= now)
        {
            return;
        }

        planned.Add(new PlannedReminder(kind, dueAt));
    }
}
