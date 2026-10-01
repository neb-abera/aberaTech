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
/// Phone pushes from every entry point: a phone registers its token with
/// its own device token, every change a phone must hear about bumps the
/// plan version, and the sender pushes each phone at most once a minute.
/// The standup is marked #critical, so it is an alarm.
/// </summary>
public sealed class AlertPushRouteTests : IDisposable
{
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly CapturedLogs _logs = new();
    private readonly ApnsHandler _apple = new();
    private readonly RecordingDelay _delay = new();
    private readonly FakeGoogleCalendar _google = new();

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));

    private string _feed = Ics(
        Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]),
        Event("review@google.com", "Review", "20261028T140000", "20261028T150000"));

    private readonly RecordingHandler _calendar;
    private readonly TestApp _app;

    public AlertPushRouteTests() : this(apns: true)
    {
    }

    private AlertPushRouteTests(bool apns, string? leaveOut = null)
    {
        _calendar = new RecordingHandler(() => RecordingHandler.Text(HttpStatusCode.OK, _feed, "text/calendar"));
        _app = App(apns, leaveOut);
        _google.Seed("standup@google.com", "#critical");
        _google.Seed("review@google.com", "");
        Worker.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private TestApp App(bool apns, string? leaveOut)
    {
        var settings = AlertsRouteTests.Settings();
        var options = new Dictionary<string, string?>
        {
            ["Alerts:ApnsKeyP8"] = _key.ExportPkcs8PrivateKeyPem(),
            ["Alerts:ApnsKeyId"] = ApnsFixture.KeyId,
            ["Alerts:ApnsTeamId"] = ApnsFixture.TeamId
        };
        if (leaveOut is not null) options.Remove(leaveOut);
        if (apns)
        {
            foreach (var (name, value) in options) settings[name] = value;
        }

        return new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton<IAlertStore>(_store);
            services.AddSingleton<IAlertDeviceStore>(_devices);
            services.AddSingleton<ILoggerProvider>(_logs);
            services.AddSingleton<ApnsDelay>(_delay);
            services.AddSingleton(new AlertsOptions
            {
                CalendarIcsUrl = AlertsRouteTests.FeedUrl,
                PushoverAppToken = AlertsRouteTests.AppToken,
                PushoverUserKey = AlertsRouteTests.UserKey,
                PushoverRetrySeconds = 0,
                ApnsKeyP8 = apns ? options.GetValueOrDefault("Alerts:ApnsKeyP8") : null,
                ApnsKeyId = apns ? options.GetValueOrDefault("Alerts:ApnsKeyId") : null,
                ApnsTeamId = apns ? options.GetValueOrDefault("Alerts:ApnsTeamId") : null
            });
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
            services.AddHttpClient<ApnsClient>().ConfigurePrimaryHttpMessageHandler(() => _apple);
            services.AddSingleton<IAlertCalendarGrant>(_google);
            services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
        });
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

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    /// <summary>Pairs a phone on the page and answers its id and token.</summary>
    private async Task<(Guid Id, string Token)> PairAsync(string name = "Neb's iPhone")
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync("/api/alerts/devices", new { name });
        var paired = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (paired.GetProperty("id").GetGuid(), paired.GetProperty("token").GetString()!);
    }

    /// <summary>A paired phone that has registered for pushes.</summary>
    private async Task<(Guid Id, string Token)> RegisteredAsync(string apnsToken, string environment = "production")
    {
        var phone = await PairAsync();
        using var client = Phone(phone.Token);
        using var response = await client.PutAsJsonAsync("/api/alerts/devices/me/push", new { apnsToken, environment });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return phone;
    }

    private async Task<string> KeyOf(string title)
    {
        using var owner = Owner();
        return (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray()
            .Single(alert => alert.GetProperty("title").GetString() == title).GetProperty("key").GetString()!;
    }

    private static long VersionIn(ApnsHandler.Seen push) =>
        JsonDocument.Parse(push.Body).RootElement.GetProperty("v").GetInt64();

    // ------------------------------------------------------------------ registration

    [Fact]
    public async Task A_phone_registers_its_push_token_with_its_own_token_and_it_is_stored()
    {
        var (id, _) = await RegisteredAsync(ApnsFixture.Token, "sandbox");

        var target = Assert.Single(await _devices.PushTargetsAsync(CancellationToken.None));
        Assert.Equal(id, target.DeviceId);
        Assert.Equal(ApnsFixture.Token, target.Token);
        Assert.Equal("sandbox", target.Environment);
    }

    [Theory]
    [InlineData("PUT", "{\"apnsToken\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"environment\":\"production\"}")]
    [InlineData("DELETE", null)]
    public async Task The_owners_cookie_is_refused_the_push_routes_with_403(string method, string? body)
    {
        await PairAsync();
        using var owner = Owner();

        using var response = await Send(owner, method, "/api/alerts/devices/me/push", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await _devices.PushTargetsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task No_token_and_no_cookie_is_401_on_the_push_routes(string method)
    {
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, "/api/alerts/devices/me/push", method == "PUT" ? "{}" : null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static IEnumerable<object?[]> BadRegistrations =>
    [
        // Uppercase hex.
        ["apnsToken", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", "production"],
        // Not hex.
        ["apnsToken", "0123456789abcdeg0123456789abcdef0123456789abcdef0123456789abcdef", "production"],
        // 63 characters.
        ["apnsToken", new string('a', 63), "production"],
        // 201 characters.
        ["apnsToken", new string('a', 201), "production"],
        ["apnsToken", null, "production"],
        ["environment", new string('a', 64), "development"],
        ["environment", new string('a', 64), "Production"],
        ["environment", new string('a', 64), null]
    ];

    [Theory]
    [MemberData(nameof(BadRegistrations))]
    public async Task A_registration_the_app_would_not_send_is_400_naming_the_field(
        string field, string? apnsToken, string? environment)
    {
        var phone = await PairAsync();
        using var client = Phone(phone.Token);

        using var response = await client.PutAsJsonAsync("/api/alerts/devices/me/push", new { apnsToken, environment });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Empty(await _devices.PushTargetsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(200)]
    public async Task Tokens_of_64_and_200_characters_are_taken(int length)
    {
        var phone = await PairAsync();
        using var client = Phone(phone.Token);

        using var response = await client.PutAsJsonAsync(
            "/api/alerts/devices/me/push", new { apnsToken = new string('f', length), environment = "production" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_new_token_replaces_the_old_one()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token, "sandbox");
        using var client = Phone(token);

        using var response = await client.PutAsJsonAsync(
            "/api/alerts/devices/me/push", new { apnsToken = ApnsFixture.OtherToken, environment = "production" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var target = Assert.Single(await _devices.PushTargetsAsync(CancellationToken.None));
        Assert.Equal(ApnsFixture.OtherToken, target.Token);
        Assert.Equal("production", target.Environment);
    }

    [Fact]
    public async Task Delete_clears_the_token_and_the_phone_stays_paired()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token);
        using var client = Phone(token);

        using var response = await client.DeleteAsync("/api/alerts/devices/me/push");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await _devices.PushTargetsAsync(CancellationToken.None));
        Assert.Single(await _devices.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Revoking_a_phone_on_the_page_drops_its_push_token()
    {
        var (id, _) = await RegisteredAsync(ApnsFixture.Token);
        using var owner = Owner();

        using var response = await owner.DeleteAsync($"/api/alerts/devices/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await _devices.PushTargetsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_phones_list_says_whether_each_has_push_and_never_carries_the_token()
    {
        await RegisteredAsync(ApnsFixture.Token);
        await PairAsync("iPad");
        using var owner = Owner();

        var text = await owner.GetStringAsync("/api/alerts/devices");

        Assert.DoesNotContain(ApnsFixture.Token, text);
        Assert.DoesNotContain("apns", text, StringComparison.OrdinalIgnoreCase);
        var listed = JsonDocument.Parse(text).RootElement.EnumerateArray().ToList();
        Assert.Equal(["id", "name", "createdAt", "lastSeenAt", "push"], listed[0].EnumerateObject().Select(field => field.Name));
        Assert.True(listed[0].GetProperty("push").GetBoolean());
        Assert.False(listed[1].GetProperty("push").GetBoolean());
        Assert.All(_logs.Entries, entry => Assert.DoesNotContain(ApnsFixture.Token, entry.Everything));
    }

    // ------------------------------------------------------------------ what pushes

    /// <summary>Each change a phone must hear about, made the way the page or the phone makes it.</summary>
    public static IEnumerable<object?[]> Triggers =>
    [
        ["mute from the phone", "phone", "POST", "/api/alerts/mute", "{\"until\":\"hour\"}"],
        ["unmute from the page", "owner", "POST", "/api/alerts/unmute", "{}"],
        ["skip from the page", "owner", "POST", "/api/alerts/skip", "{\"key\":\"standup@google.com|20261028T130000Z\"}"],
        ["unskip from the phone", "phone", "POST", "/api/alerts/unskip", "{\"key\":\"standup@google.com|20261028T130000Z\"}"],
        ["ack from the phone", "phone", "POST", "/api/alerts/ack", "{\"key\":\"standup@google.com|20261028T130000Z\",\"via\":\"phone\"}"],
        ["ack from a browser", "owner", "POST", "/api/alerts/ack", "{\"key\":\"standup@google.com|20261028T130000Z\",\"via\":\"browser\"}"],
        ["type from the page", "owner", "PUT", "/api/alerts/event-type", "{\"key\":\"review@google.com|20261028T180000Z\",\"type\":\"alarm\"}"],
        ["type from the phone", "phone", "PUT", "/api/alerts/event-type", "{\"key\":\"review@google.com|20261028T180000Z\",\"type\":\"notification\"}"],
        ["new event from the phone", "phone", "POST", "/api/alerts/events",
            "{\"title\":\"Dentist\",\"startsAt\":\"2026-10-28T11:00:00-04:00\",\"durationMinutes\":30,\"type\":\"alarm\",\"leadMinutes\":20}"],
        ["new event from the page", "owner", "POST", "/api/alerts/events",
            "{\"title\":\"Dentist\",\"startsAt\":\"2026-10-28T11:00:00-04:00\",\"durationMinutes\":30,\"type\":\"none\"}"],
        ["routine added on the page", "owner", "POST", "/api/alerts/routines", "{\"label\":\"Wake up\",\"hour\":6,\"minute\":30,\"days\":[1,2,3,4,5]}"],
        ["routine added from the phone", "phone", "POST", "/api/alerts/routines", "{\"hour\":6,\"minute\":30,\"days\":[]}"],
        ["routine changed on the page", "owner", "PUT", $"/api/alerts/routines/{SeededRoutine}",
            "{\"label\":\"Gym\",\"hour\":5,\"minute\":0,\"days\":[1,3,5],\"enabled\":true,\"snoozeMinutes\":9}"],
        ["ring-once routine turned off by the phone", "phone", "PUT", $"/api/alerts/routines/{SeededRoutine}",
            "{\"label\":\"Nap\",\"hour\":14,\"minute\":0,\"days\":[],\"enabled\":false,\"snoozeMinutes\":9}"],
        ["routine deleted on the page", "owner", "DELETE", $"/api/alerts/routines/{SeededRoutine}", null],
        ["routine deleted from the phone", "phone", "DELETE", $"/api/alerts/routines/{SeededRoutine}", null],
        ["alarm sound changed from the phone", "phone", "PUT", "/api/alerts/phone-settings", "{\"sound\":\"chime\",\"snoozeMinutes\":9}"],
        ["snooze changed from the page", "owner", "PUT", "/api/alerts/phone-settings", "{\"sound\":\"default\",\"snoozeMinutes\":15}"],
        ["alarm sound changed in the settings form", "owner", "PUT", "/api/alerts/settings",
            JsonSerializer.Serialize(AlertsRouteTests.Form(("phoneSound", "siren"), ("phoneSnoozeMinutes", 5)))]
    ];

    /// <summary>A routine alarm the change triggers above find stored.</summary>
    private static readonly Guid SeededRoutine = Guid.Parse("7c1e2d3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f");

    [Theory]
    [MemberData(nameof(Triggers))]
    public async Task Each_change_pushes_every_phone_with_a_token_the_caller_included(
        string change, string caller, string method, string path, string? body)
    {
        var first = await RegisteredAsync(ApnsFixture.Token);
        var second = await PairAsync("iPad");
        using (var ipad = Phone(second.Token))
        {
            await ipad.PutAsJsonAsync(
                "/api/alerts/devices/me/push", new { apnsToken = ApnsFixture.OtherToken, environment = "sandbox" });
        }

        await PairAsync("No push");
        await _store.AddRoutineAsync(
            new AlertRoutine(SeededRoutine, "Nap", 14, 0, [], true, 9, Eight), CancellationToken.None);
        var before = await _devices.PlanVersionAsync(CancellationToken.None);

        using var client = caller == "phone" ? Phone(first.Token) : Owner();
        using var response = await Send(client, method, path, body);
        Assert.True(response.IsSuccessStatusCode, $"{change}: {response.StatusCode}");
        await Pushes.PassAsync(CancellationToken.None);

        var pushes = _apple.Requests.ToList();
        Assert.Equal(2, pushes.Count);
        Assert.Contains(pushes, push => push.Url.AbsolutePath.EndsWith(ApnsFixture.Token, StringComparison.Ordinal));
        Assert.Contains(pushes, push => push.Url.AbsolutePath.EndsWith(ApnsFixture.OtherToken, StringComparison.Ordinal));
        Assert.All(pushes, push => Assert.Equal(before + 1, VersionIn(push)));
    }

    [Fact]
    public async Task A_second_acknowledgement_changes_nothing_and_pushes_nothing()
    {
        await RegisteredAsync(ApnsFixture.Token);
        var key = await KeyOf("Standup");
        using var owner = Owner();
        await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });
        var after = await _devices.PlanVersionAsync(CancellationToken.None);

        await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });

        Assert.Equal(after, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_calendar_read_whose_alarms_changed_pushes_and_an_unchanged_one_does_not()
    {
        await RegisteredAsync(ApnsFixture.Token);

        await Worker.ReadNowAsync(CancellationToken.None);
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Empty(_apple.Requests);

        // The standup moves ten minutes: its alarm's time and start change.
        _feed = Ics(
            Event("standup@google.com", "Standup #critical", "20261028T091000", "20261028T094000", "Room 1", alarms: [Popup("-PT15M")]),
            Event("review@google.com", "Review", "20261028T140000", "20261028T150000"));
        await Worker.ReadNowAsync(CancellationToken.None);
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Single(_apple.Requests);
    }

    [Theory]
    [InlineData("added")]
    [InlineData("renamed")]
    [InlineData("removed")]
    public async Task A_calendar_read_that_adds_renames_or_removes_an_alarm_pushes(string change)
    {
        await RegisteredAsync(ApnsFixture.Token);
        var standup = change == "renamed"
            ? Event("standup@google.com", "Standup moved #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")])
            : Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]);
        var review = Event("review@google.com", "Review", "20261028T140000", "20261028T150000");
        _feed = change switch
        {
            "added" => Ics(standup, review, Event("lunch@google.com", "Lunch #critical", "20261028T120000", "20261028T130000")),
            "removed" => Ics(review),
            _ => Ics(standup, review)
        };

        await Worker.ReadNowAsync(CancellationToken.None);
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Single(_apple.Requests);
    }

    [Fact]
    public async Task A_calendar_change_to_an_event_that_is_not_an_alarm_does_not_push()
    {
        await RegisteredAsync(ApnsFixture.Token);

        _feed = Ics(
            Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]),
            Event("review@google.com", "Review renamed", "20261028T150000", "20261028T160000"));
        await Worker.ReadNowAsync(CancellationToken.None);
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Empty(_apple.Requests);
    }

    [Fact]
    public async Task Setting_a_type_on_the_page_pushes_once_and_the_next_calendar_read_does_not_push_again()
    {
        await RegisteredAsync(ApnsFixture.Token);
        using var owner = Owner();
        await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = await KeyOf("Review"), type = "alarm" });
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Single(_apple.Requests);

        _clock.Now = Eight + Duration.FromMinutes(5);
        await Worker.ReadNowAsync(CancellationToken.None);
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Single(_apple.Requests);
    }

    // ------------------------------------------------------------------ coalescing

    [Fact]
    public async Task Two_changes_inside_a_minute_send_one_push_then_the_latest_after_the_window()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token);
        using var phone = Phone(token);

        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        await Pushes.PassAsync(CancellationToken.None);
        var first = Assert.Single(_apple.Requests);

        _clock.Now = Eight + Duration.FromSeconds(10);
        await phone.PostAsJsonAsync("/api/alerts/unmute", new { });
        _clock.Now = Eight + Duration.FromSeconds(20);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        var next = await Pushes.PassAsync(CancellationToken.None);
        Assert.Single(_apple.Requests);
        Assert.Equal(Eight + Duration.FromSeconds(60), next);

        _clock.Now = Eight + Duration.FromSeconds(60);
        await Pushes.PassAsync(CancellationToken.None);

        var pushes = _apple.Requests.ToList();
        Assert.Equal(2, pushes.Count);
        Assert.Equal(VersionIn(first) + 2, VersionIn(pushes[1]));
        Assert.Equal(await _devices.PlanVersionAsync(CancellationToken.None), VersionIn(pushes[1]));

        // Nothing changed since: nothing more goes.
        _clock.Now = Eight + Duration.FromMinutes(5);
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Equal(2, _apple.Requests.Count);
    }

    [Fact]
    public async Task Registering_does_not_push_and_the_first_change_after_does()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token);

        await Pushes.PassAsync(CancellationToken.None);
        Assert.Empty(_apple.Requests);

        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Single(_apple.Requests);
    }

    [Fact]
    public async Task Two_replicas_passing_at_once_send_one_push()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token);
        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        // A second replica: its own sender over the same database.
        var services = _app.Factory.Services;
        var other = new AlertPushWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<AlertsStatus>(),
            services.GetRequiredService<AlertsOptions>(),
            _clock,
            _delay,
            services.GetRequiredService<ILogger<AlertPushWorker>>());

        await Task.WhenAll(
            Enumerable.Range(0, 8).Select(n => n % 2 == 0 ? Pushes.PassAsync(CancellationToken.None) : other.PassAsync(CancellationToken.None)));

        Assert.Single(_apple.Requests);
    }

    // ------------------------------------------------------------------ Apple's answers

    [Theory]
    [InlineData(410, "Unregistered")]
    [InlineData(400, "BadDeviceToken")]
    [InlineData(400, "DeviceTokenNotForTopic")]
    public async Task A_dead_token_is_cleared_and_logged_without_it(int status, string reason)
    {
        var (id, token) = await RegisteredAsync(ApnsFixture.Token);
        _apple.Then(() => ApnsHandler.Answer((HttpStatusCode)status, reason));
        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await Pushes.PassAsync(CancellationToken.None);

        Assert.Empty(await _devices.PushTargetsAsync(CancellationToken.None));
        var cleared = Assert.Single(_logs.Entries, entry => entry.EventId.Id == SecurityEvents.AlertsPushTokenCleared);
        Assert.Equal(id, cleared.Fields["DeviceId"]);
        Assert.Contains(reason, cleared.Message);
        Assert.All(_logs.Entries, entry => Assert.DoesNotContain(ApnsFixture.Token, entry.Everything));
    }

    [Fact]
    public async Task Another_400_keeps_the_token()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token);
        _apple.Then(() => ApnsHandler.Answer(HttpStatusCode.BadRequest, "BadPriority"));
        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await Pushes.PassAsync(CancellationToken.None);

        Assert.Single(await _devices.PushTargetsAsync(CancellationToken.None));
        Assert.Single(_logs.Entries, entry => entry.EventId.Id == SecurityEvents.AlertsPushFailed);
    }

    [Theory]
    [InlineData(429, "TooManyRequests")]
    [InlineData(500, "InternalServerError")]
    [InlineData(503, "ServiceUnavailable")]
    public async Task A_429_or_5xx_is_tried_once_more_after_30_seconds_then_left_until_the_next_change(int status, string reason)
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token);
        _apple.Then(() => ApnsHandler.Answer((HttpStatusCode)status, reason));
        _apple.Then(() => ApnsHandler.Answer((HttpStatusCode)status, reason));
        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(2, _apple.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(30)], _delay.Waits);
        Assert.Single(_logs.Entries, entry => entry.EventId.Id == SecurityEvents.AlertsPushRetrying);
        Assert.Single(_logs.Entries, entry => entry.EventId.Id == SecurityEvents.AlertsPushFailed);
        Assert.Single(await _devices.PushTargetsAsync(CancellationToken.None));

        // Given up: later passes send nothing until something changes.
        _clock.Now = Eight + Duration.FromMinutes(10);
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Equal(2, _apple.Requests.Count);

        await phone.PostAsJsonAsync("/api/alerts/unmute", new { });
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Equal(3, _apple.Requests.Count);
    }

    [Fact]
    public async Task A_retry_that_succeeds_is_logged_as_sent()
    {
        var (id, token) = await RegisteredAsync(ApnsFixture.Token);
        _apple.Then(() => ApnsHandler.Answer(HttpStatusCode.ServiceUnavailable, "ServiceUnavailable"));
        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(2, _apple.Requests.Count);
        var sent = Assert.Single(_logs.Entries, entry => entry.EventId.Id == SecurityEvents.AlertsPushSent);
        Assert.Equal(id, sent.Fields["DeviceId"]);
        Assert.All(_logs.Entries, entry =>
        {
            Assert.DoesNotContain(ApnsFixture.Token, entry.Everything);
            Assert.DoesNotContain("PRIVATE KEY", entry.Everything);
            Assert.DoesNotContain("bearer", entry.Everything, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task The_push_the_sender_makes_is_exactly_the_one_apple_needs()
    {
        var (_, token) = await RegisteredAsync(ApnsFixture.Token, "sandbox");
        using var phone = Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await Pushes.PassAsync(CancellationToken.None);

        var push = Assert.Single(_apple.Requests);
        Assert.Equal($"https://api.sandbox.push.apple.com/3/device/{ApnsFixture.Token}", push.Url.ToString());
        Assert.Equal(HttpVersion.Version20, push.Version);
        Assert.Equal(
            $"{{\"aps\":{{\"content-available\":1}},\"v\":{await _devices.PlanVersionAsync(CancellationToken.None)}}}",
            push.Body);
        Assert.Equal("background", push.Headers["apns-push-type"]);
        Assert.Equal("5", push.Headers["apns-priority"]);
        Assert.Equal("tech.abera.alarms", push.Headers["apns-topic"]);
        Assert.Equal("plan", push.Headers["apns-collapse-id"]);
        Assert.Equal((Eight + Duration.FromHours(1)).ToUnixTimeSeconds().ToString(), push.Headers["apns-expiration"]);

        // The client the server registers asks for HTTP/2 and nothing else.
        var http = _app.Factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ApnsClient));
        Assert.Equal(HttpVersion.Version20, http.DefaultRequestVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, http.DefaultVersionPolicy);
    }

    // ------------------------------------------------------------------ missing secrets

    [Theory]
    [InlineData("Alerts:ApnsKeyP8", "Alerts__ApnsKeyP8")]
    [InlineData("Alerts:ApnsKeyId", "Alerts__ApnsKeyId")]
    [InlineData("Alerts:ApnsTeamId", "Alerts__ApnsTeamId")]
    public async Task A_missing_secret_turns_pushes_off_keeps_registration_and_is_named_on_the_page(string setting, string name)
    {
        using var missing = new AlertPushRouteTests(apns: true, leaveOut: setting);
        var (_, token) = await missing.RegisteredAsync(ApnsFixture.Token);
        using var phone = missing.Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await missing.Pushes.PassAsync(CancellationToken.None);

        Assert.False(missing.Pushes.On);
        Assert.Empty(missing._apple.Requests);
        Assert.Single(await missing._devices.PushTargetsAsync(CancellationToken.None));
        using var owner = missing.Owner();
        var push = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("push");
        Assert.False(push.GetProperty("on").GetBoolean());
        Assert.Equal([name], push.GetProperty("missing").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task With_no_secrets_pushes_are_off_registration_still_stores_and_the_page_lists_all_three()
    {
        using var off = new AlertPushRouteTests(apns: false);
        var (_, token) = await off.RegisteredAsync(ApnsFixture.Token);
        using var phone = off.Phone(token);
        await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });

        await off.Pushes.PassAsync(CancellationToken.None);

        Assert.False(off.Pushes.On);
        Assert.Empty(off._apple.Requests);
        Assert.Single(await off._devices.PushTargetsAsync(CancellationToken.None));
        using var owner = off.Owner();
        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.True(status.GetProperty("configured").GetBoolean());
        var push = status.GetProperty("push");
        Assert.False(push.GetProperty("on").GetBoolean());
        Assert.Equal(
            ["Alerts__ApnsKeyP8", "Alerts__ApnsKeyId", "Alerts__ApnsTeamId"],
            push.GetProperty("missing").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task With_all_three_the_page_says_pushes_are_on_and_names_no_value()
    {
        using var owner = Owner();
        var text = await owner.GetStringAsync("/api/alerts/status");

        var push = JsonDocument.Parse(text).RootElement.GetProperty("push");
        Assert.True(push.GetProperty("on").GetBoolean());
        Assert.Empty(push.GetProperty("missing").EnumerateArray());
        Assert.DoesNotContain("PRIVATE KEY", text);
        Assert.DoesNotContain(ApnsFixture.KeyId, text);
        Assert.DoesNotContain(ApnsFixture.TeamId, text);
    }

    [Fact]
    public void A_key_that_is_not_a_p256_key_counts_as_missing()
    {
        var options = new AlertsOptions { ApnsKeyP8 = "not a key", ApnsKeyId = "K", ApnsTeamId = "T" };

        Assert.Equal(["Alerts__ApnsKeyP8"], options.ApnsMissing());
    }

    /// <summary>The wait before a retry, recorded rather than slept.</summary>
    private sealed class RecordingDelay : ApnsDelay
    {
        private readonly List<TimeSpan> _waits = [];

        public IReadOnlyList<TimeSpan> Waits
        {
            get { lock (_waits) return [.. _waits]; }
        }

        public override Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            lock (_waits) _waits.Add(delay);
            return Task.CompletedTask;
        }
    }
}
