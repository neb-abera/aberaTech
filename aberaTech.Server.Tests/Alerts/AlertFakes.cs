using System.Collections.Concurrent;
using System.Net;
using aberaTech.Scheduling.Alerts;
using NodaTime;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeClock(Instant now) : IClock
{
    public Instant Now { get; set; } = now;

    public Instant GetCurrentInstant() => Now;
}

/// <summary>
/// The store's rules, in memory: one claim per key ever, and mute, skip
/// and the settings as plain values. The Postgres store is held to the same rules by
/// DatabaseAlertStoreTests; this one lets the send logic be tested without
/// a database.
/// </summary>
internal sealed class InMemoryAlertStore : IAlertStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _claims = [];
    private readonly Dictionary<string, Instant> _skips = [];
    private Instant? _mutedUntil;
    private AlertSettings? _settings;

    public Task<AlertSettings?> SettingsAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_settings);
    }

    public Task SaveSettingsAsync(AlertSettings settings, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) _settings = settings;
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, string> Claims
    {
        get { lock (_lock) return new Dictionary<string, string>(_claims); }
    }

    public Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_mutedUntil);
    }

    public Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) _mutedUntil = until;
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult<IReadOnlySet<string>>(_skips.Keys.ToHashSet());
    }

    public Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_skips.ContainsKey(key));
    }

    public Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) _skips[key] = startsAt;
        return Task.CompletedTask;
    }

    public Task UnskipAsync(string key, CancellationToken cancellationToken)
    {
        lock (_lock) _skips.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_claims.TryAdd(key, "claimed"));
    }

    public Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) _claims[key] = outcome;
        return Task.CompletedTask;
    }

    public Task PruneAsync(Instant before, CancellationToken cancellationToken) => Task.CompletedTask;

    private readonly Dictionary<string, string> _types = [];

    /// <summary>The event ids each read reported as in the feed, newest last.</summary>
    public List<IReadOnlyCollection<string>> Seen { get; } = [];

    public Task<IReadOnlyDictionary<string, string>> EventTypesAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_types));
    }

    public Task<string?> EventTypeAsync(string eventId, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_types.GetValueOrDefault(eventId));
    }

    public Task SetEventTypeAsync(string eventId, string? type, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (type is null) _types.Remove(eventId);
            else _types[eventId] = type;
        }

        return Task.CompletedTask;
    }

    public Task SeenEventsAsync(IReadOnlyCollection<string> eventIds, Instant now, Instant forgetBefore, CancellationToken cancellationToken)
    {
        lock (_lock) Seen.Add(eventIds);
        return Task.CompletedTask;
    }
}

/// <summary>
/// An HTTP server in a handler: records every request with its form body,
/// and answers with whatever the test queued, then with the default.
/// </summary>
internal sealed class RecordingHandler(Func<HttpResponseMessage> answer) : HttpMessageHandler
{
    public sealed record Seen(HttpMethod Method, Uri Url, IReadOnlyDictionary<string, string> Form);

    private readonly ConcurrentQueue<Func<HttpResponseMessage>> _queued = new();

    public ConcurrentQueue<Seen> Requests { get; } = new();

    public void Then(Func<HttpResponseMessage> next) => _queued.Enqueue(next);

    public static HttpResponseMessage Text(HttpStatusCode status, string body, string type = "text/plain") =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, type) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>();
        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                form[Uri.UnescapeDataString(parts[0].Replace('+', ' '))] =
                    parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
            }
        }

        Requests.Enqueue(new Seen(request.Method, request.RequestUri!, form));
        var next = _queued.TryDequeue(out var queued) ? queued : answer;
        return next();
    }
}
