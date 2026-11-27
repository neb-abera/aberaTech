using System.Diagnostics;
using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The rings of a routine alarm that repeats every N units, and of one on
/// weekdays every N weeks: elapsed minutes and hours with each ring's own
/// stamp, both clock changes, days to years on the clock, the months and
/// years a date skips, the weeks counted from the start date, a series
/// from long ago, and the save.
/// </summary>
public sealed class RoutineRepeatRingsTests
{
    private static readonly DateTimeZone Amman = DateTimeZoneProviders.Tzdb["Asia/Amman"];
    private static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    private static readonly Duration ThreeHours = Duration.FromHours(3);
    private static readonly Guid Id = Guid.Parse("0D8C6F1E-3F6E-4A53-9D53-8F1B2A7C4D10");
    private static readonly Instant LongAgo = Instant.FromUtc(1980, 1, 1, 0, 0);

    private static AlertRoutine Interval(int hour, int minute, int every, string unit, LocalDate startsOn, Instant? updatedAt = null) =>
        new(Id, "Meds", hour, minute, [], true, 9, updatedAt ?? LongAgo)
        {
            Repeat = RoutineRepeats.Interval,
            Every = every,
            Unit = unit,
            StartsOn = startsOn,
        };

    private static AlertRoutine Weekdays(int hour, int minute, int[] days, int every, LocalDate? startsOn) =>
        new(Id, "Gym", hour, minute, days, true, 9, LongAgo)
        {
            Every = every,
            StartsOn = startsOn,
        };

    private static IReadOnlyList<RoutineRing> Plan(AlertRoutine routine, DateTimeZone zone, Instant from, Instant until) =>
        RoutineRings.Plan([routine], zone, from, until, ThreeHours);

    /// <summary>"2026-11-25T22:00": the stamp at the end of each ring's key.</summary>
    private static string[] Stamps(IEnumerable<RoutineRing> rings) =>
        [.. rings.Select(ring => ring.Key[(RoutineRings.KeyPrefix.Length + 37)..])];

    // ------------------------------------------------------------------ minutes and hours

    [Fact]
    public void Every_97_minutes_rings_in_elapsed_time_and_each_stamp_is_that_rings_own_date_and_time()
    {
        // Amman is UTC+3 all year. The series starts at 22:00 on Wednesday 25 November 2026.
        var routine = Interval(22, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 25, 18, 0), Instant.FromUtc(2026, 11, 26, 0, 0));

