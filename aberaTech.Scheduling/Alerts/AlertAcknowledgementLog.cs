using Microsoft.Extensions.Logging;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// An acknowledgement for an occurrence already acknowledged. The first
/// stands and nothing changes, so this line is the only trace of the second.
/// It tells a Stop that never ran from one that ran and came late. On
/// 2026-10-10 a Stop on the phone left no row and Pushover rang. A line
/// carries the occurrence key, both channels and the seconds between them.
/// Never a token or a receipt.
/// </summary>
public static partial class AlertAcknowledgementLog
{
    public const string Category = "aberaTech.Alerts.Acknowledgements";

    public const int LateId = 4018;

    /// <summary>Logs <paramref name="via"/>'s acknowledgement of <paramref name="key"/>, which arrived after another.</summary>
    public static async Task LateAsync(
        IAlertStore store, ILoggerFactory loggers, string key, string via, Instant now, CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger(Category);
        if (!logger.IsEnabled(LogLevel.Information)) return;
        // Only a second acknowledgement reads this. The table holds a few days.
        var acknowledgements = await store.AcknowledgementsAsync(cancellationToken);
        if (!acknowledgements.TryGetValue(key, out var first)) return;
        Late(logger, key, via, (long)Math.Round((now - first.At).TotalSeconds), first.Via);
    }

    [LoggerMessage(EventId = LateId, EventName = "AlertsAcknowledgementLate", Level = LogLevel.Information,
        Message = "Acknowledgement of {Key} via {Via} arrived {LateSeconds} s after the one via {FirstVia}. The first stands.")]
    private static partial void Late(ILogger logger, string key, string via, long lateSeconds, string firstVia);
}
