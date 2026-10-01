using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// What a phone registers for pushes: the token Apple gave the app, and
/// which of Apple's two servers it belongs to. Checked before anything is
/// stored, so the token is always safe in a URL path.
/// </summary>
public static class ApnsPushTokens
{
    public const int MinLength = 64;

    public const int MaxLength = 200;

    public const string Sandbox = "sandbox";

    public const string Production = "production";

    public const int MaxEnvironmentLength = 10;

    /// <summary>Lowercase hex, 64 to 200 characters.</summary>
    public static bool IsToken(string? token) =>
        token is { Length: >= MinLength and <= MaxLength } && token.AsSpan().ContainsAnyExcept(Hex) is false;

    public static bool IsEnvironment(string? environment) => environment is Sandbox or Production;

    /// <summary>The fields that are wrong, by name. Empty when both are right.</summary>
    public static Dictionary<string, string[]> Validate(string? token, string? environment)
    {
        var errors = new Dictionary<string, string[]>();
        if (!IsToken(token)) errors["apnsToken"] = [$"Lowercase hex, {MinLength} to {MaxLength} characters."];
        if (!IsEnvironment(environment)) errors["environment"] = ["\"sandbox\" or \"production\"."];
        return errors;
    }

    private static readonly System.Buffers.SearchValues<char> Hex = System.Buffers.SearchValues.Create("0123456789abcdef");
}

/// <summary>A phone with a push token: where to push, and what it was last sent.</summary>
public sealed record PushTarget(Guid DeviceId, string Token, string Environment, long PushedVersion, Instant? PushedAt);

/// <summary>What Apple answered. Status 0 is no answer: the reason is then the error's kind.</summary>
/// <param name="Reason">Apple's reason word, or null. Letters only, so it is safe to log.</param>
public sealed record ApnsResult(int Status, string? Reason)
{
    public bool Ok => Status == 200;

    /// <summary>Apple says the token is dead or not for this app. It is cleared.</summary>
    public bool DeadToken =>
        Status == 410 || (Status == 400 && Reason is "BadDeviceToken" or "DeviceTokenNotForTopic");

    /// <summary>Too many requests or a server error: tried once more after <see cref="AlertPushWorker.RetryAfter"/>.</summary>
    public bool Retryable => Status == 429 || Status >= 500;
}

/// <summary>
/// The provider token: a JWT signed ES256 with the .p8 key, header kid the
/// key id, claims iss the team id and iat. One is reused for 50 minutes.
/// Apple answers TooManyProviderTokenUpdates to a token refreshed more
/// often than every 20, so a refresh Apple asks for waits until then.
/// </summary>
/// <remarks>
/// The key never leaves this class: it is read from the options when a
/// token is minted and never logged or put in an exception message.
/// </remarks>
public sealed class ApnsTokens(AlertsOptions options, IClock clock)
{
    public static readonly Duration ReuseFor = Duration.FromMinutes(50);

    public static readonly Duration MinAge = Duration.FromMinutes(20);

    private readonly Lock _lock = new();
    private string? _token;
    private Instant _mintedAt;
    private bool _refresh;

    /// <summary>How many tokens this process has minted.</summary>
    public int Minted { get; private set; }

    /// <summary>The token to send. A new one only when it is 50 minutes old, or Apple refused it and it is 20.</summary>
    public string Current()
    {
        lock (_lock)
        {
            var now = clock.GetCurrentInstant();
            var age = now - _mintedAt;
            if (_token is null || age >= ReuseFor || (_refresh && age >= MinAge))
            {
                _token = Mint(options, now);
                _mintedAt = now;
                _refresh = false;
                Minted++;
            }

            return _token;
        }
    }

    /// <summary>Apple said the token expired or is invalid: the next send mints one, once it is 20 minutes old.</summary>
    public void Refused()
    {
        lock (_lock) _refresh = true;
    }

