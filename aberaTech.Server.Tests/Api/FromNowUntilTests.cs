using aberaTech.Scheduling.Api;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// The guard against a stale closing time.
/// </summary>
/// <remarks>
/// This exists because of a real failure. A queue session left open overnight
/// has a closing time in the past, and building an interval from now until then
/// threw, which returned 500 for the whole scheduling page rather than
/// degrading to "nothing is open".
/// </remarks>
public class FromNowUntilTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 8, 24, 12, 0);

    [Fact]
    public void A_future_end_is_used_as_given()
    {
        var end = Now + Duration.FromHours(6);

        var interval = SchedulingEndpoints.FromNowUntil(Now, end);

        Assert.Equal(Now, interval.Start);
        Assert.Equal(end, interval.End);
    }

    [Fact]
    public void An_end_already_past_collapses_to_an_empty_interval_rather_than_throwing()
    {
        // The exact case that broke the site: a session that closed yesterday.
        var end = Now - Duration.FromHours(18);

        var interval = SchedulingEndpoints.FromNowUntil(Now, end);

        Assert.Equal(Now, interval.Start);
        Assert.Equal(Now, interval.End);
        Assert.Equal(Duration.Zero, interval.Duration);
    }

    [Fact]
    public void An_end_exactly_now_is_fine()
    {
        var interval = SchedulingEndpoints.FromNowUntil(Now, Now);

        Assert.Equal(Duration.Zero, interval.Duration);
    }

    [Fact]
    public void It_never_produces_a_backwards_interval()
    {
        foreach (var offset in new[] { -48, -1, 0, 1, 48 })
        {
            var interval = SchedulingEndpoints.FromNowUntil(Now, Now + Duration.FromHours(offset));
            Assert.True(interval.End >= interval.Start, $"backwards at {offset}h");
        }
    }
}
