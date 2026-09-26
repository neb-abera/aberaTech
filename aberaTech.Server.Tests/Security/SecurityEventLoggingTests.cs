using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using aberaTech.Scheduling.Sms;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// Somebody working at the edges of the site leaves a trail: which address,
/// which route, what happened, under an id an alert can be written against.
/// And the trail holds to the bar the message senders set — no number, no
/// address, no key, no cookie, no capability id ever reaches a log line.
/// </summary>
public sealed class SecurityEventLoggingTests : IDisposable
{
    private const string Category = SecurityEvents.Category;
    private const string Visitor = "203.0.113.7";
    private const string Phone = "+12025550143";
    private const string StrangerEmail = "stranger@example.test";
    private const string GuessedKey = "guess-0123456789abcdef0123456789abcdef";

    private readonly CapturedLogs _logs = new();
    private readonly TestApp _app;
    private readonly HttpClient _client;

    public SecurityEventLoggingTests()
    {
        _app = new TestApp(
            ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable),
            services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddSingleton<ILoggerProvider>(_logs);
            });
        _client = _app.CreateClient();
    }

    [Fact]
    public async Task A_rejected_digest_key_is_an_event_without_the_key()
    {
        using var request = Request(HttpMethod.Get, "/api/fitness/digest.txt");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GuessedKey);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);

        var logged = Single(SecurityEvents.DigestKeyRejected);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.Equal(Visitor, logged.Fields["ClientIp"]);
        Assert.Equal("/api/fitness/digest.txt", logged.Fields["Route"]);
        Assert.DoesNotContain(GuessedKey, logged.Everything);
    }

    [Fact]
    public async Task A_bad_twilio_signature_is_an_event_without_the_number()
    {
        using var request = Request(HttpMethod.Post, SmsReceiptEndpoint.Path);
        request.Headers.Add("X-Twilio-Signature", "forged-signature");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MessageSid"] = "SM123",
            ["MessageStatus"] = "delivered",
            ["To"] = Phone
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);

        var logged = Single(SecurityEvents.WebhookSignatureRejected);
        Assert.Equal(Visitor, logged.Fields["ClientIp"]);
        Assert.DoesNotContain(Phone, logged.Everything);
        Assert.DoesNotContain("forged-signature", logged.Everything);
    }

    [Fact]
    public async Task An_admin_call_with_no_session_is_an_event()
    {
        using var request = Request(HttpMethod.Get, "/api/scheduling/admin/queue");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);

        var logged = Single(SecurityEvents.SignInRequired);
        Assert.Equal("/api/scheduling/admin/queue", logged.Fields["Route"]);
        Assert.Equal(Visitor, logged.Fields["ClientIp"]);
    }

    [Fact]
    public async Task A_google_account_off_the_allowlist_is_an_event_that_does_not_name_it()
    {
        // Anybody with a Google account can complete the sign-in; the
        // allowlist is what refuses them. That refusal is worth knowing
        // about. Whose account it was is not the log's business.
        var cookie = AdminSession.CookieFor(_app.Factory.Services, StrangerEmail);
        var session = cookie[(cookie.IndexOf('=') + 1)..];

        using var request = Request(HttpMethod.Get, "/api/scheduling/admin/queue");
        request.Headers.Add("Cookie", cookie);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);

        var logged = Single(SecurityEvents.AllowlistRefused);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.DoesNotContain(StrangerEmail, logged.Everything);
        Assert.DoesNotContain("Stranger", logged.Everything);
        Assert.DoesNotContain(session, logged.Everything);
    }

    [Fact]
    public async Task Running_out_of_budget_is_an_event()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var request = Request(HttpMethod.Post, "/api/scheduling/queue");
            request.Content = JsonContent.Create(new { name = "", phone = Phone });
            await _client.SendAsync(request);
        }

        var logged = Single(SecurityEvents.RateLimited);
        Assert.Equal("/api/scheduling/queue", logged.Fields["Route"]);
        Assert.Equal(Visitor, logged.Fields["ClientIp"]);
        Assert.Equal(429, logged.Fields["Status"]);
    }

    [Fact]
    public async Task A_refused_join_is_an_event_without_what_was_typed()
    {
        using var request = Request(HttpMethod.Post, "/api/scheduling/queue");
        request.Content = JsonContent.Create(new { name = "Private Snuffy", phone = "+442079460000", smsConsent = true });

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(request)).StatusCode);

        var logged = Single(SecurityEvents.PublicWriteRefused);
        Assert.Equal(LogLevel.Information, logged.Level);
        Assert.DoesNotContain("+442079460000", logged.Everything);
        Assert.DoesNotContain("Snuffy", logged.Everything);
    }

    [Fact]
    public async Task Nothing_the_site_logs_about_these_requests_carries_what_they_carried()
    {
        // Every category, not only the security one: the framework's own
        // loggers are as capable of echoing a header as ours.
        using var digest = Request(HttpMethod.Get, "/api/fitness/digest.txt?key=" + GuessedKey);
        digest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GuessedKey);
        await _client.SendAsync(digest);

        using var join = Request(HttpMethod.Post, "/api/scheduling/queue");
        join.Content = JsonContent.Create(new { name = "", phone = Phone, smsConsent = true });
        await _client.SendAsync(join);

        Assert.NotEmpty(_logs.InCategory(Category));
        Assert.All(_logs.Entries.Where(entry => entry.Level >= LogLevel.Information), entry =>
        {
            Assert.DoesNotContain(GuessedKey, entry.Everything);
            Assert.DoesNotContain(Phone, entry.Everything);
        });
    }

    [Fact]
    public void Event_ids_are_stable_and_distinct()
    {
        // Alerts are written against these numbers. Changing one is a
        // breaking change to whoever is watching.
        Assert.Equal(4001, SecurityEvents.RateLimited);
        Assert.Equal(4002, SecurityEvents.SignInRequired);
        Assert.Equal(4003, SecurityEvents.AllowlistRefused);
        Assert.Equal(4004, SecurityEvents.DigestKeyRejected);
        Assert.Equal(4005, SecurityEvents.WebhookSignatureRejected);
        Assert.Equal(4006, SecurityEvents.UnknownCapability);
        Assert.Equal(4007, SecurityEvents.PublicWriteRefused);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(RemoteAddressStartupFilter.Header, Visitor);
        return request;
    }

    private CapturedLog Single(int eventId) =>
        Assert.Single(_logs.InCategory(Category), entry => entry.EventId.Id == eventId);

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }
}

