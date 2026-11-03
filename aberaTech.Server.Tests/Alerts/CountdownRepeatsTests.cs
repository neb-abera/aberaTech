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
        Assert.Equal(At(2020, 1, 1), CountdownRepeats.Next(At(2020, 1, 1), CountdownRepeats.None, 1, NewYork, At(2026, 11, 3)));
        Assert.Equal(At(2027, 1, 1), CountdownRepeats.Next(At(2027, 1, 1), CountdownRepeats.Yearly, 1, NewYork, At(2026, 11, 3)));
    }

    [Theory]
    [InlineData(CountdownRepeats.Yearly, 2026, 12, 30)]
    [InlineData(CountdownRepeats.Monthly, 2026, 11, 30)]
    [InlineData(CountdownRepeats.Weekly, 2026, 11, 9)]
    public void Each_period_steps_to_the_first_date_after_now(string repeat, int y, int m, int d)
    {
        // Monday 30 December 2019.
        Assert.Equal(At(y, m, d, 9), CountdownRepeats.Next(At(2019, 12, 30, 9), repeat, 1, NewYork, At(2026, 11, 3, 12)));
    }

    [Fact]
    public void The_time_on_the_clock_holds_across_a_clock_change()
    {
        // 09:00 in summer is 13:00 UTC, in winter 14:00 UTC.
        var next = CountdownRepeats.Next(At(2026, 7, 7, 9), CountdownRepeats.Monthly, 1, NewYork, At(2026, 11, 8));
        Assert.Equal(Instant.FromUtc(2026, 12, 7, 14, 0), next);
    }

    [Fact]
    public void The_29th_of_February_comes_only_in_a_leap_year()
    {
        Assert.Equal(At(2028, 2, 29), CountdownRepeats.Next(At(2024, 2, 29), CountdownRepeats.Yearly, 1, NewYork, At(2026, 11, 3)));
    }

    [Fact]
    public void The_31st_skips_the_months_without_one()
    {
        Assert.Equal(At(2026, 12, 31), CountdownRepeats.Next(At(2026, 1, 31), CountdownRepeats.Monthly, 1, NewYork, At(2026, 11, 3)));
    }

    [Theory]
    [InlineData(CountdownRepeats.Hourly, 2, "2026-11-03T14:00:00Z")]
    [InlineData(CountdownRepeats.Daily, 3, "2026-11-05T14:00:00Z")]
    [InlineData(CountdownRepeats.Weekly, 2, "2026-11-09T14:00:00Z")]
    [InlineData(CountdownRepeats.Monthly, 3, "2026-12-30T14:00:00Z")]
    [InlineData(CountdownRepeats.Yearly, 2, "2027-12-30T14:00:00Z")]
    public void Every_n_units_counts_whole_steps_of_n_from_the_first_date(string repeat, int every, string expected)
    {
        // 09:00 on Monday 30 December 2019 in New York, asked at 12:00 UTC on 3 November 2026.
        // 12:00 UTC is itself a two-hour step, and the next is after now. 2,502 days is 834 threes.
        // 2,506 days is 358 weeks, an even number. 84 months is 28 threes.
        var next = CountdownRepeats.Next(At(2019, 12, 30, 9), repeat, every, NewYork, Instant.FromUtc(2026, 11, 3, 12, 0));
        Assert.Equal(NodaTime.Text.InstantPattern.ExtendedIso.Parse(expected).Value, next);
    }

    [Theory]
    [InlineData(CountdownRepeats.Hourly, 1, "RRULE:FREQ=HOURLY")]
    [InlineData(CountdownRepeats.Daily, 3, "RRULE:FREQ=DAILY;INTERVAL=3")]
    [InlineData(CountdownRepeats.Yearly, 1, "RRULE:FREQ=YEARLY")]
    [InlineData(CountdownRepeats.None, 4, null)]
    public void Google_is_sent_the_unit_and_the_interval(string repeat, int every, string? rule) =>
        Assert.Equal(rule, CountdownRepeats.Rule(repeat, every));

    [Theory]
    [InlineData(CountdownRepeats.Yearly, 1, "Every year")]
    [InlineData(CountdownRepeats.Hourly, 2, "Every 2 hours")]
    [InlineData(CountdownRepeats.None, 1, null)]
    public void The_page_says_how_often(string repeat, int every, string? text) =>
        Assert.Equal(text, CountdownRepeats.Describe(repeat, every));

    [Fact]
    public void Its_own_moment_is_past_and_the_next_date_follows()
    {
        Assert.Equal(At(2027, 10, 7), CountdownRepeats.Next(At(1991, 10, 7), CountdownRepeats.Yearly, 1, NewYork, At(2026, 10, 7)));
    }
}
