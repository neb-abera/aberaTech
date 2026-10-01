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
        lock (_lock)
        {
            if (!_claims.TryAdd(key, "claimed")) return Task.FromResult(false);
            _starts[key] = startsAt;
            return Task.FromResult(true);
        }
    }

    private readonly Dictionary<string, Instant> _starts = [];
    private readonly Dictionary<string, string> _receipts = [];
    private readonly Dictionary<string, AlertAcknowledgement> _acknowledgements = [];

    public Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken, string? receipt = null)
    {
        BeforeRecordOutcome?.Invoke();
        lock (_lock)
        {
            _claims[key] = outcome;
            if (receipt is not null) _receipts[key] = receipt;
            else _receipts.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>Runs as Pushover's answer is about to be stored, so a test can acknowledge while the message is on its way.</summary>
    public Action? BeforeRecordOutcome { get; set; }

    public Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_claims.TryGetValue(key, out var outcome)
                ? new AlertDelivery(_starts.GetValueOrDefault(key), outcome, _receipts.GetValueOrDefault(key))
                : null);
        }
    }

    public Task<IReadOnlyDictionary<string, AlertAcknowledgement>> AcknowledgementsAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyDictionary<string, AlertAcknowledgement>>(
                new Dictionary<string, AlertAcknowledgement>(_acknowledgements));
    }

    public Task<bool> IsAcknowledgedAsync(string key, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_acknowledgements.ContainsKey(key));
    }

    public Task<bool> AcknowledgeAsync(string key, Instant startsAt, string via, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_acknowledgements.TryAdd(key, new AlertAcknowledgement(now, via)));
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

    private readonly Dictionary<string, CreatedAlertEvent> _created = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<CreatedAlertEvent>> CreatedEventsAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<CreatedAlertEvent>>([.. _created.Values.OrderBy(created => created.StartsAt)]);
    }

    public Task AddCreatedEventAsync(CreatedAlertEvent created, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock) _created.TryAdd(created.EventId, created);
        return Task.CompletedTask;
    }

    public Task ForgetCreatedEventsAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            foreach (var id in eventIds) _created.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task UpdateCreatedEventAsync(CreatedAlertEvent updated, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_created.ContainsKey(updated.EventId)) _created[updated.EventId] = updated;
        }

        return Task.CompletedTask;
    }

    private readonly List<AlertEventChange> _changes = [];

    public Task<IReadOnlyList<AlertEventChange>> EventChangesAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<AlertEventChange>>(
                [.. _changes.OrderBy(change => change.CreatedAt).ThenBy(change => change.Id)]);
    }

    public Task AddEventChangeAsync(AlertEventChange change, CancellationToken cancellationToken)
    {
        lock (_lock) _changes.Add(change);
        return Task.CompletedTask;
    }

    public Task ForgetEventChangesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        lock (_lock) _changes.RemoveAll(change => ids.Contains(change.Id));
        return Task.CompletedTask;
    }

    private readonly Dictionary<Guid, AlertRoutine> _routines = [];

    public Task<IReadOnlyList<AlertRoutine>> RoutinesAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(AlertRoutines.Sorted(_routines.Values));
    }

    public Task<bool> AddRoutineAsync(AlertRoutine routine, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_routines.Count >= AlertRoutines.MaxRoutines) return Task.FromResult(false);
            return Task.FromResult(_routines.TryAdd(routine.Id, routine));
        }
    }

    public Task<bool> UpdateRoutineAsync(AlertRoutine routine, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_routines.ContainsKey(routine.Id)) return Task.FromResult(false);
            _routines[routine.Id] = routine;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteRoutineAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_routines.Remove(id));
    }

    private readonly Dictionary<Guid, AlertCountdown> _countdowns = [];

    public Task<IReadOnlyList<AlertCountdown>> CountdownsAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(AlertCountdowns.Sorted(_countdowns.Values));
    }

    public Task<bool> AddCountdownAsync(AlertCountdown countdown, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_countdowns.Count >= AlertCountdowns.MaxCountdowns) return Task.FromResult(false);
            return Task.FromResult(_countdowns.TryAdd(countdown.Id, countdown));
        }
    }

    public Task<bool> UpdateCountdownAsync(AlertCountdown countdown, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_countdowns.ContainsKey(countdown.Id)) return Task.FromResult(false);
            _countdowns[countdown.Id] = countdown;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteCountdownAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_countdowns.Remove(id));
    }
}

/// <summary>
/// The paired phones in memory, with the database store's rules: at most
/// five, looked up by the token's hash, a revoked one gone at once, one
/// plan version, and a push claimed by one caller per window.
/// DatabaseAlertDeviceStoreTests holds the Postgres store to the same rules.
/// </summary>
internal sealed class InMemoryAlertDeviceStore : IAlertDeviceStore
{
    private sealed class Row
    {
        public required AlertDevice Device { get; set; }

