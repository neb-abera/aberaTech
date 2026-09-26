using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// When each alert goes off. A reminder on the event wins over the default
/// lead, a recurring event alerts once per occurrence at its own local time
/// on both sides of a clock change, and an all-day, cancelled or declined
/// event does not alert at all.
/// </summary>
public sealed class AlertPlannerTests
{
    /// <summary>06:00 New York time on the morning of the standup.</summary>
    private static readonly Instant Morning = Instant.FromUtc(2026, 10, 28, 10, 0);

    private static readonly Instant StandupStart = Instant.FromUtc(2026, 10, 28, 13, 0);

    private static IReadOnlyList<PlannedAlert> Plan(string feed, Instant? now = null, AlertsOptions? options = null) =>
        AlertPlanner.Plan(feed, now ?? Morning, options ?? new AlertsOptions()).Alerts;

    [Fact]
    public void The_events_own_reminder_sets_the_alert_time()
    {
        var alert = Assert.Single(Plan(Standup(Popup("-P0DT0H15M0S"))));

        Assert.Equal(StandupStart, alert.StartsAt);
        Assert.Equal(StandupStart - Duration.FromMinutes(15), alert.AlertAt);
        Assert.Equal(AlertSource.Reminder, alert.Source);
        Assert.Equal("Standup", alert.Title);
        Assert.Equal("Room 1", alert.Location);
    }

    [Fact]
    public void Without_a_reminder_the_alert_is_the_default_lead_before_the_start()
    {
        var alert = Assert.Single(Plan(Standup()));

        Assert.Equal(StandupStart - Duration.FromMinutes(10), alert.AlertAt);
        Assert.Equal(AlertSource.DefaultLead, alert.Source);
    }

    [Fact]
    public void The_default_lead_is_configuration()
    {
        var alert = Assert.Single(Plan(Standup(), options: new AlertsOptions { DefaultLeadMinutes = 3 }));

        Assert.Equal(StandupStart - Duration.FromMinutes(3), alert.AlertAt);
    }

    [Fact]
    public void A_mail_reminder_is_not_a_phone_alert_and_leaves_the_default_lead()
    {
        var alert = Assert.Single(Plan(Standup(Mail("-P0DT1H0M0S"))));

        Assert.Equal(StandupStart - Duration.FromMinutes(10), alert.AlertAt);
        Assert.Equal(AlertSource.DefaultLead, alert.Source);
    }

    [Fact]
    public void Of_two_popup_reminders_the_earlier_one_is_the_one_alert()
    {
        var alert = Assert.Single(Plan(Standup(Popup("-PT10M"), Popup("-PT30M"), Mail("-PT2H"))));

        Assert.Equal(StandupStart - Duration.FromMinutes(30), alert.AlertAt);
    }

