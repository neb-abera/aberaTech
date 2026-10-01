using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The paired phone's side of /api/alerts: pairing on the page, the token
/// on the routes a phone needs, a 403 on the rest, and Acknowledge.
/// The standup is marked #critical, so it is an alarm and goes at emergency
/// priority with a receipt.
/// </summary>
public sealed class AlertDeviceRouteTests : IDisposable
{
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    /// <summary>08:45 New York: the standup's reminder.</summary>
    private static readonly Instant AlertTime = Instant.FromUtc(2026, 10, 28, 12, 45);

    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly CapturedLogs _logs = new();

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\",\"receipt\":\"rcpt30lettersanddigits0000000a\"}", "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(
            Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]),
            Event("review@google.com", "Review", "20261028T140000", "20261028T150000")),
        "text/calendar"));

    private readonly FakeGoogleCalendar _google = new();

    private readonly TestApp _app;

    public AlertDeviceRouteTests()
    {
        var settings = AlertsRouteTests.Settings();
        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton<IAlertStore>(_store);
            services.AddSingleton<IAlertDeviceStore>(_devices);
            services.AddSingleton<ILoggerProvider>(_logs);
            services.AddSingleton(new AlertsOptions
            {
                CalendarIcsUrl = AlertsRouteTests.FeedUrl,
                PushoverAppToken = AlertsRouteTests.AppToken,
                PushoverUserKey = AlertsRouteTests.UserKey,
                PushoverRetrySeconds = 0
            });
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
            services.AddSingleton<IAlertCalendarGrant>(_google);
            services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
        });
        _google.Seed("standup@google.com", "#critical");
        Worker.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private HttpClient Phone(string token)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private async Task<JsonElement> PairAsync(string name = "Neb's iPhone")
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync("/api/alerts/devices", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> KeyOf(string title)
    {
        using var owner = Owner();
        return (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray()
            .Single(alert => alert.GetProperty("title").GetString() == title).GetProperty("key").GetString()!;
    }

    /// <summary>The routes a phone may call, with a body each accepts.</summary>
    public static IEnumerable<object?[]> PhoneRoutes =>
    [
        ["GET", "/api/alerts/status", null],
        ["POST", "/api/alerts/mute", "{\"until\":\"hour\"}"],
        ["POST", "/api/alerts/unmute", "{}"],
        ["POST", "/api/alerts/skip", "{\"key\":\"standup@google.com|20261028T130000Z\"}"],
        ["POST", "/api/alerts/unskip", "{\"key\":\"standup@google.com|20261028T130000Z\"}"],
        ["POST", "/api/alerts/ack", "{\"key\":\"standup@google.com|20261028T130000Z\",\"via\":\"phone\"}"],
        ["PUT", "/api/alerts/event-type", "{\"key\":\"standup@google.com|20261028T130000Z\",\"type\":\"none\"}"],
        ["POST", "/api/alerts/events", "{\"title\":\"Dentist\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["PUT", "/api/alerts/events", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"Standup\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["POST", "/api/alerts/events/delete", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\"}"],
        ["PUT", "/api/alerts/phone-settings", "{\"sound\":\"chime\",\"snoozeMinutes\":10}"]
    ];

    /// <summary>The routes that stay the owner's cookie alone.</summary>
    public static IEnumerable<object?[]> OwnerOnlyRoutes =>
    [
        ["PUT", "/api/alerts/settings", "{}"],
        ["POST", "/api/alerts/test", null],
        ["POST", "/api/alerts/test-notification", null],
        ["POST", "/api/alerts/test-event", "{\"key\":\"standup@google.com|20261028T130000Z\"}"],
        ["GET", "/api/alerts/devices", null],
        ["POST", "/api/alerts/devices", "{\"name\":\"Second phone\"}"],
        ["DELETE", "/api/alerts/devices/0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10", null]
    ];

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Pairing_answers_the_token_once_as_a_link_and_stores_only_its_hash()
    {
        var paired = await PairAsync("  Neb's iPhone  ");

        var token = paired.GetProperty("token").GetString()!;
        Assert.Matches("^aat_[A-Za-z0-9_-]{43}$", token);
        Assert.Equal("Neb's iPhone", paired.GetProperty("name").GetString());
        Assert.Equal(Eight.ToDateTimeOffset(), paired.GetProperty("createdAt").GetDateTimeOffset());
        Assert.True(Guid.TryParse(paired.GetProperty("id").GetString(), out _));
        // Not abera.tech: the link names the server, for a development phone.
        Assert.Equal(
            $"aberaalarms://pair#token={token}&server=http%3A%2F%2Flocalhost",
            paired.GetProperty("pairUrl").GetString());

        var hash = Assert.Single(_devices.Hashes);
        Assert.Equal(SHA256.HashData(Encoding.ASCII.GetBytes(token)), hash);

        // The list never carries the token or its hash, and a second read
        // cannot recover it.
        using var owner = Owner();
        var text = await owner.GetStringAsync("/api/alerts/devices");
        Assert.DoesNotContain(token, text);
        Assert.DoesNotContain(Convert.ToBase64String(hash), text);
        Assert.DoesNotContain("tokenHash", text, StringComparison.OrdinalIgnoreCase);
        var listed = Assert.Single(JsonDocument.Parse(text).RootElement.EnumerateArray());
        Assert.Equal(["id", "name", "createdAt", "lastSeenAt", "push"], listed.EnumerateObject().Select(field => field.Name));
        Assert.Equal(JsonValueKind.Null, listed.GetProperty("lastSeenAt").ValueKind);
    }

    [Fact]
    public void The_pairing_link_leaves_the_server_out_on_abera_tech()
    {
        Assert.Equal(
            "aberaalarms://pair#token=aat_x",
            AlertDeviceTokens.PairUrl("aat_x", "https://abera.tech", "abera.tech"));
        Assert.Equal(
            "aberaalarms://pair#token=aat_x&server=https%3A%2F%2Fstaging.example.test",
            AlertDeviceTokens.PairUrl("aat_x", "https://staging.example.test", "staging.example.test"));
    }

    [Fact]
    public void Tokens_are_47_characters_of_base64url_and_never_repeat()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => AlertDeviceTokens.New()).ToList();

        Assert.All(tokens, token => Assert.True(AlertDeviceTokens.IsWellFormed(token), token));
        Assert.Equal(200, tokens.Distinct().Count());
        Assert.False(AlertDeviceTokens.IsWellFormed(null));
        Assert.False(AlertDeviceTokens.IsWellFormed("aat_" + new string('A', 42)));
        Assert.False(AlertDeviceTokens.IsWellFormed("aat_" + new string('A', 44)));
        Assert.False(AlertDeviceTokens.IsWellFormed("aat_" + new string('A', 42) + "="));
        Assert.False(AlertDeviceTokens.IsWellFormed("aat_" + new string('A', 42) + "+"));
        Assert.False(AlertDeviceTokens.IsWellFormed("xyz_" + new string('A', 43)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"   \"}")]
    [InlineData("{\"name\":\"a\\u0007bell\"}")]
    public async Task A_name_the_list_could_not_show_is_refused_and_nothing_is_paired(string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/devices", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _devices.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_sixty_one_character_name_is_refused_and_sixty_is_taken()
    {
        using var owner = Owner();

        using var long61 = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = new string('n', 61) });
        using var long60 = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = new string('n', 60) });

        Assert.Equal(HttpStatusCode.BadRequest, long61.StatusCode);
        Assert.Equal(HttpStatusCode.Created, long60.StatusCode);
    }

    [Fact]
    public async Task A_sixth_phone_is_refused_until_one_is_revoked()
    {
        var first = await PairAsync("1");
        for (var n = 2; n <= AlertDeviceTokens.MaxDevices; n++) await PairAsync($"{n}");
        using var owner = Owner();

        using var sixth = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "6" });

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.DoesNotContain("aat_", await sixth.Content.ReadAsStringAsync());
        Assert.Equal(5, (await _devices.ListAsync(CancellationToken.None)).Count);

        using var revoked = await owner.DeleteAsync($"/api/alerts/devices/{first.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var again = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "6" });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PhoneRoutes))]
    public async Task A_paired_phone_reaches_the_routes_it_needs(string method, string path, string? body)
    {
        var token = (await PairAsync()).GetProperty("token").GetString()!;
        using var phone = Phone(token);

        using var response = await Send(phone, method, path, body);

        Assert.Equal(
            method == "POST" && path.EndsWith("/events", StringComparison.Ordinal) ? HttpStatusCode.Created : HttpStatusCode.OK,
            response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(state.GetProperty("configured").GetBoolean());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("calendarWrite").ValueKind);
    }

    [Theory]
    [MemberData(nameof(OwnerOnlyRoutes))]
    public async Task A_paired_phone_is_refused_every_other_route_with_403_and_the_refusal_is_logged(
        string method, string path, string? body)
    {
        var token = (await PairAsync()).GetProperty("token").GetString()!;
        using var phone = Phone(token);

        using var response = await Send(phone, method, path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_pushover.Requests);
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
        Assert.Single(await _devices.ListAsync(CancellationToken.None));
        var refused = Assert.Single(
            _logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.AlertsDeviceRefused);
        Assert.DoesNotContain(token, refused.Everything);
    }

    [Theory]
    [MemberData(nameof(PhoneRoutes))]
    [MemberData(nameof(OwnerOnlyRoutes))]
    public async Task The_owners_cookie_still_reaches_every_route(string method, string path, string? body)
    {
        using var owner = Owner();

        using var response = await Send(owner, method, path, body);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static IEnumerable<object[]> BadTokens =>
    [
        ["aat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"],
        ["aat_short"],
        ["not-a-token-at-all"],
        ["aat_" + new string('A', 42) + "="]
    ];

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task A_wrong_or_malformed_token_is_401_and_logged_without_the_token(string token)
    {
        await PairAsync();
        using var phone = Phone(token);

        using var response = await phone.GetAsync("/api/alerts/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString());
        var rejected = Assert.Single(
            _logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.AlertsDeviceTokenRejected);
        Assert.Equal("/api/alerts/status", rejected.Fields["Route"]);
        Assert.All(_logs.Entries, entry => Assert.DoesNotContain(token, entry.Everything));
    }

    [Fact]
    public async Task No_token_and_no_cookie_is_401_with_no_bearer_challenge()
    {
        using var visitor = _app.CreateClient();

        using var response = await visitor.GetAsync("/api/alerts/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.DoesNotContain(
            _logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.AlertsDeviceTokenRejected);
    }

    [Fact]
    public async Task A_revoked_phone_is_refused_on_its_next_request()
    {
        var paired = await PairAsync();
        using var phone = Phone(paired.GetProperty("token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/alerts/status")).StatusCode);

        using var owner = Owner();
        using var revoked = await owner.DeleteAsync($"/api/alerts/devices/{paired.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/alerts/status")).StatusCode);
        Assert.Empty(await _devices.ListAsync(CancellationToken.None));
        using var twice = await owner.DeleteAsync($"/api/alerts/devices/{paired.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.NotFound, twice.StatusCode);
    }

    [Fact]
    public async Task Ten_wrong_tokens_a_minute_from_one_address_then_the_right_one_is_refused_too()
    {
        var token = (await PairAsync()).GetProperty("token").GetString()!;
        using var guesser = Phone("aat_" + new string('B', 43));
        for (var guess = 0; guess < RateLimits.AlertsDeviceTokenFailures; guess++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await guesser.GetAsync("/api/alerts/status")).StatusCode);
        }

        using var phone = Phone(token);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await phone.GetAsync("/api/alerts/status")).StatusCode);

        // The owner's browser presents no token and is not counted.
        using var owner = Owner();
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/alerts/status")).StatusCode);
    }

    [Fact]
    public async Task Last_seen_is_written_at_most_once_a_minute()
    {
        var token = (await PairAsync()).GetProperty("token").GetString()!;
        using var phone = Phone(token);

        await phone.GetAsync("/api/alerts/status");
        _clock.Now = Eight + Duration.FromSeconds(30);
        await phone.GetAsync("/api/alerts/status");
        Assert.Equal(1, _devices.Touches);
        Assert.Equal(Eight, (await _devices.ListAsync(CancellationToken.None)).Single().LastSeenAt);

        _clock.Now = Eight + Duration.FromSeconds(61);
        await phone.GetAsync("/api/alerts/status");
        Assert.Equal(2, _devices.Touches);
        using var owner = Owner();
        var listed = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/devices"))[0];
        Assert.Equal((Eight + Duration.FromSeconds(61)).ToDateTimeOffset(), listed.GetProperty("lastSeenAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Acknowledging_shows_on_the_alert_and_a_second_acknowledgement_changes_nothing()
    {
        var token = (await PairAsync()).GetProperty("token").GetString()!;
        var key = await KeyOf("Standup");
        using var phone = Phone(token);

        using var first = await phone.PostAsJsonAsync("/api/alerts/ack", new { key, via = "phone" });
        _clock.Now = Eight + Duration.FromMinutes(1);
        using var owner = Owner();
        using var second = await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        foreach (var answer in new[] { first, second })
        {
            var standup = (await answer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alerts").EnumerateArray()
                .Single(alert => alert.GetProperty("key").GetString() == key);
            Assert.True(standup.GetProperty("acknowledged").GetBoolean());
            Assert.Equal(Eight.ToDateTimeOffset(), standup.GetProperty("acknowledgedAt").GetDateTimeOffset());
            Assert.Equal("phone", standup.GetProperty("acknowledgedVia").GetString());
        }

        var review = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts")[1];
        Assert.False(review.GetProperty("acknowledged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("acknowledgedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, review.GetProperty("acknowledgedVia").ValueKind);
    }

    [Fact]
    public async Task Acknowledging_after_the_emergency_send_cancels_its_repeats_once()
    {
        _clock.Now = AlertTime;
        await Worker.TickAsync(CancellationToken.None);
        var sent = Assert.Single(_pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        var key = await KeyOf("Standup");
        using var owner = Owner();

        using var ack = await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });
        using var again = await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });

        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        var cancels = _pushover.Requests.Where(request => request.Url.AbsolutePath.Contains("/receipts/")).ToList();
        var cancel = Assert.Single(cancels);
        Assert.Equal(PushoverClient.CancelEndpoint("rcpt30lettersanddigits0000000a"), cancel.Url.ToString());
        Assert.Equal(AlertsRouteTests.AppToken, cancel.Form["token"]);
        Assert.False(cancel.Form.ContainsKey("user"));
    }

    [Fact]
    public async Task A_cancel_Pushover_refuses_is_logged_and_the_acknowledgement_stands()
    {
        _clock.Now = AlertTime;
        await Worker.TickAsync(CancellationToken.None);
        var key = await KeyOf("Standup");
        _pushover.Then(() => RecordingHandler.Text(HttpStatusCode.BadRequest, "{\"status\":0}"));
        using var owner = Owner();

        using var ack = await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });

        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        Assert.True(await _store.IsAcknowledgedAsync(key, CancellationToken.None));
        var failed = Assert.Single(_logs.Entries, entry => entry.Message.Contains("Cancelling the repeats", StringComparison.Ordinal));
        Assert.Contains("HTTP 400", failed.Message);
        Assert.DoesNotContain("rcpt30", failed.Everything);
    }

    [Fact]
    public async Task An_alert_that_dropped_off_the_list_after_it_was_sent_can_still_be_acknowledged()
    {
        _clock.Now = AlertTime;
        await Worker.TickAsync(CancellationToken.None);
        var key = await KeyOf("Standup");

        // 09:10: the standup has started and is off the list.
        _clock.Now = Instant.FromUtc(2026, 10, 28, 13, 10);
        using var owner = Owner();
        var listed = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray();
        Assert.DoesNotContain(listed, alert => alert.GetProperty("key").GetString() == key);
        using var ack = await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "phone" });

        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        Assert.True(await _store.IsAcknowledgedAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_key_is_404_and_nothing_is_stored()
    {
        using var owner = Owner();

        using var response = await owner.PostAsJsonAsync("/api/alerts/ack", new { key = "never|20261028T130000Z", via = "phone" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await _store.AcknowledgementsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("key", "{\"via\":\"phone\"}")]
    [InlineData("via", "{\"key\":\"standup@google.com|20261028T130000Z\",\"via\":\"watch\"}")]
    [InlineData("via", "{\"key\":\"standup@google.com|20261028T130000Z\"}")]
    public async Task An_acknowledgement_the_page_or_phone_would_not_send_is_refused_by_field(string field, string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/ack", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Empty(await _store.AcknowledgementsAsync(CancellationToken.None));
    }

    public void Dispose() => _app.Dispose();
}
