using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// /readyz opens a database connection per configured database, and it is
/// unauthenticated. Its answer is shared for a moment so that a flood of
/// probes is one check, while the deploy gate and the platform's probes —
/// seconds apart — never notice.
/// </summary>
public sealed class ReadinessProbeTests
{
    [Fact]
    public async Task A_burst_of_probes_is_one_check()
    {
        var clock = new ManualTime();
        var checks = 0;
        var probe = new ReadinessProbe(TimeSpan.FromSeconds(2), clock, _ =>
        {
            Interlocked.Increment(ref checks);
            return Task.FromResult(Readiness.From([Readiness.Reachable("scheduling-db")]));
        });

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => probe.GetAsync()));

        Assert.Equal(1, checks);
    }

    [Fact]
    public async Task The_answer_is_never_older_than_its_lifetime()
    {
        var clock = new ManualTime();
        var reachable = true;
        var probe = new ReadinessProbe(TimeSpan.FromSeconds(2), clock, _ => Task.FromResult(Readiness.From(
        [
            reachable ? Readiness.Reachable("scheduling-db") : Readiness.Unreachable("scheduling-db", "NpgsqlException")
        ])));

        Assert.True((await probe.GetAsync()).Ready);

        reachable = false;
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True((await probe.GetAsync()).Ready);

        clock.Advance(TimeSpan.FromSeconds(1.5));
        Assert.False((await probe.GetAsync()).Ready);
    }

    [Fact]
    public async Task A_check_that_throws_is_not_remembered()
    {
        var clock = new ManualTime();
        var attempts = 0;
        var probe = new ReadinessProbe(TimeSpan.FromSeconds(2), clock, _ =>
            ++attempts == 1
                ? throw new InvalidOperationException("first")
                : Task.FromResult(Readiness.From([])));

        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.GetAsync());
        Assert.True((await probe.GetAsync()).Ready);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
