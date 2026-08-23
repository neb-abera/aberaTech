using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

/// <summary>
/// The daylight saving cases are the point of this file.
/// </summary>
/// <remarks>
/// A scheduler that does wall-clock arithmetic looks correct all year and then
/// hands out the wrong number of slots on two Sundays, or quietly moves
/// everybody's appointment by an hour. Both transitions are asserted here
/// against real 2027 dates in a zone that observes them, because the only way
/// this stays fixed is if the build fails when it breaks.
/// </remarks>
public class SlotPlannerTests
{
    private const string Chicago = "America/Chicago";

    // 2027-03-14 is the second Sunday in March: 02:00 becomes 03:00 and the day
    // is 23 hours long.
    private static readonly LocalDate SpringForward = new(2027, 3, 14);

    // 2027-11-07 is the first Sunday in November: 01:00 happens twice and the
    // day is 25 hours long.
    private static readonly LocalDate FallBack = new(2027, 11, 7);

    private static readonly Instant Distant = Instant.FromUtc(2000, 1, 1, 0, 0);

    [Fact]
    public void An_ordinary_day_divides_into_whole_slots()
    {
        var rule = new AvailabilityRule(IsoDayOfWeek.Wednesday, new LocalTime(9, 0), new LocalTime(17, 0), Chicago);
        var date = new LocalDate(2027, 3, 10);

        var slots = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [], Distant);

        Assert.Equal(16, slots.Count);
    }

    [Fact]
    public void A_window_spanning_the_spring_forward_gap_loses_the_hour_that_does_not_exist()
    {
        // 01:00 to 05:00 is four hours on the wall clock and three hours of
        // elapsed time on this particular Sunday. Six half-hour slots fit, not
        // eight. Getting this wrong offers two slots nobody can attend.
        var rule = new AvailabilityRule(IsoDayOfWeek.Sunday, new LocalTime(1, 0), new LocalTime(5, 0), Chicago);

        var slots = SlotPlanner.Plan([rule], SpringForward, SpringForward, Duration.FromMinutes(30), [], Distant);

        Assert.Equal(6, slots.Count);
        Assert.Equal(Duration.FromHours(3), slots[^1].End - slots[0].Start);
    }

    [Fact]
    public void A_window_spanning_the_fall_back_repeat_gains_the_hour_that_happens_twice()
    {
        // 01:00 to 03:00 is two hours on the wall clock and three hours of
        // elapsed time. A tool that trusts the wall clock closes the queue an
        // hour early.
        var rule = new AvailabilityRule(IsoDayOfWeek.Sunday, new LocalTime(1, 0), new LocalTime(3, 0), Chicago);

        var slots = SlotPlanner.Plan([rule], FallBack, FallBack, Duration.FromMinutes(30), [], Distant);

        Assert.Equal(6, slots.Count);
        Assert.Equal(Duration.FromHours(3), slots[^1].End - slots[0].Start);
    }

    [Fact]
    public void Slot_boundaries_stay_contiguous_across_a_transition()
    {
        var rule = new AvailabilityRule(IsoDayOfWeek.Sunday, new LocalTime(1, 0), new LocalTime(5, 0), Chicago);

        var slots = SlotPlanner.Plan([rule], SpringForward, SpringForward, Duration.FromMinutes(30), [], Distant);

        // No gaps and no overlaps: the end of each slot is the start of the
        // next, in elapsed time, whatever the clock on the wall said.
        for (var i = 1; i < slots.Count; i++)
        {
            Assert.Equal(slots[i - 1].End, slots[i].Start);
        }
    }

    [Fact]
    public void Busy_periods_remove_only_the_slots_they_touch()
    {
        var rule = new AvailabilityRule(IsoDayOfWeek.Wednesday, new LocalTime(9, 0), new LocalTime(11, 0), Chicago);
        var date = new LocalDate(2027, 3, 10);

        var all = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [], Distant);
        var busy = new Interval(all[1].Start, all[1].End);

        var remaining = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [busy], Distant);

        Assert.Equal(all.Count - 1, remaining.Count);
        Assert.DoesNotContain(remaining, slot => slot.Start == busy.Start);
    }

    [Fact]
    public void A_busy_period_overlapping_a_slot_edge_still_removes_it()
    {
        var rule = new AvailabilityRule(IsoDayOfWeek.Wednesday, new LocalTime(9, 0), new LocalTime(10, 0), Chicago);
        var date = new LocalDate(2027, 3, 10);

        var all = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [], Distant);
        // One minute of overlap is still a double booking.
        var busy = new Interval(all[0].End - Duration.FromMinutes(1), all[0].End + Duration.FromMinutes(1));

        var remaining = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [busy], Distant);

        Assert.Empty(remaining);
    }

    [Fact]
    public void Slots_before_the_lead_time_are_not_offered()
    {
        var rule = new AvailabilityRule(IsoDayOfWeek.Wednesday, new LocalTime(9, 0), new LocalTime(11, 0), Chicago);
        var date = new LocalDate(2027, 3, 10);

        var all = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [], Distant);
        var remaining = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [], all[2].Start);

        Assert.Equal(all.Count - 2, remaining.Count);
        Assert.All(remaining, slot => Assert.True(slot.Start >= all[2].Start));
    }

    [Fact]
    public void A_remainder_too_short_for_a_whole_slot_is_dropped()
    {
        // 09:00 to 09:50 holds one 30 minute slot, not one and two thirds.
        var rule = new AvailabilityRule(IsoDayOfWeek.Wednesday, new LocalTime(9, 0), new LocalTime(9, 50), Chicago);
        var date = new LocalDate(2027, 3, 10);

        var slots = SlotPlanner.Plan([rule], date, date, Duration.FromMinutes(30), [], Distant);

        Assert.Single(slots);
    }

    [Fact]
    public void A_rule_only_applies_on_its_own_weekday()
    {
        var rule = new AvailabilityRule(IsoDayOfWeek.Monday, new LocalTime(9, 0), new LocalTime(17, 0), Chicago);

        var slots = SlotPlanner.Plan(
            [rule],
            new LocalDate(2027, 3, 9),
            new LocalDate(2027, 3, 13),
            Duration.FromMinutes(30),
            [],
            Distant);

        Assert.Empty(slots);
    }

    [Fact]
    public void A_rule_that_ends_before_it_starts_is_rejected_at_construction()
    {
        Assert.Throws<ArgumentException>(() =>
            new AvailabilityRule(IsoDayOfWeek.Monday, new LocalTime(17, 0), new LocalTime(9, 0), Chicago));
    }

    [Fact]
    public void An_unknown_zone_is_rejected_at_construction()
    {
        Assert.Throws<ArgumentException>(() =>
            new AvailabilityRule(IsoDayOfWeek.Monday, new LocalTime(9, 0), new LocalTime(17, 0), "Mars/Olympus_Mons"));
    }
}