        public required byte[] Hash { get; init; }

        public string? ApnsToken { get; set; }

        public string? ApnsEnvironment { get; set; }

        public long PushedVersion { get; set; }

        public Instant? PushedAt { get; set; }
    }

    private readonly Lock _lock = new();
    private readonly List<Row> _devices = [];
    private long _version;
    private byte[]? _fingerprint;

    /// <summary>How many times LastSeenAt was written.</summary>
    public int Touches { get; private set; }

    public IReadOnlyList<byte[]> Hashes
    {
        get { lock (_lock) return [.. _devices.Select(row => row.Hash)]; }
    }

    public Task<IReadOnlyList<AlertDevice>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<AlertDevice>>(
                [.. _devices.Select(row => row.Device with { Push = row.ApnsToken is not null })]);
    }

    public Task<AlertDevice?> CreateAsync(Guid id, string name, byte[] tokenHash, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_devices.Count >= AlertDeviceTokens.MaxDevices) return Task.FromResult<AlertDevice?>(null);
            var device = new AlertDevice(id, name, now, null);
            _devices.Add(new Row { Device = device, Hash = tokenHash });
            return Task.FromResult<AlertDevice?>(device);
        }
    }

    public Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_devices.RemoveAll(row => row.Device.Id == id) > 0);
    }

    public Task<AlertDevice?> FindAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult(
                _devices.FirstOrDefault(row => row.Hash.AsSpan().SequenceEqual(tokenHash)) is { } row
                    ? row.Device with { Push = row.ApnsToken is not null }
                    : null);
    }

    public Task TouchAsync(Guid id, Instant now, Instant unlessAfter, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_devices.FirstOrDefault(row => row.Device.Id == id) is { } row
                && (row.Device.LastSeenAt is not { } seen || seen <= unlessAfter))
            {
                row.Device = row.Device with { LastSeenAt = now };
                Touches++;
            }
        }

        return Task.CompletedTask;
    }

    public Task SetPushAsync(Guid id, string? token, string? environment, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_devices.FirstOrDefault(row => row.Device.Id == id) is { } row)
            {
                row.ApnsToken = token;
                row.ApnsEnvironment = environment;
                row.PushedVersion = _version;
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearPushIfAsync(Guid id, string token, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_devices.FirstOrDefault(row => row.Device.Id == id && row.ApnsToken == token) is { } row)
            {
                row.ApnsToken = null;
                row.ApnsEnvironment = null;
            }
        }

        return Task.CompletedTask;
    }

    public Task<long> PlanVersionAsync(CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_version);
    }

    public Task<bool> BumpPlanAsync(byte[]? fingerprint, bool force, Instant now, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var differs = fingerprint is not null && (_fingerprint is null || !_fingerprint.AsSpan().SequenceEqual(fingerprint));
            if (_version > 0 && !force && !differs) return Task.FromResult(false);
            _version++;
            if (fingerprint is not null) _fingerprint = fingerprint;
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<PushTarget>> PushTargetsAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<PushTarget>>(
            [
                .. _devices
                    .Where(row => row.ApnsToken is not null && row.ApnsEnvironment is not null)
                    .Select(row => new PushTarget(row.Device.Id, row.ApnsToken!, row.ApnsEnvironment!, row.PushedVersion, row.PushedAt))
            ]);
    }

    public Task<bool> TryClaimPushAsync(Guid id, long version, Instant now, Instant windowStart, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_devices.FirstOrDefault(row => row.Device.Id == id) is not { ApnsToken: not null } row
                || row.PushedVersion >= version
                || row.PushedAt > windowStart)
            {
                return Task.FromResult(false);
            }

            row.PushedVersion = version;
            row.PushedAt = now;
            return Task.FromResult(true);
        }
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

    /// <summary>
    /// pushover.net/api#sounds is answered here and kept out of
    /// <see cref="Requests"/>, so a test that counts the messages sent is not
    /// thrown by the page reading the sound list. The built-ins by default.
    /// </summary>
    public Func<HttpResponseMessage> Sounds { get; set; } = () => SoundList();

    /// <summary>Every sounds.json request, with its URL.</summary>
    public ConcurrentQueue<Uri> SoundRequests { get; } = new();

    /// <summary>A sounds.json answer: the built-ins and these uploads.</summary>
    public static HttpResponseMessage SoundList(params (string Name, string Description)[] custom)
    {
        var sounds = PushoverSounds.BuiltIn.ToDictionary(sound => sound.Name, sound => sound.Description);
        foreach (var (name, description) in custom) sounds[name] = description;
        return Text(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new { sounds, status = 1 }), "application/json");
    }

    public static HttpResponseMessage Text(HttpStatusCode status, string body, string type = "text/plain") =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, type) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/1/sounds.json")
        {
            SoundRequests.Enqueue(request.RequestUri);
            return Sounds();
        }

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
