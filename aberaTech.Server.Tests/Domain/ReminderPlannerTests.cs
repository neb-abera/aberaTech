using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

/// <summary>
/// Which reminders a new booking earns, and exactly when each is due. These
/// rows sit in the outbox for hours before anybody can see whether they were
/// right, which is why the decision is pinned here rather than trusted.
/// </summary>
public class ReminderPlannerTests
{
    private static readonly Instant Now = Instant.FromUtc(2027, 6, 1, 14, 0);
    private static readonly Duration DayBefore = Duration.FromHours(24);
    private static readonly Duration HourBefore = Duration.FromHours(1);

    [Fact]
    public void A_booking_days_out_gets_both_reminders_at_their_exact_moments()
    {
        var startsAt = Now + Duration.FromDays(3);

        var planned = ReminderPlanner.Plan(Now, startsAt, DayBefore, HourBefore);

        Assert.Equal(
            [
                new PlannedReminder(NotificationKind.ReminderDayBefore, startsAt - DayBefore),
                new PlannedReminder(NotificationKind.Reminder, startsAt - HourBefore)
            ],
            planned);
    }

    [Fact]
    public void A_booking_later_today_gets_only_the_final_reminder()
    {
        // Ninety minutes out. A "tomorrow" reminder for something this
        // afternoon is noise, and one whose moment has already passed would go
        // out immediately, which is worse.
        var startsAt = Now + Duration.FromMinutes(90);

        var planned = ReminderPlanner.Plan(Now, startsAt, DayBefore, HourBefore);

        Assert.Equal([new PlannedReminder(NotificationKind.Reminder, startsAt - HourBefore)], planned);
    }

    [Fact]
    public void A_booking_about_to_start_gets_no_reminders_at_all()
    {
        // Thirty minutes out: the confirmation they just received is the
        // reminder.
        var planned = ReminderPlanner.Plan(Now, Now + Duration.FromMinutes(30), DayBefore, HourBefore);

        Assert.Empty(planned);
    }

    [Fact]
    public void A_reminder_due_this_very_instant_is_not_queued()
    {
        // Booked exactly one hour ahead. The reminder's moment is now, so
        // queueing it would text them twice in the same breath as the
        // confirmation.
        var planned = ReminderPlanner.Plan(Now, Now + HourBefore, DayBefore, HourBefore);

        Assert.Empty(planned);
    }

    [Fact]
    public void Leads_are_absolute_time_even_across_a_clock_change()
    {
        // 9 AM Eastern on Sunday 7 November 2027, the morning the clocks fall
        // back. Twenty-four absolute hours earlier is 10 AM Eastern on the
        // Saturday — a different wall-clock time, deliberately. The lead is a
        // duration on the instant line, not "the same time yesterday", so a
        // clock change can never make a reminder fire an hour early into a
        // quiet Sunday morning or an hour late after the appointment.
        var startsAt = Instant.FromUtc(2027, 11, 7, 14, 0);

        var planned = ReminderPlanner.Plan(startsAt - Duration.FromDays(3), startsAt, DayBefore, HourBefore);

        Assert.Equal(Instant.FromUtc(2027, 11, 6, 14, 0), planned[0].DueAt);
    }
}
