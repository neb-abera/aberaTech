using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The Apple side of a push: the provider token and the one request. The
/// key is a P-256 key made for the test, so the signature is checked with
/// its public half.
/// </summary>
public sealed class ApnsClientTests : IDisposable
{
    private static readonly Instant Noon = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly FakeClock _clock = new(Noon);
    private readonly ApnsHandler _apple = new();
    private readonly AlertsOptions _options;
    private readonly ApnsTokens _tokens;

    public ApnsClientTests()
    {
        _options = ApnsFixture.Options(_key);
        _tokens = new ApnsTokens(_options, _clock);
    }

    public void Dispose() => _key.Dispose();

    private ApnsClient Client() => new(new HttpClient(_apple), _tokens, _clock);

    [Fact]
    public async Task A_push_is_a_background_post_over_http2_with_exactly_the_headers_and_body_apple_needs()
    {
        var result = await Client().SendAsync(ApnsFixture.Token, "production", 42, CancellationToken.None);

        Assert.True(result.Ok);
        var seen = Assert.Single(_apple.Requests);
        Assert.Equal(HttpMethod.Post, seen.Method);
        Assert.Equal($"https://api.push.apple.com/3/device/{ApnsFixture.Token}", seen.Url.ToString());
        Assert.Equal(HttpVersion.Version20, seen.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, seen.VersionPolicy);
        Assert.Equal("{\"aps\":{\"content-available\":1},\"v\":42}", seen.Body);
        Assert.Equal("application/json", seen.ContentType);
        Assert.Equal("background", seen.Headers["apns-push-type"]);
        Assert.Equal("5", seen.Headers["apns-priority"]);
        Assert.Equal("tech.abera.alarms", seen.Headers["apns-topic"]);
        Assert.Equal((Noon + Duration.FromHours(1)).ToUnixTimeSeconds().ToString(), seen.Headers["apns-expiration"]);
        Assert.Equal("plan", seen.Headers["apns-collapse-id"]);
        Assert.StartsWith("bearer ", seen.Headers["authorization"]);
        Assert.Equal(
            ["apns-collapse-id", "apns-expiration", "apns-priority", "apns-push-type", "apns-topic", "authorization"],
            seen.Headers.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_sandbox_token_goes_to_the_sandbox_host()
    {
        await Client().SendAsync(ApnsFixture.Token, "sandbox", 1, CancellationToken.None);

        Assert.Equal("api.sandbox.push.apple.com", Assert.Single(_apple.Requests).Url.Host);
    }

    [Fact]
    public void The_provider_token_is_es256_with_kid_iss_and_iat_and_verifies_with_the_keys_public_half()
    {
        var jwt = _tokens.Current();

        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(parts[0]));
        using var claims = JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal(ApnsFixture.KeyId, header.RootElement.GetProperty("kid").GetString());
        Assert.Equal(2, header.RootElement.EnumerateObject().Count());
        Assert.Equal(ApnsFixture.TeamId, claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal(Noon.ToUnixTimeSeconds(), claims.RootElement.GetProperty("iat").GetInt64());
        Assert.Equal(2, claims.RootElement.EnumerateObject().Count());

        using var verifier = ECDsa.Create(_key.ExportParameters(includePrivateParameters: false));
        var signature = System.Buffers.Text.Base64Url.DecodeFromChars(parts[2]);
        Assert.Equal(64, signature.Length);
        Assert.True(verifier.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256));

        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.False(stranger.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public void A_token_is_reused_for_50_minutes_and_a_new_one_minted_after()
    {
        var first = _tokens.Current();

        _clock.Now = Noon + Duration.FromMinutes(49) + Duration.FromSeconds(59);
        Assert.Equal(first, _tokens.Current());
        Assert.Equal(1, _tokens.Minted);

        _clock.Now = Noon + Duration.FromMinutes(50);
        Assert.NotEqual(first, _tokens.Current());
        Assert.Equal(2, _tokens.Minted);
    }

    [Fact]
    public async Task A_refused_provider_token_is_replaced_only_once_it_is_20_minutes_old()
    {
        var first = _tokens.Current();
        _apple.Then(() => ApnsHandler.Answer(HttpStatusCode.Forbidden, "ExpiredProviderToken"));

        var refused = await Client().SendAsync(ApnsFixture.Token, "production", 1, CancellationToken.None);
        Assert.Equal(403, refused.Status);
        Assert.Equal("ExpiredProviderToken", refused.Reason);

        _clock.Now = Noon + Duration.FromMinutes(19);
        Assert.Equal(first, _tokens.Current());

        _clock.Now = Noon + Duration.FromMinutes(20);
        Assert.NotEqual(first, _tokens.Current());
        Assert.Equal(2, _tokens.Minted);
    }

    [Theory]
    [InlineData(410, "Unregistered", true)]
    [InlineData(400, "BadDeviceToken", true)]
    [InlineData(400, "DeviceTokenNotForTopic", true)]
    [InlineData(400, "BadCollapseId", false)]
    [InlineData(429, "TooManyRequests", false)]
    [InlineData(500, "InternalServerError", false)]
    public async Task Apples_answer_is_read_for_its_status_and_reason(int status, string reason, bool dead)
    {
        _apple.Then(() => ApnsHandler.Answer((HttpStatusCode)status, reason));

        var result = await Client().SendAsync(ApnsFixture.Token, "production", 1, CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(dead, result.DeadToken);
        Assert.Equal(status is 429 or 500, result.Retryable);
    }

    [Fact]
    public async Task A_reason_that_is_not_a_word_is_not_kept()
    {
        _apple.Then(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"reason\":\"<script>\"}", Encoding.UTF8, "application/json")
        });

        Assert.Null((await Client().SendAsync(ApnsFixture.Token, "production", 1, CancellationToken.None)).Reason);
    }

    [Fact]
    public async Task A_token_that_is_not_lowercase_hex_is_never_put_in_a_url()
    {
        var result = await Client().SendAsync("../" + ApnsFixture.Token, "production", 1, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Empty(_apple.Requests);
    }

    [Theory]
    [InlineData("https://api.push.apple.com/3/device/abc", true)]
    [InlineData("https://api.sandbox.push.apple.com/3/device/abc", true)]
    [InlineData("https://api.pushover.net/1/messages.json", false)]
    [InlineData("https://push.apple.com.example.test/3/device/abc", false)]
    public void Apples_hosts_are_recognised_for_the_trace_filter(string url, bool apple) =>
        Assert.Equal(apple, ApnsClient.IsApnsRequest(new Uri(url)));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("-----BEGIN PRIVATE KEY-----\nnot a key\n-----END PRIVATE KEY-----", false)]
    public void Only_a_p256_private_key_counts_as_the_key(string? pem, bool key) => Assert.Equal(key, ApnsTokens.IsKey(pem));

    [Fact]
    public void A_p256_key_counts_and_a_p384_key_does_not()
    {
        Assert.True(ApnsTokens.IsKey(_key.ExportPkcs8PrivateKeyPem()));
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.False(ApnsTokens.IsKey(p384.ExportPkcs8PrivateKeyPem()));
    }
}

/// <summary>A test key, key id and team id, and a device token.</summary>
internal static class ApnsFixture
{
    public const string KeyId = "TESTKEY123";

    public const string TeamId = "TESTTEAM12";

    /// <summary>64 lowercase hex characters, the length iOS gives today.</summary>
    public static readonly string Token = string.Concat(Enumerable.Repeat("0123456789abcdef", 4));

    public static readonly string OtherToken = string.Concat(Enumerable.Repeat("fedcba9876543210", 5));

    public static AlertsOptions Options(ECDsa key, AlertsOptions? from = null) => new()
    {
        CalendarIcsUrl = from?.CalendarIcsUrl,
        PushoverAppToken = from?.PushoverAppToken,
        PushoverUserKey = from?.PushoverUserKey,
        PushoverRetrySeconds = from?.PushoverRetrySeconds ?? 0,
        ApnsKeyP8 = key.ExportPkcs8PrivateKeyPem(),
        ApnsKeyId = KeyId,
        ApnsTeamId = TeamId
    };
}

/// <summary>Apple in a handler: records each request whole, answers 200 unless the test queued otherwise.</summary>
internal sealed class ApnsHandler : HttpMessageHandler
{
    public sealed record Seen(
        HttpMethod Method,
        Uri Url,
        Version Version,
        HttpVersionPolicy VersionPolicy,
        IReadOnlyDictionary<string, string> Headers,
        string Body,
        string? ContentType);

    private readonly System.Collections.Concurrent.ConcurrentQueue<Func<HttpResponseMessage>> _queued = new();

    public System.Collections.Concurrent.ConcurrentQueue<Seen> Requests { get; } = new();

    public void Then(Func<HttpResponseMessage> next) => _queued.Enqueue(next);

    public static HttpResponseMessage Answer(HttpStatusCode status, string reason) => new(status)
    {
        Content = new StringContent($"{{\"reason\":\"{reason}\"}}", Encoding.UTF8, "application/json")
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(
            header => header.Key.ToLowerInvariant(), header => string.Join(",", header.Value), StringComparer.Ordinal);
        Requests.Enqueue(new Seen(
            request.Method,
            request.RequestUri!,
            request.Version,
            request.VersionPolicy,
            headers,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Content?.Headers.ContentType?.MediaType));
        return _queued.TryDequeue(out var next) ? next() : new HttpResponseMessage(HttpStatusCode.OK);
    }
}
