using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>The next date of a repeating countdown, on its own zone's clock, skipping dates a period lacks as RFC 5545 does.</summary>
public sealed class CountdownRepeatsTests
{
    private static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];

    private static Instant At(int y, int m, int d, int h = 0) =>
        new LocalDateTime(y, m, d, h, 0).InZoneLeniently(NewYork).ToInstant();

    [Fact]
    public void One_that_does_not_repeat_or_has_not_come_yet_keeps_its_date()
    {
        Assert.Equal(At(2020, 1, 1), CountdownRepeats.Next(At(2020, 1, 1), CountdownRepeats.None, NewYork, At(2026, 11, 3)));
        Assert.Equal(At(2027, 1, 1), CountdownRepeats.Next(At(2027, 1, 1), CountdownRepeats.Yearly, NewYork, At(2026, 11, 3)));
    }

    [Theory]
    [InlineData(CountdownRepeats.Yearly, 2026, 12, 30)]
    [InlineData(CountdownRepeats.Monthly, 2026, 11, 30)]
    [InlineData(CountdownRepeats.Weekly, 2026, 11, 9)]
    public void Each_period_steps_to_the_first_date_after_now(string repeat, int y, int m, int d)
    {
        // Monday 30 December 2019.
        Assert.Equal(At(y, m, d, 9), CountdownRepeats.Next(At(2019, 12, 30, 9), repeat, NewYork, At(2026, 11, 3, 12)));
    }

    [Fact]
    public void The_time_on_the_clock_holds_across_a_clock_change()
    {
        // 09:00 in summer is 13:00 UTC, in winter 14:00 UTC.
        var next = CountdownRepeats.Next(At(2026, 7, 7, 9), CountdownRepeats.Monthly, NewYork, At(2026, 11, 8));
        Assert.Equal(Instant.FromUtc(2026, 12, 7, 14, 0), next);
    }

    [Fact]
    public void The_29th_of_February_comes_only_in_a_leap_year()
    {
        Assert.Equal(At(2028, 2, 29), CountdownRepeats.Next(At(2024, 2, 29), CountdownRepeats.Yearly, NewYork, At(2026, 11, 3)));
    }

    [Fact]
    public void The_31st_skips_the_months_without_one()
    {
        Assert.Equal(At(2026, 12, 31), CountdownRepeats.Next(At(2026, 1, 31), CountdownRepeats.Monthly, NewYork, At(2026, 11, 3)));
    }

    [Fact]
    public void Its_own_moment_is_past_and_the_next_date_follows()
    {
        Assert.Equal(At(2027, 10, 7), CountdownRepeats.Next(At(1991, 10, 7), CountdownRepeats.Yearly, NewYork, At(2026, 10, 7)));
    }
}
