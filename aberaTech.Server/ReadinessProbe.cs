namespace aberaTech.Server;

/// <summary>
/// One readiness check shared by everybody who asks within a moment of it.
/// </summary>
/// <remarks>
/// /readyz is unauthenticated and each evaluation opens a connection to every
/// configured database, out of a pool of ten on a server that allows fifty.
/// Asked often enough it is a way to starve the features it reports on. A
/// rate limit is the wrong tool: the callers that matter — the deploy gate,
/// an uptime probe, the platform — must never be told 429, and behind
/// Cloudflare some of them share an address.
///
/// So the answer is held for a couple of seconds instead, and concurrent
/// callers wait for the one check in flight rather than starting their own.
/// A flood costs one check per lifetime; a probe that polls every few seconds
/// sees a fresh answer every time. /healthz needs nothing: it touches no
/// dependency and allocates a two-byte response.
/// </remarks>
public sealed class ReadinessProbe(
    TimeSpan lifetime,
    TimeProvider time,
    Func<CancellationToken, Task<ReadinessReport>> check)
{
    /// <summary>
    /// A hung database must not hang the probe; an unanswered check inside
    /// this budget is a failed check. The check's own, not any one caller's:
    /// a caller that gives up must not fail the answer everybody else shares.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ReadinessReport? _report;
    private long _takenAt;

    public async Task<ReadinessReport> GetAsync(CancellationToken cancellationToken = default)
    {
        if (Fresh() is { } current) return current;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Whoever held the gate may have just answered the question.
            if (Fresh() is { } justTaken) return justTaken;

            using var deadline = new CancellationTokenSource(Budget);
            var report = await check(deadline.Token);

            _takenAt = time.GetTimestamp();
            _report = report;
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    private ReadinessReport? Fresh() =>
        _report is { } report && time.GetElapsedTime(Volatile.Read(ref _takenAt)) < lifetime ? report : null;
}