    public static string Mint(AlertsOptions options, Instant now)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(options.ApnsKeyP8);

        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
        {
            ["alg"] = "ES256",
            ["kid"] = options.ApnsKeyId!.Trim()
        }));
        var claims = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = options.ApnsTeamId!.Trim(),
            ["iat"] = now.ToUnixTimeSeconds()
        }));
        var signed = $"{header}.{claims}";

        // IEEE P1363, r then s, 64 bytes: the form JWS ES256 uses (RFC 7518 §3.4).
        var signature = key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256);
        return $"{signed}.{Encode(signature)}";
    }

    /// <summary>A P-256 private key in PEM, as Apple's .p8 file holds it.</summary>
    public static bool IsKey(string? pem)
    {
        if (string.IsNullOrWhiteSpace(pem)) return false;
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(pem);
            return key.KeySize == 256;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            return false;
        }
    }

    private static string Encode(byte[] bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);
}

/// <summary>
/// One background push to one phone: POST /3/device/&lt;token&gt; over HTTP/2.
/// The body asks the app to wake and read /api/alerts/status, and carries
/// the plan version. Nothing else.
/// </summary>
/// <remarks>
/// The token is in the path, so the client is registered with its loggers
/// removed, and Program.cs filters Apple's hosts out of the request traces.
/// </remarks>
public sealed class ApnsClient(HttpClient http, ApnsTokens tokens, IClock clock)
{
    public const string Topic = "tech.abera.alarms";

    public const string SandboxHost = "api.sandbox.push.apple.com";

    public const string ProductionHost = "api.push.apple.com";

    public const string CollapseId = "plan";

    /// <summary>An undelivered push is dropped after an hour. The phone reads the plan when opened anyway.</summary>
    public static readonly Duration Expiry = Duration.FromHours(1);

    public static string Host(string environment) =>
        environment == ApnsPushTokens.Production ? ProductionHost : SandboxHost;

    /// <summary>Whether a request goes to Apple's push service, whose path carries the device token.</summary>
    public static bool IsApnsRequest(Uri? uri) =>
        uri is not null
        && (string.Equals(uri.Host, SandboxHost, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, ProductionHost, StringComparison.OrdinalIgnoreCase));

    public static string Body(long version) =>
        $"{{\"aps\":{{\"content-available\":1}},\"v\":{version.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";

    public async Task<ApnsResult> SendAsync(string token, string environment, long version, CancellationToken cancellationToken)
    {
        if (!ApnsPushTokens.IsToken(token)) return new ApnsResult(0, "NotAToken");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Host(environment)}/3/device/{token}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new StringContent(Body(version), Encoding.UTF8, "application/json")
        };
        var expires = (clock.GetCurrentInstant() + Expiry).ToUnixTimeSeconds();
        request.Headers.TryAddWithoutValidation("apns-push-type", "background");
        request.Headers.TryAddWithoutValidation("apns-priority", "5");
        request.Headers.TryAddWithoutValidation("apns-topic", Topic);
        request.Headers.TryAddWithoutValidation(
            "apns-expiration", expires.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("apns-collapse-id", CollapseId);
        request.Headers.TryAddWithoutValidation("authorization", $"bearer {tokens.Current()}");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            if (status == 200) return new ApnsResult(200, null);

            var reason = await ReasonAsync(response, cancellationToken);
            if (status == 403 && reason is "ExpiredProviderToken" or "InvalidProviderToken") tokens.Refused();
            return new ApnsResult(status, reason);
        }
        catch (HttpRequestException exception)
        {
            return new ApnsResult(0, exception.HttpRequestError == HttpRequestError.Unknown
                ? nameof(HttpRequestException)
                : exception.HttpRequestError.ToString());
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApnsResult(0, "Timeout");
        }
    }

    /// <summary>Apple's {"reason":"..."}, when it is a word of letters. Anything else is not kept.</summary>
    private static async Task<string?> ReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length is 0 or > 4096) return null;
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object
                   && json.RootElement.TryGetProperty("reason", out var reason)
                   && reason.ValueKind == JsonValueKind.String
                   && reason.GetString() is { Length: > 0 and <= 64 } word
                   && word.All(char.IsAsciiLetter)
                ? word
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The wait before the one retry. Replaced in tests, which read the time asked for.</summary>
public class ApnsDelay
{
    public virtual Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}