    [Fact]
    public void A_reminder_relative_to_the_end_or_at_a_fixed_time_is_honoured()
    {
        var fromEnd = Assert.Single(Plan(Standup("BEGIN:VALARM\r\nACTION:DISPLAY\r\nTRIGGER;RELATED=END:-PT45M\r\nEND:VALARM")));
        var fixedTime = Assert.Single(Plan(Standup("BEGIN:VALARM\r\nACTION:AUDIO\r\nTRIGGER;VALUE=DATE-TIME:20261028T124000Z\r\nEND:VALARM")));

        // Ends 09:30, so 45 minutes before the end is 08:45.
        Assert.Equal(StandupStart - Duration.FromMinutes(15), fromEnd.AlertAt);
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 12, 40), fixedTime.AlertAt);
    }

    [Fact]
    public void A_recurring_event_alerts_once_per_occurrence_and_an_excluded_date_not_at_all()
    {
        var feed = Ics(Event(
            "daily@google.com", "Daily", "20261026T090000", "20261026T093000",
            extra: ["RRULE:FREQ=DAILY", "EXDATE;TZID=America/New_York:20261029T090000"],
            alarms: [Popup("-PT15M")]));

        var alerts = Plan(feed, options: new AlertsOptions { LookaheadHours = 72 });

        Assert.Equal(
            [
                Instant.FromUtc(2026, 10, 28, 13, 0),
                Instant.FromUtc(2026, 10, 30, 13, 0)
            ],
            alerts.Select(alert => alert.StartsAt));
        Assert.All(alerts, alert => Assert.Equal(alert.StartsAt - Duration.FromMinutes(15), alert.AlertAt));
        Assert.Equal(alerts.Count, alerts.Select(alert => alert.Key).Distinct().Count());
    }

    [Fact]
    public void A_daily_nine_oclock_stays_at_nine_across_the_end_of_daylight_saving()
    {
        // Clocks go back at 02:00 on Sunday 1 November 2026. 09:00 is 13:00
        // UTC on the Saturday and 14:00 UTC on the Sunday and after; an alert
        // computed in UTC, or from the first occurrence's offset, is an hour
        // early from Sunday on.
        var feed = Ics(Event(
            "daily@google.com", "Daily", "20261030T090000", "20261030T093000",
            extra: ["RRULE:FREQ=DAILY"]));

        var alerts = Plan(feed, Instant.FromUtc(2026, 10, 31, 0, 0), new AlertsOptions { LookaheadHours = 72 });

        Assert.Equal(
            [
                Instant.FromUtc(2026, 10, 31, 12, 50),
                Instant.FromUtc(2026, 11, 1, 13, 50),
                Instant.FromUtc(2026, 11, 2, 13, 50)
            ],
            alerts.Select(alert => alert.AlertAt));
    }

    [Fact]
    public void A_reminder_one_day_ahead_across_the_change_is_one_calendar_day()
    {
        // 09:00 on Sunday 1 November, the day the clocks go back, with a
        // reminder one day before: 09:00 on Saturday, which is 25 hours
        // earlier, not 24.
        var feed = Ics(Event(
            "dentist@google.com", "Dentist", "20261101T090000", "20261101T100000", alarms: [Popup("-P1D")]));

        var alert = Assert.Single(Plan(feed, Instant.FromUtc(2026, 10, 31, 0, 0)));

        Assert.Equal(Instant.FromUtc(2026, 11, 1, 14, 0), alert.StartsAt);
        Assert.Equal(Instant.FromUtc(2026, 10, 31, 13, 0), alert.AlertAt);
    }

    [Fact]
    public void A_daily_event_keeps_its_local_time_into_daylight_saving()
    {
        // Clocks go forward at 02:00 on Sunday 8 March 2026.
        var feed = Ics(Event(
            "daily@google.com", "Daily", "20260306T090000", "20260306T093000",
            extra: ["RRULE:FREQ=DAILY"]));

        var alerts = Plan(feed, Instant.FromUtc(2026, 3, 7, 0, 0), new AlertsOptions { LookaheadHours = 48 });

        Assert.Equal(
            [Instant.FromUtc(2026, 3, 7, 14, 0), Instant.FromUtc(2026, 3, 8, 13, 0)],
            alerts.Select(alert => alert.StartsAt));
    }

    [Fact]
    public void An_all_day_event_is_skipped_unless_asked_for()
    {
        var feed = Ics(
            "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20261029\r\nDTEND;VALUE=DATE:20261030\r\nUID:holiday@google.com\r\nSUMMARY:Holiday\r\nEND:VEVENT");

        Assert.Empty(Plan(feed));

        var included = Assert.Single(Plan(feed, options: new AlertsOptions { IncludeAllDay = true }));
        // Midnight in New York, not midnight UTC.
        Assert.Equal(Instant.FromUtc(2026, 10, 29, 4, 0), included.StartsAt);
        Assert.Equal(Instant.FromUtc(2026, 10, 29, 3, 50), included.AlertAt);
    }

    [Fact]
    public void A_cancelled_event_and_a_cancelled_occurrence_do_not_alert()
    {
        var feed = Ics(
            Event("gone@google.com", "Gone", "20261028T110000", extra: ["STATUS:CANCELLED"]),
            Event("daily@google.com", "Daily", "20261028T090000", "20261028T093000", extra: ["RRULE:FREQ=DAILY"]),
            // Google writes one cancelled instance of a series as an override
            // with the series' UID, the instance's RECURRENCE-ID and STATUS.
            "BEGIN:VEVENT\r\nDTSTART;TZID=America/New_York:20261029T090000\r\nDTEND;TZID=America/New_York:20261029T093000\r\n"
            + "RECURRENCE-ID;TZID=America/New_York:20261029T090000\r\nUID:daily@google.com\r\nSUMMARY:Daily\r\n"
            + "STATUS:CANCELLED\r\nEND:VEVENT");

        var alerts = Plan(feed, options: new AlertsOptions { LookaheadHours = 52 });

        Assert.DoesNotContain(alerts, alert => alert.Title == "Gone");
        Assert.Equal(
            [Instant.FromUtc(2026, 10, 28, 13, 0), Instant.FromUtc(2026, 10, 30, 13, 0)],
            alerts.Select(alert => alert.StartsAt));
    }

    [Fact]
    public void A_moved_occurrence_alerts_at_its_new_time()
    {
        var feed = Ics(
            Event("daily@google.com", "Daily", "20261028T090000", "20261028T093000", extra: ["RRULE:FREQ=DAILY;COUNT=2"]),
            "BEGIN:VEVENT\r\nDTSTART;TZID=America/New_York:20261029T110000\r\nDTEND;TZID=America/New_York:20261029T113000\r\n"
            + "RECURRENCE-ID;TZID=America/New_York:20261029T090000\r\nUID:daily@google.com\r\nSUMMARY:Daily, moved\r\n"
            + "STATUS:CONFIRMED\r\nEND:VEVENT");

        var alerts = Plan(feed);

        var moved = Assert.Single(alerts, alert => alert.Title == "Daily, moved");
        Assert.Equal(Instant.FromUtc(2026, 10, 29, 15, 0), moved.StartsAt);
        Assert.DoesNotContain(alerts, alert => alert.StartsAt == Instant.FromUtc(2026, 10, 29, 13, 0));
    }

    [Fact]
    public void An_invitation_the_owner_declined_does_not_alert()
    {
        string Invite(string uid, string status, string who) =>
            Event(uid, uid, "20261028T110000", extra:
            [
                $"ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT={status};CN={who};X-NUM-GUESTS=0:mailto:{who}",
                "ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=ACCEPTED;CN=host@example.test:mailto:host@example.test"
            ]);

        var feed = Ics(
            Invite("declined", "DECLINED", Owner),
            Invite("accepted", "ACCEPTED", Owner),
            Invite("other-declined", "DECLINED", "someone@example.test"),
            Invite("declined-secondary", "DECLINED", "neb@work.example"));

        var titles = Plan(feed, options: new AlertsOptions { OwnerEmails = ["neb@work.example"] })
            .Select(alert => alert.Title);

        Assert.Equal(["accepted", "other-declined"], titles.Order());
    }

    [Fact]
    public void Only_events_that_have_not_started_and_start_within_the_lookahead_are_planned()
    {
        var feed = Ics(
            Event("past", "past", "20261028T050000"),
            Event("now", "now", "20261028T060000"),
            Event("late", "late", "20261028T060500"),
            Event("tomorrow", "tomorrow", "20261029T090000"),
            Event("far", "far", "20261031T090000"));

        var alerts = Plan(feed, options: new AlertsOptions { LookaheadHours = 48 });

        // "late" alerts at 05:55, before now: still planned, because it has
        // not started, and the worker sends it at once.
        Assert.Equal(["late", "tomorrow"], alerts.Select(alert => alert.Title));
        Assert.True(alerts[0].AlertAt < Morning);
    }

    [Fact]
    public void A_time_with_no_zone_is_read_as_new_york_time()
    {
        var feed = Ics("BEGIN:VEVENT\r\nDTSTART:20261028T090000\r\nDTEND:20261028T093000\r\nUID:floating\r\nSUMMARY:Floating\r\nEND:VEVENT");

        Assert.Equal(StandupStart, Assert.Single(Plan(feed)).StartsAt);
    }

    [Fact]
    public void A_new_york_event_on_an_amman_calendar_keeps_its_own_time_and_is_written_in_amman_time()
    {
        // The calendar's default zone is Asia/Amman; this one event was
        // made in New York. The start comes from the event's TZID, 09:00
        // in New York. The text is the calendar's: 16:00 in Amman.
        var feed = IcsIn("Asia/Amman", Event(
            "trip@google.com", "Call from New York", "20261028T090000", "20261028T093000", alarms: [Popup("-PT15M")]));

        var plan = AlertPlanner.Plan(feed, Morning, new AlertsOptions());

        Assert.Equal("Asia/Amman", plan.Zone.Id);
        var alert = Assert.Single(plan.Alerts);
        Assert.Equal(StandupStart, alert.StartsAt);
        Assert.Equal(StandupStart - Duration.FromMinutes(15), alert.AlertAt);
        Assert.Equal("Starts 4:00 PM +03, Wed 28 Oct", AlertText.Message(alert, plan.Zone));
    }

    [Fact]
    public void A_floating_time_is_read_in_the_calendars_zone()
    {
        var feed = IcsIn("Asia/Amman",
            "BEGIN:VEVENT\r\nDTSTART:20261028T160000\r\nUID:floating\r\nSUMMARY:Floating\r\nEND:VEVENT");

        Assert.Equal(StandupStart, Assert.Single(AlertPlanner.Plan(feed, Morning, new AlertsOptions()).Alerts).StartsAt);
    }

    [Fact]
    public void A_feed_that_names_no_zone_falls_back_to_configuration_and_then_to_utc()
    {
        var feed = Ics(Event("a", "a", "20261028T090000")).Replace("X-WR-TIMEZONE:America/New_York\r\n", "");

        Assert.Equal("Asia/Amman", AlertPlanner.Plan(feed, Morning, new AlertsOptions { TimeZone = "Asia/Amman" }).Zone.Id);
        Assert.Equal(DateTimeZone.Utc, AlertPlanner.Plan(feed, Morning, new AlertsOptions()).Zone);
    }

    [Fact]
    public void An_event_with_no_title_still_alerts_and_says_so()
    {
        var feed = Ics("BEGIN:VEVENT\r\nDTSTART:20261028T130000Z\r\nUID:untitled\r\nEND:VEVENT");

        Assert.Equal("(no title)", Assert.Single(Plan(feed)).Title);
    }

    [Fact]
    public void Keys_are_stable_across_reads_and_bounded_in_length()
    {
        var longUid = new string('u', 900) + "@google.com";
        var feed = Ics(Event(longUid, "Long", "20261028T090000"), Event("short@google.com", "Short", "20261028T100000"));

        var first = Plan(feed);
        var second = Plan(feed, Morning + Duration.FromMinutes(5));

        Assert.Equal(first.Select(alert => alert.Key), second.Select(alert => alert.Key));
        Assert.All(first, alert => Assert.InRange(alert.Key.Length, 1, AlertPlanner.MaxKeyLength));
        Assert.StartsWith("short@google.com|20261028T140000Z", first.Single(alert => alert.Title == "Short").Key);
    }

    [Fact]
    public void The_plan_is_in_alert_order()
    {
        var feed = Ics(
            Event("b", "later start, early reminder", "20261028T120000", alarms: [Popup("-PT3H")]),
            Event("a", "earlier start", "20261028T100000"));

        Assert.Equal(["later start, early reminder", "earlier start"], Plan(feed).Select(alert => alert.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<!DOCTYPE html><html><body>Sign in</body></html>")]
    public void Something_that_is_not_a_calendar_is_a_feed_error(string body)
    {
        Assert.Throws<CalendarFeedException>(() => Plan(body));
    }

    [Fact]
    public void An_event_whose_start_cannot_be_read_is_left_out_and_the_rest_still_alert()
    {
        var feed = Ics(
            "BEGIN:VEVENT\r\nDTSTART:not-a-date\r\nUID:broken\r\nSUMMARY:Broken\r\nEND:VEVENT",
            Event("standup@google.com", "Standup", "20261028T090000"));

        Assert.Equal(["Standup"], Plan(feed).Select(alert => alert.Title));
    }

    [Fact]
    public void A_rule_that_never_matches_ends_in_a_feed_error_rather_than_a_hang()
    {
        // The 30th of February never comes.
        var feed = Ics(Event("never", "Never", "20261028T090000", extra: ["RRULE:FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30"]));

        var started = DateTime.UtcNow;
        try
        {
            Assert.Empty(Plan(feed));
        }
        catch (CalendarFeedException)
        {
        }

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Seeded_random_damage_to_a_real_feed_is_planned_or_refused_and_never_anything_else()
    {
        // A property test over the parser, which reads a document from the
        // network: any byte-level damage either plans or raises the one
        // exception the worker reports. Seeded, so a failure reproduces.
        var original = Ics(
            Event("daily@google.com", "Daily", "20261026T090000", "20261026T093000",
                "Room 1", ["RRULE:FREQ=DAILY", "EXDATE;TZID=America/New_York:20261029T090000"], [Popup("-PT15M")]),
            Event("once@google.com", "Once", "20261028T140000", alarms: [Popup("-P0DT0H30M0S"), Mail("-P1D")]));
        var random = new Random(20260926);
        const string alphabet = "0123456789:;=-+TZPDWHMS\r\n ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        for (var round = 0; round < 400; round++)
        {
            var chars = original.ToCharArray();
            for (var edit = random.Next(1, 8); edit > 0; edit--)
            {
                chars[random.Next(chars.Length)] = alphabet[random.Next(alphabet.Length)];
            }

            var damaged = new string(chars);
            try
            {
                var plan = Plan(damaged);
                Assert.All(plan, alert => Assert.True(alert.StartsAt > Morning, $"round {round}"));
            }
            catch (CalendarFeedException)
            {
            }
        }
    }
}
