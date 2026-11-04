using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// Warns through Pushover when a paired phone stops checking in. A phone
/// that is off, out of battery, has the app deleted or offloaded, or has
/// Background App Refresh off cannot say so itself. abera.tech sees the
/// silence. Alarms already set on that phone still ring. Events added or
/// moved since its last check-in are not on it, and Pushover is then the
/// only thing that rings them.
/// </summary>
public static class PhoneWatch
{
    /// <summary>The same 12 hours after which the app's Ready to Ring screen calls its sync stale.</summary>
    public static readonly Duration QuietAfter = Duration.FromHours(12);

    private static readonly InstantPattern KeyTime = InstantPattern.CreateWithInvariantCulture("uuuuMMdd'T'HHmmss'Z'");

    /// <summary>
    /// The claim that makes the warning go once per silence, on every
    /// replica and across restarts. A check-in after it starts a new one.
    /// </summary>
    public static string ClaimKey(AlertDevice device) =>
        $"phone-quiet|{device.Id:N}|{KeyTime.Format(device.LastSeenAt ?? device.CreatedAt)}";

    /// <summary>The phones quiet for longer than <see cref="QuietAfter"/>.</summary>
    public static IEnumerable<AlertDevice> Quiet(IEnumerable<AlertDevice> devices, Instant now) =>
        devices.Where(device => now - (device.LastSeenAt ?? device.CreatedAt) > QuietAfter);

    public static string Message(AlertDevice device, Instant now, DateTimeZone zone)
    {
        var seen = device.LastSeenAt ?? device.CreatedAt;
        var hours = (int)(now - seen).TotalHours;
        var at = seen.InZone(zone).ToString("ddd d MMM h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        return $"{device.Name} last checked in {hours} hours ago, {at}. Open the Alarms app on it so new and moved alarms are set. Alarms already on it still ring.";
    }

    /// <summary>Sends one warning per quiet phone. Each failure is logged, never thrown.</summary>
    public static async Task CheckAsync(
        IServiceScopeFactory scopes, AlertSettings settings, DateTimeZone zone, Instant now, ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (scope.ServiceProvider.GetService<IAlertDeviceStore>() is not { } devices) return;
        var store = scope.ServiceProvider.GetRequiredService<IAlertStore>();

        foreach (var device in Quiet(await devices.ListAsync(cancellationToken), now))
        {
            if (!await store.TryClaimAsync(ClaimKey(device), now, now, cancellationToken)) continue;
            var delivery = settings.NotificationDelivery with { Priority = 1 };
            var result = await scope.ServiceProvider.GetRequiredService<PushoverClient>().SendAsync(
                "Your phone is not checking in", Message(device, now, zone), delivery, cancellationToken);
            await store.RecordOutcomeAsync(ClaimKey(device), result.Outcome, now, cancellationToken);
            // The silence, never the phone's name: the log leaves this process.
            if (result.Ok) logger.LogInformation("Quiet phone warning sent after {Hours} hours.", (int)(now - (device.LastSeenAt ?? device.CreatedAt)).TotalHours);
            else logger.LogWarning("Quiet phone warning failed ({Failure}).", result.Error);
        }
    }
}
