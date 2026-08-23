using aberaTech.Scheduling.Calendar;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Calendar;

public class BusyMergeTests
{
    private static Interval At(int fromHour, int toHour) =>
        new(Instant.FromUtc(2027, 6, 1, fromHour, 0), Instant.FromUtc(2027, 6, 1, toHour, 0));

    [Fact]
    public void Nothing_merges_to_nothing()
    {
        Assert.Empty(BusyMerge.Coalesce([]));
    }

    [Fact]
    public void Disjoint_periods_are_left_alone_but_sorted()
    {
        var merged = BusyMerge.Coalesce([At(14, 15), At(9, 10)]);

        Assert.Equal([At(9, 10), At(14, 15)], merged);
    }

    [Fact]
    public void Overlapping_periods_become_one()
    {
        // The everyday case: an appointment booked here is also an event on the
        // host's Google calendar, so both sources report the same hour.
        var merged = BusyMerge.Coalesce([At(9, 11), At(10, 12)]);

        Assert.Equal([At(9, 12)], merged);
    }

    [Fact]
    public void Touching_periods_become_one()
    {
        Assert.Equal([At(9, 11)], BusyMerge.Coalesce([At(9, 10), At(10, 11)]));
    }

    [Fact]
    public void A_period_wholly_inside_another_disappears_into_it()
    {
        // The bug this guards: taking the later end unconditionally would
        // shorten the block to 10:00 and offer a slot in the middle of it.
        Assert.Equal([At(9, 17)], BusyMerge.Coalesce([At(9, 17), At(9, 10), At(12, 13)]));
    }

    [Fact]
    public void Identical_periods_collapse()
    {
        Assert.Equal([At(9, 10)], BusyMerge.Coalesce([At(9, 10), At(9, 10), At(9, 10)]));
    }

    [Fact]
    public void Empty_and_backwards_periods_are_discarded()
    {
        // Google returns zero-length busy blocks for declined and cancelled
        // events; treating one as busy would block a slot for no reason.
        var backwards = new Interval(Instant.FromUtc(2027, 6, 1, 10, 0), Instant.FromUtc(2027, 6, 1, 10, 0));

        Assert.Equal([At(9, 10)], BusyMerge.Coalesce([At(9, 10), backwards]));
    }

    [Fact]
    public void A_long_chain_collapses_to_one_block()
    {
        var chain = Enumerable.Range(9, 8).Select(hour => At(hour, hour + 1));

        Assert.Equal([At(9, 17)], BusyMerge.Coalesce(chain));
    }
}
