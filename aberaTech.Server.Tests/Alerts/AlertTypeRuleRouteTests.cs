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
using NodaTime;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// Rules for what an event sends, from every entry point: PUT rules from the
/// page's cookie and a paired phone's token, the status that lists them and
/// the type each event takes, the phones' push, and the send path.
/// </summary>
public sealed class AlertTypeRuleRouteTests : IDisposable
{
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private const string DrillKey = "drill@google.com|20261028T140000Z";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly ApnsHandler _apple = new();
    private readonly FakeGoogleCalendar _google = new();

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\",\"receipt\":\"rcpt\"}", "application/json"));

    // Standup is marked #critical. Drill is a work invitation. Lunch has a
    // place. Review has none of these.
    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(
            Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]),
            Event("drill@google.com", "Fire drill", "20261028T100000", "20261028T103000",
                extra: ["ORGANIZER;CN=Operations:mailto:ops@army.mil"], alarms: [Popup("-PT10M")]),
            Event("lunch@google.com", "Lunch", "20261028T120000", "20261028T130000", "Cafe Paris"),
            Event("review@google.com", "Review", "20261028T140000", "20261028T150000")),
        "text/calendar"));

    private readonly TestApp _app;

    public AlertTypeRuleRouteTests()
    {
        var settings = AlertsRouteTests.Settings();
        // Enough presses for the cases below. The limit itself is checked on its own.
        settings["RateLimits:AlertsActionsPerMinute"] = "1000";
        var pem = _key.ExportPkcs8PrivateKeyPem();
        settings["Alerts:ApnsKeyP8"] = pem;
        settings["Alerts:ApnsKeyId"] = ApnsFixture.KeyId;
        settings["Alerts:ApnsTeamId"] = ApnsFixture.TeamId;
        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton<IAlertStore>(_store);
            services.AddSingleton<IAlertDeviceStore>(_devices);
            services.AddSingleton(new AlertsOptions
            {
                CalendarIcsUrl = AlertsRouteTests.FeedUrl,
                PushoverAppToken = AlertsRouteTests.AppToken,
                PushoverUserKey = AlertsRouteTests.UserKey,
                PushoverRetrySeconds = 0,
                ApnsKeyP8 = pem,
                ApnsKeyId = ApnsFixture.KeyId,
                ApnsTeamId = ApnsFixture.TeamId
            });
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
            services.AddHttpClient<ApnsClient>().ConfigurePrimaryHttpMessageHandler(() => _apple);
            services.AddSingleton<IAlertCalendarGrant>(_google);
            services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
        });
        Worker.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _app.Dispose();
        _key.Dispose();
    }

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private AlertPushWorker Pushes => _app.Factory.Services.GetRequiredService<AlertPushWorker>();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private HttpClient Phone(string token)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string body) =>
        client.PutAsync("/api/alerts/rules", new StringContent(body, Encoding.UTF8, "application/json"));

    /// <summary>A phone paired on the page and registered for pushes. Answers its token.</summary>
    private async Task<string> RegisteredPhoneAsync()
    {
        using var owner = Owner();
        using var paired = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Neb's iPhone" });
        var token = (await paired.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        using var phone = Phone(token);
        using var push = await phone.PutAsJsonAsync(
            "/api/alerts/devices/me/push", new { apnsToken = ApnsFixture.Token, environment = "production" });
        Assert.Equal(HttpStatusCode.NoContent, push.StatusCode);
        return token;
    }

    private static async Task<JsonElement> StateOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static (string Type, string From) TypeOf(JsonElement state, string title)
    {
        var alert = state.GetProperty("alerts").EnumerateArray()
            .Single(item => item.GetProperty("title").GetString() == title);
        return (alert.GetProperty("type").GetString()!, alert.GetProperty("typeFrom").GetString()!);
    }

    private const string WorkAlarmsAndPlacesNotify = """
        {"rules":[
          {"field":"organizer","text":"army.mil","type":"alarm"},
          {"field":"location","text":"","type":"notification"},
          {"field":"title","text":"standup","type":"none"}
        ]}
        """;

    // ------------------------------------------------------------------ what a rule decides

    [Fact]
    public async Task The_first_matching_rule_decides_and_a_critical_mark_still_wins()
    {
        using var owner = Owner();

        using var response = await Put(owner, WorkAlarmsAndPlacesNotify);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        Assert.Equal(("alarm", "rule"), TypeOf(state, "Fire drill"));
        Assert.Equal(("notification", "rule"), TypeOf(state, "Lunch"));
        Assert.Equal(("none", "default"), TypeOf(state, "Review"));
        // The title rule says none, and the #critical mark comes first.
        Assert.Equal(("alarm", "critical"), TypeOf(state, "Standup"));
        var rules = state.GetProperty("rules").EnumerateArray().ToList();
        Assert.Equal(3, rules.Count);
        Assert.Equal(("organizer", "army.mil", "alarm"),
            (rules[0].GetProperty("field").GetString(), rules[0].GetProperty("text").GetString(), rules[0].GetProperty("type").GetString()));
    }

    [Fact]
    public async Task A_choice_for_one_event_beats_every_rule()
    {
        using var owner = Owner();
        using var saved = await Put(owner, WorkAlarmsAndPlacesNotify);
        using var chosen = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = DrillKey, type = "notification" });

        var state = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal(("notification", "set"), TypeOf(state, "Fire drill"));
    }

    [Fact]
    public async Task An_empty_list_removes_every_rule()
    {
        using var owner = Owner();
        using var saved = await Put(owner, WorkAlarmsAndPlacesNotify);

        using var cleared = await Put(owner, "{\"rules\":[]}");

        var state = await StateOf(cleared);
        Assert.Empty(state.GetProperty("rules").EnumerateArray());
        Assert.Equal(("none", "default"), TypeOf(state, "Fire drill"));
        Assert.Empty(await _store.TypeRulesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_alarm_a_rule_makes_is_sent_as_an_alarm_at_its_time()
    {
        using var owner = Owner();
        using var saved = await Put(owner, WorkAlarmsAndPlacesNotify);

        // 09:50 New York: the drill's 10-minute reminder.
        _clock.Now = Instant.FromUtc(2026, 10, 28, 13, 50);
        await Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(_pushover.Requests, request => request.Form.GetValueOrDefault("title") == "Fire drill");
        Assert.Equal("2", sent.Form["priority"]);
    }

    // ------------------------------------------------------------------ who

    [Fact]
    public async Task A_paired_phone_saves_the_rules_and_the_phones_are_pushed_once()
    {
        var token = await RegisteredPhoneAsync();
        var before = await _devices.PlanVersionAsync(CancellationToken.None);
        using var phone = Phone(token);

        using var response = await Put(phone, WorkAlarmsAndPlacesNotify);
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await _store.TypeRulesAsync(CancellationToken.None)).Count);
        Assert.Single(_apple.Requests);
        Assert.Equal(before + 1, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_visitor_is_told_to_sign_in_and_nothing_is_saved()
    {
        using var visitor = _app.CreateClient();

        using var response = await Put(visitor, WorkAlarmsAndPlacesNotify);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await _store.TypeRulesAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ bounds

    public static IEnumerable<object[]> BadRules =>
    [
        ["rules", "{}"],
        ["rules[0].field", "{\"rules\":[{\"field\":\"calendar\",\"text\":\"work\",\"type\":\"alarm\"}]}"],
        ["rules[0].text", "{\"rules\":[{\"field\":\"title\",\"text\":\"  \",\"type\":\"alarm\"}]}"],
        ["rules[0].text", "{\"rules\":[{\"field\":\"location\",\"text\":\"" + new string('x', 101) + "\",\"type\":\"alarm\"}]}"],
        ["rules[1].type", "{\"rules\":[{\"field\":\"title\",\"text\":\"a\",\"type\":\"alarm\"},{\"field\":\"title\",\"text\":\"b\",\"type\":\"loud\"}]}"],
        ["rules", "{\"rules\":[" + string.Join(',', Enumerable.Repeat("{\"field\":\"title\",\"text\":\"a\",\"type\":\"none\"}", 51)) + "]}"]
    ];

    [Theory]
    [MemberData(nameof(BadRules))]
    public async Task A_list_outside_the_bounds_is_refused_by_field_and_nothing_is_saved(string field, string body)
    {
        using var owner = Owner();

        using var response = await Put(owner, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Empty(await _store.TypeRulesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Every_bound_itself_is_accepted_and_text_is_trimmed()
    {
        using var owner = Owner();
        var fifty = string.Join(',', Enumerable.Repeat("{\"field\":\"title\",\"text\":\" a \",\"type\":\"none\"}", 49));
        var body = "{\"rules\":[" + fifty + ",{\"field\":\"location\",\"text\":\"" + new string('x', 100) + "\",\"type\":\"alarm\"}]}";

        using var response = await Put(owner, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rules = await _store.TypeRulesAsync(CancellationToken.None);
        Assert.Equal(50, rules.Count);
        Assert.Equal("a", rules[0].Text);
    }
}
