using System.Net;
using System.Net.Http.Json;
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
/// Acknowledging in the Pushover app acknowledges everywhere. Every alarm
/// names the callback. Pushover posts the receipt to it with no session.
/// The server believes the post only for a receipt it sent and only once
/// Pushover's receipts API says it was acknowledged, records "pushover",
/// pushes the phones, and does nothing more the second time.
/// </summary>
public sealed class PushoverCallbackRouteTests : IDisposable
{
    /// <summary>08:46 New York time: the standup's reminder at 08:45 is due.</summary>
    private static readonly Instant Due = Instant.FromUtc(2026, 10, 28, 12, 46);

    private const string Receipt = "rcptcallback00000000000000001";
    private const string Origin = "https://abera.tech";

    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 10, 28, 12, 0));
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FakeGoogleCalendar _google = new();
    private readonly CapturedLogs _logs = new();

    /// <summary>What Pushover's receipts API answers for <see cref="Receipt"/>: acknowledged 1, 0, or an outage.</summary>
    private Func<HttpResponseMessage> _poll = () => Poll(1);

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, $"{{\"status\":1,\"request\":\"r\",\"receipt\":\"{Receipt}\"}}", "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(
            Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]),
            Event("review@google.com", "Review", "20261028T140000", "20261028T150000")),
        "text/calendar"));

    private readonly TestApp _app;

    public PushoverCallbackRouteTests() : this(Origin)
    {
    }

    private PushoverCallbackRouteTests(string? origin, Dictionary<string, string?>? extra = null)
    {
        var settings = AlertsRouteTests.Settings();
        foreach (var (key, value) in extra ?? []) settings[key] = value;
        _pushover.Route = seen => seen.Method == HttpMethod.Get && seen.Url.AbsolutePath == $"/1/receipts/{Receipt}.json" ? _poll() : null;
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
                PushoverRetrySeconds = 0,
                PublicOrigin = origin
            });
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
            services.AddSingleton<IAlertCalendarGrant>(_google);
            services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
        });
        Worker.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose() => _app.Dispose();

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private static HttpResponseMessage Poll(int acknowledged) => RecordingHandler.Text(
        HttpStatusCode.OK,
        $"{{\"status\":1,\"acknowledged\":{acknowledged},\"acknowledged_at\":1761655560,\"acknowledged_by\":\"u\",\"acknowledged_by_device\":\"iphone\",\"last_delivered_at\":1761655500,\"expired\":0,\"expires_at\":1761666300,\"called_back\":0,\"called_back_at\":0,\"request\":\"r\"}}",
        "application/json");

    private IReadOnlyList<RecordingHandler.Seen> Messages =>
        [.. _pushover.Requests.Where(seen => seen.Url.AbsolutePath == "/1/messages.json")];

    private IReadOnlyList<RecordingHandler.Seen> Polls =>
        [.. _pushover.Requests.Where(seen => seen.Method == HttpMethod.Get && seen.Url.AbsolutePath.StartsWith("/1/receipts/", StringComparison.Ordinal))];

    private string StandupKey => AlertPlanner.KeyFor("standup@google.com", Instant.FromUtc(2026, 10, 28, 13, 0));

    /// <summary>The standup's alarm, sent to Pushover with a receipt.</summary>
    private async Task SendStandupAsync()
    {
        _clock.Now = Due;
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Messages);
    }

    /// <summary>Pushover's post: form-encoded, no session, no cookie.</summary>
    private async Task<HttpResponseMessage> CallbackAsync(params (string Name, string Value)[] fields)
    {
        using var anonymous = _app.CreateClient();
        return await anonymous.PostAsync(PushoverCallback.Path, new FormUrlEncodedContent(
            fields.Length > 0
                ? fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))
                :
                [
                    new("receipt", Receipt),
                    new("acknowledged", "1"),
                    new("acknowledged_at", "1761655560"),
                    new("acknowledged_by", "user-key"),
                    new("acknowledged_by_device", "iphone")
                ]));
    }

    // ------------------------------------------------------------------ the send

    [Fact]
    public async Task Every_alarm_names_the_callback_on_the_public_origin()
    {
        await SendStandupAsync();

        Assert.Equal("https://abera.tech/api/alerts/pushover/acknowledged", Messages.Single().Form["callback"]);
    }

    [Fact]
    public async Task A_routine_ring_names_the_callback_too()
    {
        using var owner = Owner();
        await owner.PostAsJsonAsync("/api/alerts/routines", new { label = "Meds", hour = 12, minute = 10, days = new[] { 3 } });
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 10, 5);

        await Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(Messages);
        Assert.Equal("Meds", sent.Form["title"]);
        Assert.Equal("https://abera.tech/api/alerts/pushover/acknowledged", sent.Form["callback"]);
    }

    [Fact]
    public async Task A_notification_names_no_callback()
    {
        using var owner = Owner();
        var key = AlertPlanner.KeyFor("review@google.com", Instant.FromUtc(2026, 10, 28, 18, 0));
        await owner.PutAsJsonAsync("/api/alerts/event-type", new { key, type = "notification" });
        _clock.Now = Instant.FromUtc(2026, 10, 28, 17, 51);

        await Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(Messages, seen => seen.Form["title"] == "Review");
        Assert.Equal("0", sent.Form["priority"]);
        Assert.False(sent.Form.ContainsKey("callback"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abera.tech")]
    [InlineData("ftp://abera.tech")]
    [InlineData("https://abera.tech/alerts")]
    [InlineData("https://abera.tech/?x=1")]
    [InlineData("https://user:pass@abera.tech")]
    public void An_empty_or_malformed_origin_sends_no_callback(string? origin)
    {
        Assert.Null(new AlertsOptions { PublicOrigin = origin }.PushoverCallbackUrl());
    }

    [Theory]
    [InlineData("https://abera.tech", "https://abera.tech/api/alerts/pushover/acknowledged")]
    [InlineData("https://abera.tech/", "https://abera.tech/api/alerts/pushover/acknowledged")]
    [InlineData(" https://ABERA.tech ", "https://abera.tech/api/alerts/pushover/acknowledged")]
    [InlineData("http://localhost:8080", "http://localhost:8080/api/alerts/pushover/acknowledged")]
    public void An_origin_gives_the_callback_on_it(string origin, string expected)
    {
        Assert.Equal(expected, new AlertsOptions { PublicOrigin = origin }.PushoverCallbackUrl());
    }

    [Fact]
    public async Task With_no_origin_an_alarm_names_no_callback()
    {
        using var test = new PushoverCallbackRouteTests(origin: null);

        await test.SendStandupAsync();

        Assert.False(test.Messages.Single().Form.ContainsKey("callback"));
    }

    // ------------------------------------------------------------------ the callback

    [Fact]
    public async Task An_acknowledgement_Pushover_confirms_is_recorded_as_pushover_and_pushes_the_phones()
    {
        await SendStandupAsync();
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await CallbackAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"acknowledged\":true}", await response.Content.ReadAsStringAsync());
        var poll = Assert.Single(Polls);
        Assert.Equal($"https://api.pushover.net/1/receipts/{Receipt}.json?token={AlertsRouteTests.AppToken}", poll.Url.ToString());
        var acknowledgement = (await _store.AcknowledgementsAsync(CancellationToken.None))[StandupKey];
        Assert.Equal("pushover", acknowledgement.Via);
        Assert.Equal(Due, acknowledgement.At);
        Assert.Equal(version + 1, await _devices.PlanVersionAsync(CancellationToken.None));

        // The page and the phone read it from the status.
        using var owner = Owner();
        var standup = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray()
            .Single(alert => alert.GetProperty("key").GetString() == StandupKey);
        Assert.True(standup.GetProperty("acknowledged").GetBoolean());
        Assert.Equal("pushover", standup.GetProperty("acknowledgedVia").GetString());

        // Pushover stopped its own repeats when the owner pressed Acknowledge: nothing to cancel.
        Assert.DoesNotContain(_pushover.Requests, seen => seen.Url.AbsolutePath.EndsWith("/cancel.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_second_callback_does_nothing_more()
    {
        await SendStandupAsync();
        await CallbackAsync();
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var again = await CallbackAsync();

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(Polls);
        Assert.Equal(version, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_callback_after_the_phone_acknowledged_keeps_the_phones_acknowledgement()
    {
        await SendStandupAsync();
        using var owner = Owner();
        await owner.PostAsJsonAsync("/api/alerts/ack", new { key = StandupKey, via = "browser" });

        using var response = await CallbackAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Polls);
        Assert.Equal("browser", (await _store.AcknowledgementsAsync(CancellationToken.None))[StandupKey].Via);
    }

    [Fact]
    public async Task A_routine_ring_acknowledged_in_Pushover_shows_as_pushover_in_routineRings()
    {
        using var owner = Owner();
        await owner.PostAsJsonAsync("/api/alerts/routines", new { label = "Meds", hour = 12, minute = 10, days = new[] { 3 } });
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 10, 5);
        await Worker.TickAsync(CancellationToken.None);

        using var response = await CallbackAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ring = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("routineRings").EnumerateArray());
        Assert.True(ring.GetProperty("acknowledged").GetBoolean());
        Assert.Equal("pushover", ring.GetProperty("acknowledgedVia").GetString());
    }

    [Fact]
    public async Task A_receipt_Pushover_says_is_not_acknowledged_is_403_and_records_nothing()
    {
        await SendStandupAsync();
        _poll = () => Poll(0);

        using var response = await CallbackAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await _store.AcknowledgementsAsync(CancellationToken.None));
        Assert.Contains(
            _logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.PushoverCallbackRefused);
        Assert.All(_logs.Entries, entry => Assert.DoesNotContain(Receipt, entry.Everything));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"status\":0}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task When_Pushover_cannot_confirm_the_answer_is_503_so_it_posts_again(HttpStatusCode status, string body)
    {
        await SendStandupAsync();
        _poll = () => RecordingHandler.Text(status, body, "application/json");

        using var response = await CallbackAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await _store.AcknowledgementsAsync(CancellationToken.None));

        _poll = () => Poll(1);
        using var retried = await CallbackAsync();
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task A_receipt_this_server_never_sent_is_404_and_Pushover_is_not_asked()
    {
        await SendStandupAsync();

        using var response = await CallbackAsync(("receipt", "rcptnotours000000000000000001"), ("acknowledged", "1"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Polls);
        Assert.Empty(await _store.AcknowledgementsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Bodies_that_are_not_a_receipt_are_400()
    {
        await SendStandupAsync();
        using var anonymous = _app.CreateClient();

        using var json = await anonymous.PostAsJsonAsync(PushoverCallback.Path, new { receipt = Receipt });
        using var none = await CallbackAsync(("acknowledged", "1"));
        using var bad = await CallbackAsync(("receipt", "../../1/messages"));
        using var twice = await CallbackAsync(("receipt", Receipt), ("receipt", Receipt));

        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Empty(Polls);
    }

    [Fact]
    public async Task Ten_wrong_receipts_a_minute_and_the_address_is_refused_unheard()
    {
        await SendStandupAsync();
        for (var i = 0; i < 10; i++)
        {
            using var wrong = await CallbackAsync(("receipt", $"rcptguess{i:D20}"));
            Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        }

        using var right = await CallbackAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
        Assert.Empty(Polls);
    }

    [Fact]
    public async Task The_callback_has_its_own_rate_limit()
    {
        using var test = new PushoverCallbackRouteTests(Origin, new() { ["RateLimits:PushoverCallbacksPerMinute"] = "3" });
        await test.SendStandupAsync();

        for (var i = 0; i < 3; i++)
        {
            using var allowed = await test.CallbackAsync();
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var refused = await test.CallbackAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public void The_receipt_poll_carries_the_app_token_and_is_kept_out_of_request_traces()
    {
        Assert.True(PushoverClient.IsReceiptsRequest(new Uri($"https://api.pushover.net/1/receipts/{Receipt}.json?token=t")));
        Assert.True(PushoverClient.IsReceiptsRequest(new Uri($"https://api.pushover.net/1/receipts/{Receipt}/cancel.json")));
        Assert.False(PushoverClient.IsReceiptsRequest(new Uri("https://api.pushover.net/1/messages.json")));
        Assert.False(PushoverClient.IsReceiptsRequest(new Uri("https://example.test/1/receipts/x.json")));
        Assert.False(PushoverClient.IsReceiptsRequest(null));
    }
}