        Assert.Equal(
            [
                Instant.FromUtc(2026, 11, 25, 19, 0), Instant.FromUtc(2026, 11, 25, 20, 37),
                Instant.FromUtc(2026, 11, 25, 22, 14), Instant.FromUtc(2026, 11, 25, 23, 51)
            ],
            rings.Select(ring => ring.AlertAt));
        Assert.Equal(["2026-11-25T22:00", "2026-11-25T23:37", "2026-11-26T01:14", "2026-11-26T02:51"], Stamps(rings));
        Assert.Equal("routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-11-26T01:14", rings[2].Key);
        Assert.Equal(4, rings.Select(ring => ring.Key).Distinct().Count());
    }

    [Fact]
    public void Every_2_hours_keeps_2_elapsed_hours_when_the_clocks_go_forward()
    {
        // New York, Sunday 14 March 2027: 02:00 EST becomes 03:00 EDT.
        var routine = Interval(22, 0, 2, RoutineRepeats.Hours, new LocalDate(2027, 3, 13));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2027, 3, 14, 0, 0), Instant.FromUtc(2027, 3, 14, 11, 0));

        Assert.Equal(
            [3, 5, 7, 9, 11],
            rings.Select(ring => ring.AlertAt.InUtc().Hour));
        Assert.Equal(
            ["2027-03-13T22:00", "2027-03-14T00:00", "2027-03-14T03:00", "2027-03-14T05:00", "2027-03-14T07:00"],
            Stamps(rings));
    }

    [Fact]
    public void Every_2_hours_keeps_2_elapsed_hours_when_the_clocks_go_back_and_drops_nothing()
    {
        // New York, Sunday 1 November 2026: 02:00 EDT becomes 01:00 EST.
        var routine = Interval(21, 0, 2, RoutineRepeats.Hours, new LocalDate(2026, 10, 31));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 0, 0), Instant.FromUtc(2026, 11, 1, 9, 0));

        Assert.Equal([1, 3, 5, 7, 9], rings.Select(ring => ring.AlertAt.InUtc().Hour));
        Assert.Equal(
            ["2026-10-31T21:00", "2026-10-31T23:00", "2026-11-01T01:00", "2026-11-01T02:00", "2026-11-01T04:00"],
            Stamps(rings));
    }

    [Fact]
    public void Every_30_minutes_drops_the_rings_whose_stamp_an_earlier_ring_has_when_the_clocks_go_back()
    {
        // 01:00 and 01:30 happen twice. The second pass of each would repeat a stamp.
        var routine = Interval(0, 0, 30, RoutineRepeats.Minutes, new LocalDate(2026, 11, 1));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 4, 0), Instant.FromUtc(2026, 11, 1, 7, 30));

        Assert.Equal(
            [
                Instant.FromUtc(2026, 11, 1, 4, 0), Instant.FromUtc(2026, 11, 1, 4, 30),
                Instant.FromUtc(2026, 11, 1, 5, 0), Instant.FromUtc(2026, 11, 1, 5, 30),
                Instant.FromUtc(2026, 11, 1, 7, 0), Instant.FromUtc(2026, 11, 1, 7, 30)
            ],
            rings.Select(ring => ring.AlertAt));
        Assert.Equal(
            ["2026-11-01T00:00", "2026-11-01T00:30", "2026-11-01T01:00", "2026-11-01T01:30", "2026-11-01T02:00", "2026-11-01T02:30"],
            Stamps(rings));
    }

    [Fact]
    public void Every_45_minutes_drops_nothing_when_the_clocks_go_back_because_no_stamp_repeats()
    {
        var routine = Interval(0, 0, 45, RoutineRepeats.Minutes, new LocalDate(2026, 11, 1));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 4, 0), Instant.FromUtc(2026, 11, 1, 7, 0));

        Assert.Equal(
            ["2026-11-01T00:00", "2026-11-01T00:45", "2026-11-01T01:30", "2026-11-01T01:15", "2026-11-01T02:00"],
            Stamps(rings));
        Assert.All(rings.Zip(rings.Skip(1)), pair => Assert.Equal(Duration.FromMinutes(45), pair.Second.AlertAt - pair.First.AlertAt));
    }

    [Fact]
    public void A_start_the_clocks_pass_twice_is_the_first_pass_and_an_hourly_series_drops_the_second()
    {
        var routine = Interval(1, 30, 1, RoutineRepeats.Hours, new LocalDate(2026, 11, 1));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 0, 0), Instant.FromUtc(2026, 11, 1, 8, 0));

        // 01:30 EDT at 05:30 UTC. 01:30 EST at 06:30 UTC has the same stamp and is dropped.
        Assert.Equal([Instant.FromUtc(2026, 11, 1, 5, 30), Instant.FromUtc(2026, 11, 1, 7, 30)], rings.Select(ring => ring.AlertAt));
        Assert.Equal(["2026-11-01T01:30", "2026-11-01T02:30"], Stamps(rings));
    }

    [Fact]
    public void Every_2_hours_keeps_a_ring_in_the_second_pass_when_the_first_pass_had_none()
    {
        // From 00:30 EDT. 01:30 EST is a ring. 01:30 EDT, an hour before it, was not.
        var routine = Interval(0, 30, 2, RoutineRepeats.Hours, new LocalDate(2026, 11, 1));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 4, 0), Instant.FromUtc(2026, 11, 1, 9, 0));

        Assert.Equal(
            [Instant.FromUtc(2026, 11, 1, 4, 30), Instant.FromUtc(2026, 11, 1, 6, 30), Instant.FromUtc(2026, 11, 1, 8, 30)],
            rings.Select(ring => ring.AlertAt));
        Assert.Equal(["2026-11-01T00:30", "2026-11-01T01:30", "2026-11-01T03:30"], Stamps(rings));
    }

    [Fact]
    public void The_drop_does_not_look_at_the_save_so_a_save_between_the_passes_brings_no_ring_back()
    {
        // Saved at 01:45 EDT. 01:00 and 01:30 rang before the save, and their second pass stays dropped.
        var routine = Interval(0, 0, 30, RoutineRepeats.Minutes, new LocalDate(2026, 11, 1), Instant.FromUtc(2026, 11, 1, 5, 45));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 4, 0), Instant.FromUtc(2026, 11, 1, 7, 30));

        Assert.Equal([Instant.FromUtc(2026, 11, 1, 7, 0), Instant.FromUtc(2026, 11, 1, 7, 30)], rings.Select(ring => ring.AlertAt));
        Assert.Equal(["2026-11-01T02:00", "2026-11-01T02:30"], Stamps(rings));
    }

    [Fact]
    public void A_start_the_clocks_skip_is_the_next_time_that_exists_and_the_first_stamp_reads_that_time()
    {
        // New York, Sunday 14 March 2027: 02:30 does not exist. The series starts at 03:00 EDT.
        var routine = Interval(2, 30, 90, RoutineRepeats.Minutes, new LocalDate(2027, 3, 14));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2027, 3, 14, 0, 0), Instant.FromUtc(2027, 3, 14, 10, 0));

        Assert.Equal(
            [Instant.FromUtc(2027, 3, 14, 7, 0), Instant.FromUtc(2027, 3, 14, 8, 30), Instant.FromUtc(2027, 3, 14, 10, 0)],
            rings.Select(ring => ring.AlertAt));
        Assert.Equal(["2027-03-14T03:00", "2027-03-14T04:30", "2027-03-14T06:00"], Stamps(rings));
    }

    [Fact]
    public void A_stored_routine_that_lost_its_start_date_never_rings_and_nothing_throws()
    {
        var from = Instant.FromUtc(2026, 11, 20, 0, 0);
        var until = from + Duration.FromDays(30);
        AlertRoutine[] broken =
        [
            .. RoutineRepeats.Units.Select(unit => new AlertRoutine(Id, "Meds", 6, 30, [], true, 9, LongAgo)
            {
                Repeat = RoutineRepeats.Interval, Every = 3, Unit = unit,
            }),
            Weekdays(6, 30, [1, 3, 5], 2, null),
            // A unit this build does not know.
            Interval(6, 30, 3, "moons", new LocalDate(2026, 11, 25)),
        ];

        Assert.All(broken, routine =>
        {
            Assert.Empty(Plan(routine, NewYork, from, until));
            Assert.Null(RoutineCompletions.CycleAt(routine, "2026-11-25T06:30", NewYork, from + Duration.FromDays(6)));
            Assert.False(routine.RingsOnce);
        });
    }

    [Fact]
    public void Weekdays_every_2_weeks_ring_nothing_at_or_before_the_save()
    {
        // Saved at 06:30 in Amman on Wednesday 25 November, the time of that day's ring.
        var routine = Weekdays(6, 30, [1, 3, 5], 2, new LocalDate(2026, 11, 25)) with { UpdatedAt = Instant.FromUtc(2026, 11, 25, 3, 30) };

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 22, 0, 0), Instant.FromUtc(2026, 12, 8, 0, 0));

        Assert.Equal(["2026-11-27T06:30", "2026-12-07T06:30"], Stamps(rings));
    }

    [Fact]
    public void History_of_an_alarm_every_minute_with_a_step_lists_14_days_of_cycles_in_seconds()
    {
        var step = new AlertRoutineStep(Guid.Parse("5a1e0000-0000-4000-8000-000000000001"), "Drink water", 0, LongAgo);
        var routine = Interval(0, 0, 1, RoutineRepeats.Minutes, new LocalDate(2020, 1, 1)) with { Steps = [step] };
        var now = Instant.FromUtc(2026, 11, 25, 12, 0);
        var watch = Stopwatch.StartNew();

        var history = RoutineCompletions.History([routine], [], new Dictionary<string, AlertAcknowledgement>(), NewYork, now);

        watch.Stop();
        // One cycle a minute for 14 days, both ends counted. New York's clocks did not change in them.
        Assert.Equal(14 * 24 * 60 + 1, history.Cycles.Count);
        Assert.Equal("2026-11-25T07:00", history.Cycles[0].Cycle);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
        Console.WriteLine($"history of {history.Cycles.Count} cycles took {watch.Elapsed.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public void A_series_from_the_year_1_every_minute_answers_at_once_and_exactly()
    {
        var routine = Interval(0, 0, 1, RoutineRepeats.Minutes, new LocalDate(1, 1, 1));
        var from = Instant.FromUtc(2026, 11, 25, 12, 0);
        var watch = Stopwatch.StartNew();

        var rings = Plan(routine, DateTimeZone.Utc, from, from + Duration.FromHours(1));

        watch.Stop();
        // A walk from the start would take a thousand million steps.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Equal(61, rings.Count);
        Assert.Equal(from, rings[0].AlertAt);
        Assert.Equal(from + Duration.FromHours(1), rings[^1].AlertAt);
        Assert.Equal("2026-11-25T12:00", Stamps(rings)[0]);
    }

    [Fact]
    public void A_series_from_1990_every_7_hours_lands_on_its_own_multiples()
    {
        var routine = Interval(0, 0, 7, RoutineRepeats.Hours, new LocalDate(1990, 1, 1));
        var anchor = Instant.FromUtc(1990, 1, 1, 5, 0);
        var from = Instant.FromUtc(2026, 11, 25, 0, 0);

        var rings = Plan(routine, NewYork, from, from + Duration.FromDays(2));

        Assert.Equal(7, rings.Count);
        Assert.All(rings, ring => Assert.Equal(0, (ring.AlertAt - anchor).TotalHours % 7));
    }

    [Fact]
    public void Nothing_rings_at_or_before_the_save()
    {
        // Hourly from 06:00 in Amman, which is 03:00 UTC. Saved at 05:00 UTC, the time of a ring.
        var routine = Interval(6, 0, 1, RoutineRepeats.Hours, new LocalDate(2026, 11, 25), Instant.FromUtc(2026, 11, 25, 5, 0));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 25, 0, 0), Instant.FromUtc(2026, 11, 25, 7, 0));

        Assert.Equal([Instant.FromUtc(2026, 11, 25, 6, 0), Instant.FromUtc(2026, 11, 25, 7, 0)], rings.Select(ring => ring.AlertAt));
    }

    [Fact]
    public void Nothing_rings_before_the_start_date()
    {
        var minutes = Interval(6, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25));
        var days = Interval(6, 0, 3, RoutineRepeats.Days, new LocalDate(2026, 11, 25));
        var weeks = Weekdays(6, 0, [1, 3, 5], 2, new LocalDate(2026, 11, 25));
        var from = Instant.FromUtc(2026, 11, 1, 0, 0);
        var until = Instant.FromUtc(2026, 11, 25, 2, 59);

        Assert.Empty(Plan(minutes, Amman, from, until));
        Assert.Empty(Plan(days, Amman, from, until));
        Assert.Empty(Plan(weeks, Amman, from, until));
        // 06:00 in Amman on the start date is 03:00 UTC.
        Assert.Single(Plan(minutes, Amman, from, until + Duration.FromMinutes(1)));
    }

    // ------------------------------------------------------------------ days and weeks

    [Fact]
    public void Every_3_days_rings_at_the_hour_and_minute_on_the_clock()
    {
        var routine = Interval(6, 30, 3, RoutineRepeats.Days, new LocalDate(2026, 11, 25));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 20, 0, 0), Instant.FromUtc(2026, 12, 6, 0, 0));

        Assert.Equal(["2026-11-25T06:30", "2026-11-28T06:30", "2026-12-01T06:30", "2026-12-04T06:30"], Stamps(rings));
        Assert.All(rings, ring => Assert.Equal((3, 30), (ring.AlertAt.InUtc().Hour, ring.AlertAt.InUtc().Minute)));
    }

    [Fact]
    public void Every_day_keeps_its_time_on_the_clock_across_a_clock_change_and_a_skipped_time_rings_after_the_gap()
    {
        // New York, Sunday 14 March 2027: 02:30 does not exist.
        var routine = Interval(2, 30, 1, RoutineRepeats.Days, new LocalDate(2027, 3, 13));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2027, 3, 13, 0, 0), Instant.FromUtc(2027, 3, 16, 0, 0));

        Assert.Equal(
            [Instant.FromUtc(2027, 3, 13, 7, 30), Instant.FromUtc(2027, 3, 14, 7, 0), Instant.FromUtc(2027, 3, 15, 6, 30)],
            rings.Select(ring => ring.AlertAt));
        Assert.Equal(["2027-03-13T02:30", "2027-03-14T02:30", "2027-03-15T02:30"], Stamps(rings));
    }

    [Fact]
    public void Every_2_weeks_rings_on_the_start_dates_weekday_every_other_week()
    {
        var routine = Interval(6, 30, 2, RoutineRepeats.Weeks, new LocalDate(2026, 11, 25));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 20, 0, 0), Instant.FromUtc(2026, 12, 30, 0, 0));

        Assert.Equal(["2026-11-25T06:30", "2026-12-09T06:30", "2026-12-23T06:30"], Stamps(rings));
    }

    [Fact]
    public void A_series_of_weeks_from_years_ago_is_found_by_arithmetic()
    {
        // 1 January 2020 to 25 November 2026 is 2,520 days: 180 steps of 2 weeks.
        var routine = Interval(6, 30, 2, RoutineRepeats.Weeks, new LocalDate(2020, 1, 1));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 20, 0, 0), Instant.FromUtc(2026, 12, 12, 0, 0));

        Assert.Equal(["2026-11-25T06:30", "2026-12-09T06:30"], Stamps(rings));
    }

    // ------------------------------------------------------------------ months and years

    [Fact]
    public void Every_month_from_the_31st_skips_the_months_without_one()
    {
        var routine = Interval(6, 30, 1, RoutineRepeats.Months, new LocalDate(2027, 1, 31));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2027, 1, 1, 0, 0), Instant.FromUtc(2027, 8, 15, 0, 0));

        Assert.Equal(["2027-01-31T06:30", "2027-03-31T06:30", "2027-05-31T06:30", "2027-07-31T06:30"], Stamps(rings));
        // 06:30 on the clock: 11:30 UTC in January, 10:30 UTC in daylight time.
        Assert.Equal([11, 10, 10, 10], rings.Select(ring => ring.AlertAt.InUtc().Hour));
    }

    [Fact]
    public void A_series_of_months_from_years_ago_starts_near_the_window()
    {
        var routine = Interval(6, 30, 1, RoutineRepeats.Months, new LocalDate(2020, 1, 31));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 25, 0, 0), Instant.FromUtc(2027, 2, 5, 0, 0));

        Assert.Equal(["2026-12-31T06:30", "2027-01-31T06:30"], Stamps(rings));
    }

    [Fact]
    public void Every_5_months_counts_from_the_start_date()
    {
        var routine = Interval(6, 30, 5, RoutineRepeats.Months, new LocalDate(2026, 1, 15));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2027, 12, 31, 0, 0));

        Assert.Equal(["2026-01-15T06:30", "2026-06-15T06:30", "2026-11-15T06:30", "2027-04-15T06:30", "2027-09-15T06:30"], Stamps(rings));
    }

    [Fact]
    public void Every_year_from_29_February_rings_in_leap_years_only()
    {
        var routine = Interval(6, 30, 1, RoutineRepeats.Years, new LocalDate(2024, 2, 29));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2024, 1, 1, 0, 0), Instant.FromUtc(2033, 1, 1, 0, 0));

        Assert.Equal(["2024-02-29T06:30", "2028-02-29T06:30", "2032-02-29T06:30"], Stamps(rings));
    }

    [Fact]
    public void Every_2_years_rings_on_the_date_every_other_year()
    {
        var routine = Interval(6, 30, 2, RoutineRepeats.Years, new LocalDate(2021, 11, 25));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2030, 1, 1, 0, 0));

        Assert.Equal(["2027-11-25T06:30", "2029-11-25T06:30"], Stamps(rings));
    }

    // ------------------------------------------------------------------ weekdays every N weeks

    [Fact]
    public void Weekdays_every_2_weeks_count_weeks_from_the_Monday_of_the_start_dates_week()
    {
        // Starts Wednesday 25 November 2026. Monday 23 is in the first week and before the start.
        var routine = Weekdays(6, 30, [1, 3, 5], 2, new LocalDate(2026, 11, 25));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 22, 0, 0), Instant.FromUtc(2026, 12, 13, 0, 0));

        // The week of 30 November is silent.
        Assert.Equal(
            ["2026-11-25T06:30", "2026-11-27T06:30", "2026-12-07T06:30", "2026-12-09T06:30", "2026-12-11T06:30"],
            Stamps(rings));
    }

    [Fact]
    public void Weekdays_every_3_weeks_with_a_Sunday_start_ring_that_Sunday_then_the_third_week_after()
    {
        // Sunday 29 November 2026 is the last day of the week of Monday 23.
        var routine = Weekdays(6, 30, [1, 7], 3, new LocalDate(2026, 11, 29));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 22, 0, 0), Instant.FromUtc(2026, 12, 21, 0, 0));

        Assert.Equal(["2026-11-29T06:30", "2026-12-14T06:30", "2026-12-20T06:30"], Stamps(rings));
    }

    [Fact]
    public void Weekdays_every_week_ignore_a_start_date_and_ring_as_they_always_did()
    {
        var plain = new AlertRoutine(Id, "Gym", 6, 30, [1, 3, 5], true, 9, LongAgo);
        var dated = Weekdays(6, 30, [1, 3, 5], 1, new LocalDate(2030, 1, 1));
        var from = Instant.FromUtc(2026, 11, 22, 0, 0);

        var rings = Plan(dated, Amman, from, from + Duration.FromDays(7));

        Assert.Equal(["2026-11-23T06:30", "2026-11-25T06:30", "2026-11-27T06:30"], Stamps(rings));
        Assert.Equal(Plan(plain, Amman, from, from + Duration.FromDays(7)), rings);
    }

    // ------------------------------------------------------------------ ring once

    [Fact]
    public void Only_weekdays_with_no_days_ring_once()
    {
        Assert.True(new AlertRoutine(Id, "Nap", 14, 0, [], true, 9, LongAgo).RingsOnce);
        Assert.False(new AlertRoutine(Id, "Nap", 14, 0, [1], true, 9, LongAgo).RingsOnce);
        var interval = Interval(14, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25));
        Assert.False(interval.RingsOnce);
        Assert.True(interval.Repeats);
    }

    [Fact]
    public void A_steps_key_carries_the_stamp_of_its_own_interval_ring()
    {
        var step = new AlertRoutineStep(Guid.Parse("5a1e0000-0000-4000-8000-000000000001"), "Drink water", 5, LongAgo);
        var routine = Interval(22, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25)) with { Steps = [step] };

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 25, 20, 30), Instant.FromUtc(2026, 11, 25, 21, 0));

        Assert.Equal(
            [
                "routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-11-25T23:37",
                "routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-11-25T23:37:step:5a1e0000-0000-4000-8000-000000000001"
            ],
            rings.Select(ring => ring.Key));
        Assert.Equal(Instant.FromUtc(2026, 11, 25, 20, 42), rings[1].AlertAt);
    }

    // ------------------------------------------------------------------ a step every N days

    private static readonly Guid Tretinoin = Guid.Parse("5a1e0000-0000-4000-8000-000000000001");
    private static readonly Guid Moisturizer = Guid.Parse("5a1e0000-0000-4000-8000-000000000002");

    /// <summary>Tretinoin 5 minutes after the ring every <paramref name="everyDays"/> days, then moisturizer 3 minutes on at every ring.</summary>
    private static AlertRoutineStep[] Steps(int everyDays, LocalDate startsOn) =>
    [
        new AlertRoutineStep(Tretinoin, "Tretinoin", 5, LongAgo) { EveryDays = everyDays, StartsOn = startsOn },
        new AlertRoutineStep(Moisturizer, "Moisturizer", 3, LongAgo),
    ];

    /// <summary>"2026-11-26T01:14 +5": each ring of a step, as its cycle's stamp and the minutes after that cycle's ring.</summary>
    private static string[] StepRings(IReadOnlyList<RoutineRing> rings, Guid step)
    {
        var own = rings.Where(ring => ring.StepId is null).ToDictionary(ring => ring.Key, ring => ring.AlertAt);
        return
        [
            .. rings.Where(ring => ring.StepId == step).Select(ring =>
            {
                var cycle = RoutineRings.ParseKey(ring.Key)!.Value.Cycle;
                var after = ring.AlertAt - own[RoutineRings.Key(Id, RoutineRings.ParseStamp(cycle)!.Value)];
                return $"{cycle} +{after.TotalMinutes:F0}";
            })
        ];
    }

    [Fact]
    public void A_step_every_2_days_rings_in_every_ring_of_a_minutes_interval_whose_own_stamp_is_on_its_day()
    {
        // Every 97 minutes from 22:00 on 25 November in Amman. The step's days are 26, 28, and so on.
        var routine = Interval(22, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25))
            with { Steps = Steps(2, new LocalDate(2026, 11, 26)) };

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 25, 18, 0), Instant.FromUtc(2026, 11, 26, 1, 0));

        // The rings stamped 22:00 and 23:37 are on the 25th: no tretinoin, and moisturizer follows the ring by its own 3 minutes.
        Assert.Equal(["2026-11-26T01:14 +5", "2026-11-26T02:51 +5"], StepRings(rings, Tretinoin));
        Assert.Equal(
            ["2026-11-25T22:00 +3", "2026-11-25T23:37 +3", "2026-11-26T01:14 +8", "2026-11-26T02:51 +8"],
            StepRings(rings, Moisturizer));
    }

    [Fact]
    public void A_step_every_2_days_rings_in_all_three_rings_a_day_of_an_8_hour_interval_then_skips_a_day()
    {
        var routine = Interval(6, 0, 8, RoutineRepeats.Hours, new LocalDate(2026, 11, 25))
            with { Steps = Steps(2, new LocalDate(2026, 11, 25)) };

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 25, 0, 0), Instant.FromUtc(2026, 11, 27, 21, 0));

        Assert.Equal(
            [
                "2026-11-25T06:00 +5", "2026-11-25T14:00 +5", "2026-11-25T22:00 +5",
                "2026-11-27T06:00 +5", "2026-11-27T14:00 +5", "2026-11-27T22:00 +5"
            ],
            StepRings(rings, Tretinoin));
        Assert.Equal(9, StepRings(rings, Moisturizer).Length);
    }

    [Fact]
    public void A_step_every_2_days_follows_the_clock_date_of_a_ring_the_clocks_moved()
    {
        // Every 6 hours from 20:00 on 31 October in New York. The clocks go back on 1 November,
        // so the rings read 20:00, then 01:00, 07:00, 13:00, 19:00, then 01:00 on the 2nd.
        var routine = Interval(20, 0, 6, RoutineRepeats.Hours, new LocalDate(2026, 10, 31))
            with { Steps = Steps(2, new LocalDate(2026, 11, 1)) };

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 0, 0), Instant.FromUtc(2026, 11, 2, 7, 0));

        Assert.Equal(
            ["2026-11-01T01:00 +5", "2026-11-01T07:00 +5", "2026-11-01T13:00 +5", "2026-11-01T19:00 +5"],
            StepRings(rings, Tretinoin));
    }

    [Fact]
    public void A_step_every_3_days_counts_calendar_days_under_weekdays_every_2_weeks()
    {
        // Monday and Wednesday of every other week from Wednesday 25 November: 25 November, 7 and 9 December.
        var routine = Weekdays(6, 30, [1, 3], 2, new LocalDate(2026, 11, 25))
            with { Steps = Steps(3, new LocalDate(2026, 11, 25)) };

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 11, 22, 0, 0), Instant.FromUtc(2026, 12, 10, 0, 0));

        // 7 December is 12 days from the step's start, 9 December is 14.
        Assert.Equal(["2026-11-25T06:30 +5", "2026-12-07T06:30 +5"], StepRings(rings, Tretinoin));
        Assert.Equal(["2026-11-25T06:30 +8", "2026-12-07T06:30 +8", "2026-12-09T06:30 +3"], StepRings(rings, Moisturizer));
    }

    [Fact]
    public void A_step_every_2_days_rings_at_every_other_ring_of_a_daily_interval_and_at_the_rings_of_a_3_day_interval_on_its_days()
    {
        var daily = Interval(6, 30, 1, RoutineRepeats.Days, new LocalDate(2026, 11, 25))
            with { Steps = Steps(2, new LocalDate(2026, 11, 26)) };
        // Every 3 days from the 25th: 25, 28, 1 December, 4 December. The step's days are the even ones from the 26th.
        var third = Interval(6, 30, 3, RoutineRepeats.Days, new LocalDate(2026, 11, 25))
            with { Steps = Steps(2, new LocalDate(2026, 11, 26)) };
        var from = Instant.FromUtc(2026, 11, 25, 0, 0);

        Assert.Equal(
            ["2026-11-26T06:30 +5", "2026-11-28T06:30 +5", "2026-11-30T06:30 +5"],
            StepRings(Plan(daily, Amman, from, from + Duration.FromDays(6)), Tretinoin));
        Assert.Equal(
            ["2026-11-28T06:30 +5", "2026-12-04T06:30 +5"],
            StepRings(Plan(third, Amman, from, from + Duration.FromDays(10)), Tretinoin));
    }

    // ------------------------------------------------------------------ a delay of up to 7 days

    [Fact]
    public void An_interval_ring_delayed_7_days_rings_then_under_its_own_key_with_its_steps_and_the_rest_ring_on_time()
    {
        var routine = Interval(22, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25))
            with { Steps = Steps(2, new LocalDate(2026, 11, 26)) };
        var planned = Instant.FromUtc(2026, 11, 25, 22, 14);
        var delay = new RoutineDelay(Id, "2026-11-26T01:14", RoutineDelays.MaxMinutes, planned, planned);
        var moved = planned + Duration.FromDays(7);

        var rings = RoutineRings.Plan(
            [routine], Amman, moved - Duration.FromMinutes(10), moved + Duration.FromMinutes(10), ThreeHours,
            delays: RoutineDelays.ByCycle([delay]));

        // The moved cycle keeps the steps of the day it was planned for, the 26th. 3 December is an off day for the step.
        var own = rings.Where(ring => RoutineRings.ParseKey(ring.Key)!.Value.Cycle == "2026-11-26T01:14").ToList();
        Assert.Equal(
            new (Guid?, Instant)[] { (null, moved), (Tretinoin, moved + Duration.FromMinutes(5)), (Moisturizer, moved + Duration.FromMinutes(8)) },
            own.Select(ring => (ring.StepId, ring.AlertAt)));
        Assert.Equal(rings.Count, rings.Select(ring => ring.Key).Distinct().Count());
        // The series itself still rings on time around it.
        Assert.Contains(rings, ring => ring.StepId is null && ring.Key != own[0].Key);
    }

    [Fact]
    public void A_cycle_of_an_interval_is_still_named_by_its_stamp_a_week_after_a_delay_moved_it()
    {
        var routine = Interval(22, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25));
        var planned = Instant.FromUtc(2026, 11, 25, 22, 14);

        // The moment the moved ring rings, 7 days on, and the last day history keeps it.
        Assert.Equal(planned, RoutineCompletions.CycleAt(routine, "2026-11-26T01:14", Amman, planned + Duration.FromDays(7)));
        Assert.Equal(planned, RoutineCompletions.CycleAt(routine, "2026-11-26T01:14", Amman, planned + Duration.FromDays(14)));
        Assert.Null(RoutineCompletions.CycleAt(routine, "2026-11-26T01:14", Amman, planned + Duration.FromDays(14) + Duration.FromMinutes(1)));
    }

    // ------------------------------------------------------------------ the cycle a stamp names

    [Fact]
    public void A_stamp_names_an_interval_ring_only_when_the_series_has_it()
    {
        var routine = Interval(22, 0, 97, RoutineRepeats.Minutes, new LocalDate(2026, 11, 25));
        var now = Instant.FromUtc(2026, 11, 26, 0, 0);

        Assert.Equal(Instant.FromUtc(2026, 11, 25, 22, 14), RoutineCompletions.CycleAt(routine, "2026-11-26T01:14", Amman, now));
        Assert.Null(RoutineCompletions.CycleAt(routine, "2026-11-26T01:15", Amman, now));
        Assert.Null(RoutineCompletions.CycleAt(routine, "2026-11-25T20:23", Amman, now));
        Assert.Null(RoutineCompletions.CycleAt(routine, "9999-12-31T23:59", Amman, now));
        Assert.Null(RoutineCompletions.CycleAt(routine, "0001-01-01T00:00", Amman, now));
    }

    [Fact]
    public void A_stamp_of_the_dropped_second_pass_names_the_first_pass()
    {
        var routine = Interval(0, 0, 30, RoutineRepeats.Minutes, new LocalDate(2026, 11, 1));

        var at = RoutineCompletions.CycleAt(routine, "2026-11-01T01:30", NewYork, Instant.FromUtc(2026, 11, 1, 12, 0));

        Assert.Equal(Instant.FromUtc(2026, 11, 1, 5, 30), at);
    }
}
