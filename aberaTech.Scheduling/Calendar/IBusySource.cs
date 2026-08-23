using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>Somewhere that knows when the host is already occupied.</summary>
/// <remarks>
/// An interface with more than one implementation because busy time comes from
/// two unrelated places: appointments booked here, which are authoritative and
/// local, and the host's Google calendar, which is authoritative and remote and
/// can be unreachable. Keeping them apart means the remote one failing degrades
/// to "we know about our own bookings" instead of taking the page down.
/// </remarks>
public interface IBusySource
{
    /// <summary>Periods the host is unavailable, overlapping the given range.</summary>
    Task<IReadOnlyList<Interval>> GetBusyAsync(Interval range, CancellationToken cancellationToken);
}

/// <summary>Merges several sources, tolerating any of them failing.</summary>
public sealed class CompositeBusySource(IEnumerable<IBusySource> sources, ILogger<CompositeBusySource> logger)
    : IBusySource
{
    public async Task<IReadOnlyList<Interval>> GetBusyAsync(Interval range, CancellationToken cancellationToken)
    {
        var busy = new List<Interval>();

        foreach (var source in sources)
        {
            try
            {
                busy.AddRange(await source.GetBusyAsync(range, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Deliberately not rethrown. If Google is slow or down, the
                // right behaviour is to keep taking bookings against the times
                // we do know about rather than show an error page — the worst
                // case is a double booking the host can move, and the best case
                // of failing hard is nobody can book at all.
                logger.LogWarning(
                    exception,
                    "A busy-time source failed; continuing with the others. Slots may be offered that are busy elsewhere.");
            }
        }

        return busy;
    }
}