/// <summary>
/// Pushes each change to the plan to every phone with a push token. A
/// change bumps one plan version in the database. Each phone gets at most
/// one push per 60 seconds, carrying the version current when it goes.
/// </summary>
/// <remarks>
/// Every replica runs this. The version is a row every replica bumps with
/// one statement, and a push is claimed by one UPDATE on the phone's row
/// that only succeeds when the phone is behind and its last push is 60
/// seconds old. Two replicas can both try. One claims and sends.
///
/// A calendar read bumps the version only when its alarms differ from
/// those stored with the version: an alarm added or removed, or its time,
/// start or title changed. The fingerprint is stored with every bump, so
/// the read after a change on the page does not push again.
/// </remarks>
public sealed class AlertPushWorker(
    IServiceScopeFactory scopes,
    AlertsStatus status,
    AlertsOptions options,
    IClock clock,
    ApnsDelay delay,
    ILogger<AlertPushWorker> logger) : BackgroundService
{
    /// <summary>At most one push per phone in this window.</summary>
    public static readonly Duration Window = Duration.FromSeconds(60);

    /// <summary>A 429 or a 5xx is tried once more after this, then left until the next change.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _wake = new(0, 1);

    /// <summary>Pushes go only when the three secrets are set. Tokens are stored either way.</summary>
    public bool On => options.ApnsMissing().Count == 0;

    /// <summary>
    /// Something a phone holds changed: an event's type, a skip, a mute, an
    /// acknowledgement, a new event or a routine alarm. Bumps the version and wakes the sender. A failure
    /// is logged, never thrown: the change itself is already stored.
    /// </summary>
    public Task PlanChangedAsync(CancellationToken cancellationToken) => ChangedAsync(force: true, cancellationToken);

    /// <summary>After a calendar read: bumps the version only when the alarms differ from the stored ones.</summary>
    public Task CalendarReadAsync(CancellationToken cancellationToken) => ChangedAsync(force: false, cancellationToken);

    private async Task ChangedAsync(bool force, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IAlertStore>();
            var snapshot = status.Snapshot();

            // Before the first good read this replica does not know the
            // alarms, so it keeps the stored fingerprint.
            // The plan is the feed's and the events created here, as the
            // status lists it.
            byte[]? fingerprint = null;
            if (snapshot.LastSuccessAt is not null)
            {
                var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
                var plan = await AlertsPlan.CurrentAsync(snapshot, store, settings, clock.GetCurrentInstant(), cancellationToken);
                fingerprint = Fingerprint(plan, await store.EventTypesAsync(cancellationToken), settings);
            }

            if (fingerprint is null && !force) return;

            var devices = scope.ServiceProvider.GetRequiredService<IAlertDeviceStore>();
            if (await devices.BumpPlanAsync(fingerprint, force, clock.GetCurrentInstant(), cancellationToken)) Wake();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Recording a plan change for phone pushes failed ({Failure}).", exception.GetType().Name);
        }
    }

    /// <summary>
    /// The alarms a phone schedules, as a SHA-256: each alarm's key, alert
    /// time, start and title, sorted. Notifications and events that send
    /// nothing are left out: a phone holds only alarms.
    /// </summary>
    public static byte[] Fingerprint(
        IEnumerable<PlannedAlert> plan, IReadOnlyDictionary<string, string> chosen, AlertSettings settings)
    {
        var lines = plan
            .Where(alert => AlertTypes.Resolve(alert, chosen.GetValueOrDefault(alert.EventId), settings).Type == AlertTypes.Alarm)
            .Select(alert => string.Join('\u001f',
                alert.Key,
                alert.AlertAt.ToUnixTimeTicks().ToString(System.Globalization.CultureInfo.InvariantCulture),
                alert.StartsAt.ToUnixTimeTicks().ToString(System.Globalization.CultureInfo.InvariantCulture),
                alert.Title))
            .Order(StringComparer.Ordinal);
        return SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001e', lines)));
    }

    /// <summary>Wakes the sender now rather than at its next minute.</summary>
    public void Wake()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already woken.
        }
    }

    /// <summary>
    /// One pass: claim and send a push to every phone that is behind and
    /// outside its window. Returns when to come back: the end of the first
    /// window still running, or a minute.
    /// </summary>
    public async Task<Instant> PassAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var next = now + Window;
        if (!On) return next;

        await using var scope = scopes.CreateAsyncScope();
        var devices = scope.ServiceProvider.GetRequiredService<IAlertDeviceStore>();
        var version = await devices.PlanVersionAsync(cancellationToken);
        if (version == 0) return next;

        var sends = new List<Task>();
        foreach (var target in await devices.PushTargetsAsync(cancellationToken))
        {
            if (target.PushedVersion >= version) continue;
            if (target.PushedAt is { } at && at > now - Window)
            {
                if (at + Window < next) next = at + Window;
                continue;
            }

            if (!await devices.TryClaimPushAsync(target.DeviceId, version, now, now - Window, cancellationToken)) continue;
            sends.Add(SendAsync(target, version, cancellationToken));
        }

        await Task.WhenAll(sends);
        return next;
    }

    private async Task SendAsync(PushTarget target, long version, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var apns = scope.ServiceProvider.GetRequiredService<ApnsClient>();
            var result = await apns.SendAsync(target.Token, target.Environment, version, cancellationToken);
            if (result.Retryable)
            {
                AlertPushLog.Retrying(logger, target.DeviceId, result.Status, result.Reason ?? "none");
                await delay.WaitAsync(RetryAfter, cancellationToken);
                result = await apns.SendAsync(target.Token, target.Environment, version, cancellationToken);
            }

            if (result.Ok)
            {
                AlertPushLog.Sent(logger, target.DeviceId, version);
            }
            else if (result.DeadToken)
            {
                await scope.ServiceProvider.GetRequiredService<IAlertDeviceStore>()
                    .ClearPushIfAsync(target.DeviceId, target.Token, cancellationToken);
                AlertPushLog.TokenCleared(logger, target.DeviceId, result.Status, result.Reason ?? "none");
            }
            else
            {
                AlertPushLog.Failed(logger, target.DeviceId, result.Status, result.Reason ?? "none");
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The type alone: a key that does not parse must not reach the log.
            AlertPushLog.Failed(logger, target.DeviceId, 0, exception.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Instant next;
            try
            {
                next = await PassAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("The phone push pass failed ({Failure}).", exception.GetType().Name);
                next = clock.GetCurrentInstant() + Window;
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                // The host is stopping, as in CalendarAlertWorker.
                return;
            }

            var wait = next - clock.GetCurrentInstant();
            if (wait < Duration.Zero) wait = Duration.Zero;
            if (wait > Window) wait = Window;

            try
            {
                await _wake.WaitAsync(wait.ToTimeSpan(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

/// <summary>
/// The push outcomes, under fixed ids beside the security events. A line
/// carries the phone's id, Apple's status and reason word, and the plan
/// version. Never the push token, the provider token or the key.
/// </summary>
public static partial class AlertPushLog
{
    public const int SentId = 4013;

    public const int RetryingId = 4014;

    public const int FailedId = 4015;

    public const int TokenClearedId = 4016;

    [LoggerMessage(EventId = SentId, EventName = "AlertsPushSent", Level = LogLevel.Information,
        Message = "Phone push sent to device {DeviceId} for plan version {Version}.")]
    public static partial void Sent(ILogger logger, Guid deviceId, long version);

    [LoggerMessage(EventId = RetryingId, EventName = "AlertsPushRetrying", Level = LogLevel.Warning,
        Message = "Phone push to device {DeviceId} answered {Status} {Reason}. Trying once more in 30 s.")]
    public static partial void Retrying(ILogger logger, Guid deviceId, int status, string reason);

    [LoggerMessage(EventId = FailedId, EventName = "AlertsPushFailed", Level = LogLevel.Warning,
        Message = "Phone push to device {DeviceId} failed ({Status} {Reason}). The next change tries again.")]
    public static partial void Failed(ILogger logger, Guid deviceId, int status, string reason);

    [LoggerMessage(EventId = TokenClearedId, EventName = "AlertsPushTokenCleared", Level = LogLevel.Warning,
        Message = "Apple refused device {DeviceId}'s push token ({Status} {Reason}). It was cleared.")]
    public static partial void TokenCleared(ILogger logger, Guid deviceId, int status, string reason);
}
