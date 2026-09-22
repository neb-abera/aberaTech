using System.Text.Json.Serialization;

namespace aberaTech.Server.DevBox;

/// <summary>What the box last reported about itself.</summary>
public sealed record DevBoxHeartbeat(
    [property: JsonPropertyName("remoteControl")] string? RemoteControl,
    [property: JsonPropertyName("sessions")] int Sessions,
    [property: JsonPropertyName("load")] double Load,
    [property: JsonPropertyName("uptimeSeconds")] long UptimeSeconds,
    [property: JsonPropertyName("holdUntil")] DateTimeOffset? HoldUntil,
    [property: JsonPropertyName("environmentUrl")] string? EnvironmentUrl);

/// <summary>What the box is asked to do on its next heartbeat.</summary>
public sealed record DevBoxCommands(
    [property: JsonPropertyName("holdMinutes")] int? HoldMinutes,
    [property: JsonPropertyName("park")] bool Park);

/// <summary>
/// The channel between the box and this site: the box reports once a minute
/// over an outbound call and takes its orders from the reply. No inbound port,
/// no SSH from the site, nothing to expose. One process, one replica, so the
/// state lives in memory; a restart loses the last report and the next one is
/// at most a minute away.
/// </summary>
public sealed class DevBoxAgentState(TimeProvider clock)
{
    private readonly Lock _lock = new();
    private DevBoxHeartbeat? _last;
    private DateTimeOffset? _lastAt;
    private int? _holdMinutes;
    private bool _park;

    public void Report(DevBoxHeartbeat heartbeat)
    {
        lock (_lock)
        {
            _last = heartbeat;
            _lastAt = clock.GetUtcNow();
        }
    }

    /// <summary>The last report and how old it is, or null if the box has never called.</summary>
    public (DevBoxHeartbeat Heartbeat, double SeenSecondsAgo)? Last()
    {
        lock (_lock)
        {
            return _last is null || _lastAt is null
                ? null
                : (_last, Math.Max(0, (clock.GetUtcNow() - _lastAt.Value).TotalSeconds));
        }
    }

    public void Hold(int minutes)
    {
        lock (_lock) _holdMinutes = minutes;
    }

    public void Park()
    {
        lock (_lock) _park = true;
    }

    /// <summary>Hands the queued commands to the box and forgets them.</summary>
    public DevBoxCommands Take()
    {
        lock (_lock)
        {
            var commands = new DevBoxCommands(_holdMinutes, _park);
            _holdMinutes = null;
            _park = false;
            return commands;
        }
    }

    public DevBoxCommands Pending()
    {
        lock (_lock) return new DevBoxCommands(_holdMinutes, _park);
    }
}
