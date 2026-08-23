using aberaTech.Scheduling.Admin;
using Xunit;

namespace aberaTech.Server.Tests.Admin;

public class AvailabilityValidationTests
{
    private static AvailabilityWeek Week(params AvailabilityDay[] days) =>
        new("Etc/GMT-3", days);

    private static AvailabilityDay Day(int day, string from = "07:00", string to = "23:00", bool active = true) =>
        new(day, from, to, active);

    [Fact]
    public void A_normal_week_is_accepted_and_parsed()
    {
        var (week, error) = AvailabilityEndpoints.Validate(
            Week(Enumerable.Range(1, 7).Select(d => Day(d)).ToArray()));

        Assert.Null(error);
        Assert.NotNull(week);
        Assert.Equal(7, week!.Days.Count);
        Assert.Equal("Etc/GMT-3", week.ZoneId);
        Assert.Equal(7, week.Days[0].Start.Hour);
        Assert.Equal(23, week.Days[0].End.Hour);
    }

    [Fact]
    public void A_named_zone_is_accepted_too()
    {
        var (_, error) = AvailabilityEndpoints.Validate(
            new AvailabilityWeek("America/New_York", [Day(1)]));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("GMT+3")]
    [InlineData("")]
    public void An_unknown_zone_is_refused(string zone)
    {
        var (week, error) = AvailabilityEndpoints.Validate(new AvailabilityWeek(zone, [Day(1)]));

        Assert.Null(week);
        Assert.Contains("time zone", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void A_day_outside_the_week_is_refused(int day)
    {
        var (week, _) = AvailabilityEndpoints.Validate(Week(Day(day)));

        Assert.Null(week);
    }

    [Fact]
    public void The_same_day_twice_is_refused()
    {
        // Two rows for one day would leave which one wins depending on
        // insertion order.
        var (week, error) = AvailabilityEndpoints.Validate(Week(Day(1), Day(1, "09:00", "17:00")));

        Assert.Null(week);
        Assert.Contains("twice", error);
    }

    [Theory]
    [InlineData("7am")]
    [InlineData("25:00")]
    [InlineData("")]
    public void A_time_that_is_not_a_time_is_refused(string from)
    {
        var (week, _) = AvailabilityEndpoints.Validate(Week(Day(1, from)));

        Assert.Null(week);
    }

    [Fact]
    public void A_day_that_ends_before_it_starts_is_refused()
    {
        var (week, error) = AvailabilityEndpoints.Validate(Week(Day(1, "23:00", "07:00")));

        Assert.Null(week);
        Assert.Contains("end after it starts", error);
    }

    [Fact]
    public void A_day_that_ends_when_it_starts_is_refused()
    {
        var (week, _) = AvailabilityEndpoints.Validate(Week(Day(1, "09:00", "09:00")));

        Assert.Null(week);
    }

    [Fact]
    public void An_inactive_day_keeps_its_times_even_if_they_are_backwards()
    {
        // Turning a day off should not force you to fix times you are not using.
        // They are kept so switching it back on restores what was there.
        var (week, error) = AvailabilityEndpoints.Validate(Week(Day(1, "23:00", "07:00", active: false)));

        Assert.Null(error);
        Assert.False(week!.Days[0].Active);
    }

    [Fact]
    public void An_empty_week_is_refused_rather_than_wiping_the_hours()
    {
        // Replace semantics make this dangerous: an empty list would otherwise
        // delete every rule and silently take the booking page offline.
        var (week, error) = AvailabilityEndpoints.Validate(new AvailabilityWeek("Etc/GMT-3", []));

        Assert.Null(week);
        Assert.Contains("whole week", error);
    }

    [Fact]
    public void A_missing_body_is_refused()
    {
        var (week, _) = AvailabilityEndpoints.Validate(null);

        Assert.Null(week);
    }
}
