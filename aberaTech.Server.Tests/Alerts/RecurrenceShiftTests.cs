using aberaTech.Scheduling.Alerts;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using NodaTime;
using Xunit;
using IcalCalendar = Ical.Net.Calendar;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// A series' recurrence lines rewritten for a move of its start, so a
/// weekly Tuesday moved to Wednesday repeats on Wednesdays, as Google
/// Calendar's own All events edit does.
/// </summary>
public sealed class RecurrenceShiftTests
{
    private static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];

    /// <summary>A wall-clock time in New York.</summary>
    private static Instant At(int year, int month, int day, int hour, int minute = 0) =>
        NewYork.AtStrictly(new LocalDateTime(year, month, day, hour, minute)).ToInstant();

    private static IReadOnlyList<string>? Rewrite(string[] lines, Instant from, Instant to) =>
        RecurrenceShift.Rewrite(lines, from, to, NewYork);

    // ----- BYDAY -----

    [Fact]
    public void A_weekly_tuesday_moved_a_day_later_repeats_on_wednesday()
    {
        // Tuesday 27 October 2026 09:00 to Wednesday 28 October 09:00.
        var lines = Rewrite(["RRULE:FREQ=WEEKLY;BYDAY=TU"], At(2026, 10, 27, 9), At(2026, 10, 28, 9));

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=WE"], lines);
    }

    [Fact]
    public void Monday_wednesday_and_friday_moved_a_day_earlier_are_sunday_tuesday_and_thursday()
    {
        var lines = Rewrite(["RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR"], At(2026, 10, 26, 9), At(2026, 10, 25, 9));

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=SU,TU,TH"], lines);
    }

    [Theory]
    [InlineData("SU", 2026, 10, 25, 2026, 10, 26, "MO")]
    [InlineData("MO", 2026, 10, 26, 2026, 10, 25, "SU")]
    [InlineData("SA,SU", 2026, 10, 24, 2026, 10, 26, "MO,TU")]
    [InlineData("TU", 2026, 10, 27, 2026, 11, 4, "WE")]
    [InlineData("TU", 2026, 10, 27, 2026, 10, 13, "TU")]
    public void Weekdays_wrap_around_the_week(
        string byDay, int y1, int m1, int d1, int y2, int m2, int d2, string expected)
    {
        var lines = Rewrite([$"RRULE:FREQ=WEEKLY;BYDAY={byDay}"], At(y1, m1, d1, 9), At(y2, m2, d2, 9));

        // A move by whole weeks keeps the days, and the rule is not sent.
        Assert.Equal(expected == byDay ? null : [$"RRULE:FREQ=WEEKLY;BYDAY={expected}"], lines);
    }

    [Fact]
    public void The_second_tuesday_monthly_moved_a_day_later_is_the_second_wednesday()
    {
        // Tuesday 13 October 2026 is the second Tuesday. Wednesday the 14th is the second Wednesday.
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYDAY=2TU"], At(2026, 10, 13, 9), At(2026, 10, 14, 9));

        Assert.Equal(["RRULE:FREQ=MONTHLY;BYDAY=2WE"], lines);
    }

    [Fact]
    public void An_ordinal_that_named_the_start_takes_the_new_starts_ordinal()
    {
        // July 2026 starts on a Wednesday. Tuesday the 14th is the second
        // Tuesday, and Wednesday the 15th is the third Wednesday.
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYDAY=2TU"], At(2026, 7, 14, 9), At(2026, 7, 15, 9));

        Assert.Equal(["RRULE:FREQ=MONTHLY;BYDAY=3WE"], lines);
    }

    [Fact]
    public void The_last_friday_moved_a_day_later_is_the_last_saturday()
    {
        // Friday 30 October 2026 is the last Friday. Saturday the 31st is the last Saturday.
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYDAY=-1FR"], At(2026, 10, 30, 9), At(2026, 10, 31, 9));

        Assert.Equal(["RRULE:FREQ=MONTHLY;BYDAY=-1SA"], lines);
    }

    [Fact]
    public void Ordinals_that_do_not_name_the_start_are_kept_and_their_days_move()
    {
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYDAY=1TU,3TU"], At(2026, 10, 6, 9), At(2026, 10, 7, 9));

        Assert.Equal(["RRULE:FREQ=MONTHLY;BYDAY=1WE,3WE"], lines);
    }

    [Fact]
    public void A_yearly_ordinal_in_a_month_follows_the_start()
    {
        // The second Sunday of March 2027 is the 14th. Monday the 15th is the third Monday.
        var lines = Rewrite(["RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU"], At(2027, 3, 14, 9), At(2027, 3, 15, 9));

        Assert.Equal(["RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=3MO"], lines);
    }

    // ----- BYMONTHDAY and BYMONTH -----

    [Fact]
    public void The_fifteenth_monthly_moved_a_day_later_is_the_sixteenth()
    {
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYMONTHDAY=15"], At(2026, 10, 15, 9), At(2026, 10, 16, 9));

        Assert.Equal(["RRULE:FREQ=MONTHLY;BYMONTHDAY=16"], lines);
    }

    [Fact]
    public void The_thirty_first_moved_a_day_later_is_the_first()
    {
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYMONTHDAY=31"], At(2026, 10, 31, 9), At(2026, 11, 1, 9));

        Assert.Equal(["RRULE:FREQ=MONTHLY;BYMONTHDAY=1"], lines);
    }

    [Theory]
    [InlineData(2026, 10, 30, "-2")]
    [InlineData(2026, 11, 1, "1")]
    public void The_last_day_stays_counted_from_the_end_while_it_stays_in_its_month(int year, int month, int day, string expected)
    {
        var lines = Rewrite(["RRULE:FREQ=MONTHLY;BYMONTHDAY=-1"], At(2026, 10, 31, 9), At(year, month, day, 9));

        Assert.Equal([$"RRULE:FREQ=MONTHLY;BYMONTHDAY={expected}"], lines);
    }

    [Fact]
    public void Several_month_days_move_together_and_are_clamped_to_the_month()
    {
        Assert.Equal(
            ["RRULE:FREQ=MONTHLY;BYMONTHDAY=3,17"],
            Rewrite(["RRULE:FREQ=MONTHLY;BYMONTHDAY=1,15"], At(2026, 10, 1, 9), At(2026, 10, 3, 9)));
        Assert.Equal(
            ["RRULE:FREQ=MONTHLY;BYMONTHDAY=31"],
            Rewrite(["RRULE:FREQ=MONTHLY;BYMONTHDAY=29,30"], At(2026, 10, 29, 9), At(2026, 11, 1, 9)));
    }

    [Fact]
    public void A_yearly_date_moved_into_the_next_month_takes_its_month_and_day()
    {
        var lines = Rewrite(["RRULE:FREQ=YEARLY;BYMONTH=3;BYMONTHDAY=31"], At(2027, 3, 31, 9), At(2027, 4, 1, 9));

        Assert.Equal(["RRULE:FREQ=YEARLY;BYMONTH=4;BYMONTHDAY=1"], lines);
    }

    [Fact]
    public void A_month_day_in_a_weekly_rule_is_left_alone()
    {
        Assert.Null(Rewrite(["RRULE:FREQ=WEEKLY;BYMONTHDAY=15"], At(2026, 10, 15, 9), At(2026, 10, 16, 9)));
    }

    // ----- Rules that follow DTSTART -----

    [Theory]
    [InlineData("RRULE:FREQ=DAILY;COUNT=7")]
    [InlineData("RRULE:FREQ=WEEKLY")]
    [InlineData("RRULE:FREQ=WEEKLY;INTERVAL=2;UNTIL=20261231T235959Z")]
    [InlineData("RRULE:FREQ=MONTHLY")]
    [InlineData("RRULE:FREQ=YEARLY")]
    public void A_rule_that_takes_its_days_from_the_start_is_not_rewritten(string rule)
    {
        Assert.Null(Rewrite([rule], At(2026, 10, 27, 9), At(2026, 10, 28, 9)));
    }

    [Fact]
    public void A_move_within_the_day_sends_no_recurrence()
    {
        Assert.Null(Rewrite(["RRULE:FREQ=WEEKLY;BYDAY=TU;COUNT=5"], At(2026, 10, 27, 9), At(2026, 10, 27, 11, 30)));
    }

    [Fact]
    public void A_move_to_the_same_start_changes_nothing()
    {
        Assert.Null(Rewrite(
            ["RRULE:FREQ=WEEKLY;BYDAY=TU", "EXDATE;TZID=America/New_York:20261103T090000"],
            At(2026, 10, 27, 9), At(2026, 10, 27, 9)));
    }

    [Fact]
    public void Until_count_interval_and_week_start_are_kept()
    {
        Assert.Equal(
            ["RRULE:FREQ=WEEKLY;UNTIL=20261231T235959Z;INTERVAL=2;BYDAY=WE;WKST=SU"],
            Rewrite(["RRULE:FREQ=WEEKLY;UNTIL=20261231T235959Z;INTERVAL=2;BYDAY=TU;WKST=SU"], At(2026, 10, 27, 9), At(2026, 10, 28, 9)));
        Assert.Equal(
            ["RRULE:FREQ=WEEKLY;COUNT=10;BYDAY=WE"],
            Rewrite(["RRULE:FREQ=WEEKLY;COUNT=10;BYDAY=TU"], At(2026, 10, 27, 9), At(2026, 10, 28, 9)));
    }

    // ----- EXDATE and RDATE -----

    [Fact]
    public void A_cancelled_occurrence_moves_with_the_series()
    {
        var lines = Rewrite(
            ["RRULE:FREQ=WEEKLY;BYDAY=TU", "EXDATE;TZID=America/New_York:20261103T090000,20261110T090000"],
            At(2026, 10, 27, 9), At(2026, 10, 28, 9));

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=WE", "EXDATE;TZID=America/New_York:20261104T090000,20261111T090000"], lines);
    }

    [Fact]
    public void A_utc_exdate_moves_on_the_series_wall_clock_across_the_end_of_daylight_time()
    {
        // Tuesday 3 November 09:00 EST is 14:00 UTC. A day and an hour later
        // on the wall clock is Wednesday 10:00 EST, 15:00 UTC.
        var lines = Rewrite(
            ["RRULE:FREQ=WEEKLY;BYDAY=TU", "EXDATE:20261103T140000Z", "RDATE;VALUE=DATE:20261201"],
            At(2026, 10, 27, 9), At(2026, 10, 28, 10));

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=WE", "EXDATE:20261104T150000Z", "RDATE;VALUE=DATE:20261202"], lines);
    }

    [Fact]
    public void A_time_move_moves_a_cancelled_occurrence_and_keeps_the_rule()
    {
        // The cancelled 09:00 would no longer match the series at 10:00, and come back.
        var lines = Rewrite(
            ["RRULE:FREQ=WEEKLY;BYDAY=TU", "EXDATE;TZID=America/New_York:20261103T090000"],
            At(2026, 10, 27, 9), At(2026, 10, 27, 10));

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=TU", "EXDATE;TZID=America/New_York:20261103T100000"], lines);
    }

    [Fact]
    public void A_period_rdate_and_lines_it_cannot_read_are_kept()
    {
        var lines = Rewrite(
            ["RRULE:FREQ=WEEKLY;BYDAY=TU", "RDATE;VALUE=PERIOD:20261201T140000Z/PT1H", "EXDATE:not-a-date"],
            At(2026, 10, 27, 9), At(2026, 10, 28, 9));

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=WE", "RDATE;VALUE=PERIOD:20261201T140000Z/PT1H", "EXDATE:not-a-date"], lines);
    }

    // ----- The series' clock -----

    [Fact]
    public void The_day_count_is_the_series_clock_not_utc()
    {
        // Tuesday 3 November 20:30 EST is Wednesday 01:30 UTC. Wednesday
        // 18:00 EST is Wednesday 23:00 UTC: the same UTC date, a day later in New York.
        Assert.Equal(
            ["RRULE:FREQ=WEEKLY;BYDAY=WE"],
            Rewrite(["RRULE:FREQ=WEEKLY;BYDAY=TU"], At(2026, 11, 3, 20, 30), At(2026, 11, 4, 18)));
        // Tuesday 18:00 EST to 20:00 EST crosses midnight in UTC and not in New York.
        Assert.Null(Rewrite(["RRULE:FREQ=WEEKLY;BYDAY=TU"], At(2026, 11, 3, 18), At(2026, 11, 3, 20)));
    }

    [Fact]
    public void A_move_across_the_end_of_daylight_time_counts_wall_clock_days()
    {
        // Saturday 31 October 09:00 EDT to Sunday 1 November 09:00 EST: 25
        // hours of elapsed time, one day on the wall clock.
        Assert.Equal(
            ["RRULE:FREQ=WEEKLY;BYDAY=SU"],
            Rewrite(["RRULE:FREQ=WEEKLY;BYDAY=SA"], At(2026, 10, 31, 9), At(2026, 11, 1, 9)));
    }

    // ----- Property: the weekdays follow the start -----

    /// <summary>
    /// For every weekly BYDAY set and a shift of D days, the first 21
    /// occurrences of the rewritten rule, expanded by Ical.Net from the
    /// moved start, fall on exactly the original weekdays moved by D, and
    /// the moved start is the first of them. The reference week holds the
    /// end of daylight time in New York.
    /// </summary>
    [Theory]
    [InlineData(-8)]
    [InlineData(-7)]
    [InlineData(-6)]
    [InlineData(-5)]
    [InlineData(-4)]
    [InlineData(-3)]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(30)]
    public void Every_weekly_set_moved_by_any_days_repeats_on_the_moved_weekdays(int days)
    {
        string[] names = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"];
        var failures = new List<string>();
        for (var mask = 1; mask < 128; mask++)
        {
            var set = Enumerable.Range(0, 7).Where(day => (mask & (1 << day)) != 0).ToList();
            // Monday 26 October 2026 09:00 plus the first day in the set, so the start is an occurrence.
            var local = new LocalDateTime(2026, 10, 26, 9, 0).PlusDays(set[0]);
            var from = NewYork.AtStrictly(local).ToInstant();
            var to = NewYork.AtStrictly(local.PlusDays(days)).ToInstant();
            string[] lines = [$"RRULE:FREQ=WEEKLY;BYDAY={string.Join(',', set.Select(day => names[day]))}"];

            var original = Weekdays(Expand(from, lines, 21));
            var rewritten = Rewrite(lines, from, to) ?? lines;
            var moved = Expand(to, rewritten, 21);
            var expected = set.Select(day => (((day + days) % 7) + 7) % 7).ToHashSet();

            if (!original.SetEquals(set) || !Weekdays(moved).SetEquals(expected) || moved[0] != to)
            {
                failures.Add($"{lines[0]} by {days}: {string.Join(';', rewritten)}");
            }
        }

        Assert.Empty(failures);
    }

    private static HashSet<int> Weekdays(IEnumerable<Instant> starts) =>
        [.. starts.Select(start => (int)start.InZone(NewYork).DayOfWeek - 1)];

    /// <summary>The first starts of a series in New York, as Ical.Net expands its lines.</summary>
    private static List<Instant> Expand(Instant start, IEnumerable<string> lines, int count)
    {
        var ics = string.Join("\r\n",
        [
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//aberaTech//Tests//EN",
            "BEGIN:VEVENT",
            "UID:property",
            $"DTSTART;TZID=America/New_York:{start.InZone(NewYork).LocalDateTime:yyyyMMdd'T'HHmmss}",
            .. lines,
            "END:VEVENT",
            "END:VCALENDAR",
            ""
        ]);
        return
        [
            .. IcalCalendar.Load(ics)!.GetOccurrences<CalendarEvent>(new CalDateTime(start.ToDateTimeUtc(), "UTC"))
                .Take(count)
                .Select(occurrence => Instant.FromDateTimeUtc(DateTime.SpecifyKind(occurrence.Period.StartTime.AsUtc, DateTimeKind.Utc)))
        ];
    }
}