public sealed class SecurityEventClassificationTests
{
    [Theory]
    [InlineData("/api/scheduling/queue", "POST", 429, false, SecurityEvents.RateLimited)]
    [InlineData("/api/fitness/digest.txt", "GET", 429, false, SecurityEvents.RateLimited)]
    [InlineData("/api/fitness/digest.txt", "GET", 401, false, SecurityEvents.DigestKeyRejected)]
    [InlineData("/api/fitness/summary", "GET", 401, false, SecurityEvents.SignInRequired)]
    [InlineData("/api/fitness/summary", "GET", 403, true, SecurityEvents.AllowlistRefused)]
    [InlineData("/api/scheduling/sms-status", "POST", 403, false, SecurityEvents.WebhookSignatureRejected)]
    [InlineData("/api/scheduling/queue/{entryId:guid}", "GET", 404, false, SecurityEvents.UnknownCapability)]
    [InlineData("/api/scheduling/queue/{entryId:guid}", "DELETE", 404, false, SecurityEvents.UnknownCapability)]
    [InlineData("/api/scheduling/book/{appointmentId:guid}", "DELETE", 404, false, SecurityEvents.UnknownCapability)]
    [InlineData("/api/scheduling/book", "POST", 400, false, SecurityEvents.PublicWriteRefused)]
    [InlineData("/api/devbox/heartbeat", "POST", 401, false, SecurityEvents.AgentTokenRejected)]
    [InlineData("/api/devbox/heartbeat", "POST", 429, false, SecurityEvents.RateLimited)]
    public void Outcomes_that_are_events(string route, string method, int status, bool signedIn, int expected)
    {
        Assert.Equal(expected, SecurityEvents.Classify(route, method, status, signedIn));
    }

    [Theory]
    // A full queue and a slot somebody else took are the site working.
    [InlineData("/api/scheduling/queue", "POST", 409, false)]
    [InlineData("/api/scheduling/book", "POST", 409, false)]
    // The owner mistyping a parameter in their own console.
    [InlineData("/api/fitness/predictions", "GET", 400, true)]
    [InlineData("/api/progress/{key}", "GET", 404, true)]
    public void Outcomes_that_are_not(string route, string method, int status, bool signedIn)
    {
        Assert.Null(SecurityEvents.Classify(route, method, status, signedIn));
    }
}

/// <summary>The one event that needs a database to reach: an id nobody was given.</summary>
public sealed class UnknownCapabilityEventTests
{
    [PostgresFact]
    public async Task An_id_nobody_was_given_is_an_event_without_the_id()
    {
        using var database = new TestDatabase("events");
        var logs = new CapturedLogs();
        using var app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = database.ConnectionString,
                ["ClientAddress:ForwardedHops"] = "0"
            },
            services => services.AddSingleton<ILoggerProvider>(logs));
        using var client = app.CreateClient();

        var guess = Guid.NewGuid();
        foreach (var (method, path) in new[]
                 {
                     (HttpMethod.Get, $"/api/scheduling/queue/{guess}"),
                     (HttpMethod.Delete, $"/api/scheduling/queue/{guess}"),
                     (HttpMethod.Delete, $"/api/scheduling/book/{guess}")
                 })
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(RemoteAddressStartupFilter.Header, "203.0.113.7");
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
        }

        var events = logs.InCategory(SecurityEvents.Category)
            .Where(entry => entry.EventId.Id == SecurityEvents.UnknownCapability)
            .ToList();

        Assert.Equal(3, events.Count);
        Assert.All(events, entry =>
        {
            Assert.Equal("203.0.113.7", entry.Fields["ClientIp"]);
            Assert.DoesNotContain(guess.ToString(), entry.Everything, StringComparison.OrdinalIgnoreCase);
        });

        // And nothing else the app logged at the levels production keeps.
        Assert.All(logs.Entries.Where(entry => entry.Level >= LogLevel.Information), entry =>
            Assert.DoesNotContain(guess.ToString(), entry.Everything, StringComparison.OrdinalIgnoreCase));
    }
}
