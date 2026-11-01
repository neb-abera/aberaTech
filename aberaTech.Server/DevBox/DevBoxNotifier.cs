using System.Text.Json.Serialization;
using aberaTech.Scheduling.Alerts;

namespace aberaTech.Server.DevBox;

/// <summary>What the box wants on the owner's phone.</summary>
public sealed record DevBoxNotice(
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("session")] string? Session = null);

/// <summary>
/// Sends dev box notices through Pushover at normal priority, at most
/// <see cref="MaxPerHour"/> an hour, so a chatty session cannot flood the
/// phone. A failure notice is exempt: the owner must hear it.
/// </summary>
public sealed class DevBoxNotifier(IServiceProvider services, TimeProvider clock, ILogger<DevBoxNotifier> logger)
{
    public const int MaxPerHour = 20;

    private readonly Lock _lock = new();
    private readonly Queue<DateTimeOffset> _sent = new();

    public async Task SendAsync(DevBoxNotice notice, string? url, CancellationToken cancellationToken)
    {
        var pushover = services.GetService<PushoverClient>();
        if (pushover is null) return;
        if (notice.Kind != "failure" && !Allowed()) return;

        var result = await pushover.SendAsync(
            notice.Title ?? "Dev box",
            notice.Message ?? "A session needs you.",
            new PushoverDelivery(0, null, null, null, Url: url),
            cancellationToken);
        if (!result.Ok) logger.LogWarning("Dev box notice failed: {Error}", result.Error);
    }

    private bool Allowed()
    {
        var now = clock.GetUtcNow();
        lock (_lock)
        {
            while (_sent.TryPeek(out var oldest) && now - oldest > TimeSpan.FromHours(1)) _sent.Dequeue();
            if (_sent.Count >= MaxPerHour) return false;
            _sent.Enqueue(now);
            return true;
        }
    }
}
