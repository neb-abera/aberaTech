using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// Whether Pushover has a device to deliver to. Pushover accepts a message
/// for a user with no active device and answers success, so a send alone
/// cannot tell. Read from users/validate.json at most every 10 minutes, and
/// a minute after a failed read.
/// </summary>
public sealed class PushoverDevices(IServiceScopeFactory scopes, IClock clock, ILogger<PushoverDevices> logger)
{
    public static readonly Duration Fresh = Duration.FromMinutes(10);
    public static readonly Duration AfterFailure = Duration.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private PushoverAccount? _devices;
    private Instant _until;

    /// <summary>The devices and the licence. Null when Pushover could not be read.</summary>
    public async Task<PushoverAccount?> CurrentAsync(CancellationToken cancellationToken)
    {
        if (clock.GetCurrentInstant() < _until) return _devices;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (clock.GetCurrentInstant() < _until) return _devices;
            using var scope = scopes.CreateScope();
            _devices = await scope.ServiceProvider.GetRequiredService<PushoverClient>().DevicesAsync(cancellationToken);
            _until = clock.GetCurrentInstant() + (_devices is null ? AfterFailure : Fresh);
            if (_devices is null) logger.LogWarning("Pushover's device list could not be read.");
            else if (_devices.Devices.Count == 0) logger.LogWarning("Pushover has no active device for the user key.");
            return _devices;
        }
        finally
        {
            _gate.Release();
        }
    }
}
