using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;
using OpenTelemetry.Instrumentation.Http;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The /alerts page's API from three chairs. A visitor is told to sign in, a
/// stranger with a Google account is refused, and the owner reads the list,
/// mutes, skips and sends a test. The store is in memory and Pushover and
/// the calendar are recording handlers; DatabaseAlertStoreTests holds the
/// Postgres store to the same rules.
/// </summary>
public sealed class AlertsRouteTests : IDisposable
{
    internal const string FeedUrl = "https://calendar.google.com/calendar/ical/owner%40example.test/private-feedsecret0042/basic.ics";
    internal const string AppToken = "app-token-route-tests-Hq2";
    internal const string UserKey = "user-key-route-tests-Jw7";

    /// <summary>08:00 New York time on the morning of the standup.</summary>
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly RecordingHandler _pushover =
        new(() => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));
    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(
            Event("standup@google.com", "Standup", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]),
            Event("review@google.com", "Review", "20261028T140000", "20261028T150000")),
        "text/calendar"));
    private readonly TestApp _app;

    public AlertsRouteTests()
    {
        _app = App(Settings());
        _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    internal static Dictionary<string, string?> Settings(bool secrets = true)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["Alerts:CalendarIcsUrl"] = FeedUrl;
        if (secrets)
        {
            settings["Alerts:PushoverAppToken"] = AppToken;
            settings["Alerts:PushoverUserKey"] = UserKey;
        }

        return settings;
    }

    private TestApp App(Dictionary<string, string?> settings) => new(settings, services =>
    {
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        // The worker is driven by hand above, at the fake clock's time.
        services.RemoveAll<IHostedService>();
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IAlertStore>(_store);
        services.AddSingleton(new AlertsOptions
        {
            CalendarIcsUrl = settings.GetValueOrDefault("Alerts:CalendarIcsUrl"),
            PushoverAppToken = settings.GetValueOrDefault("Alerts:PushoverAppToken"),
            PushoverUserKey = settings.GetValueOrDefault("Alerts:PushoverUserKey"),
            PushoverRetrySeconds = 0
        });
        services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
        services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
    });

    public static IEnumerable<object[]> Routes =>
    [
        ["GET", "/api/alerts/status"],
        ["POST", "/api/alerts/mute"],
        ["POST", "/api/alerts/unmute"],
        ["POST", "/api/alerts/skip"],
        ["POST", "/api/alerts/unskip"],
        ["POST", "/api/alerts/test"],
        ["PUT", "/api/alerts/settings"]
    ];

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_visitor_with_no_session_is_told_to_sign_in(string method, string path)
    {
        using var visitor = _app.CreateClient();

        using var response = await AdminRouteTests.Send(visitor, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_pushover.Requests);
        Assert.Null(await _store.MutedUntilAsync(CancellationToken.None));
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_google_account_that_is_not_the_owner_is_refused(string method, string path)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);

        using var response = await AdminRouteTests.Send(stranger, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_pushover.Requests);
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_owner_reads_the_next_alerts_and_the_last_calendar_read()
    {
        using var owner = Owner();

        using var response = await owner.GetAsync("/api/alerts/status");
        var text = await response.Content.ReadAsStringAsync();
        var status = JsonDocument.Parse(text).RootElement;

        Assert.True(status.GetProperty("configured").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("mutedUntil").ValueKind);
        Assert.Equal(5, status.GetProperty("pollMinutes").GetInt32());
        Assert.Equal(10, status.GetProperty("defaultLeadMinutes").GetInt32());
        Assert.Equal("America/New_York", status.GetProperty("timeZone").GetString());
        Assert.Equal(Eight.ToDateTimeOffset(), status.GetProperty("lastFetchAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("lastFetchError").ValueKind);

        var alerts = status.GetProperty("alerts").EnumerateArray().ToList();
        Assert.Equal(["Standup", "Review"], alerts.Select(alert => alert.GetProperty("title").GetString()));
        var standup = alerts[0];
        Assert.Equal("Room 1", standup.GetProperty("location").GetString());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 13, 0).ToDateTimeOffset(), standup.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 12, 45).ToDateTimeOffset(), standup.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal("reminder", standup.GetProperty("source").GetString());
        Assert.Equal("default", alerts[1].GetProperty("source").GetString());
        Assert.False(standup.GetProperty("skipped").GetBoolean());
        Assert.False(standup.GetProperty("muted").GetBoolean());

        // The calendar's address and the Pushover keys are configuration
        // the page never needs.
        Assert.DoesNotContain("feedsecret", text);
        Assert.DoesNotContain(AppToken, text);
        Assert.DoesNotContain(UserKey, text);
    }

    [Fact]
    public async Task Mute_for_an_hour_mutes_the_alerts_inside_it_and_unmute_ends_it()
    {
        using var owner = Owner();

        using var muted = await owner.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        Assert.Equal(HttpStatusCode.OK, muted.StatusCode);
        var status = await muted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((Eight + Duration.FromHours(1)).ToDateTimeOffset(), status.GetProperty("mutedUntil").GetDateTimeOffset());
        Assert.Equal(Eight + Duration.FromHours(1), await _store.MutedUntilAsync(CancellationToken.None));
        var alerts = status.GetProperty("alerts").EnumerateArray().ToList();
        Assert.True(alerts[0].GetProperty("muted").GetBoolean());
        Assert.False(alerts[1].GetProperty("muted").GetBoolean());

        using var unmuted = await owner.PostAsync("/api/alerts/unmute", null);
        Assert.Equal(HttpStatusCode.OK, unmuted.StatusCode);
        Assert.Null(await _store.MutedUntilAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Mute_until_tomorrow_ends_at_six_in_the_morning_new_york_time()
    {
        using var owner = Owner();

        using var muted = await owner.PostAsJsonAsync("/api/alerts/mute", new { until = "morning" });

        // 08:00 on Wednesday: 06:00 on Thursday, which is 10:00 UTC in October.
        Assert.Equal(Instant.FromUtc(2026, 10, 29, 10, 0), await _store.MutedUntilAsync(CancellationToken.None));
        Assert.Equal(HttpStatusCode.OK, muted.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"until\":\"forever\"}")]
    [InlineData("{\"until\":null}")]
    public async Task A_mute_the_page_would_not_send_is_refused(string body)
    {
        using var owner = Owner();

        using var response = await owner.PostAsync(
            "/api/alerts/mute", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store.MutedUntilAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Skip_marks_one_occurrence_and_undo_clears_it()
    {
        using var owner = Owner();
        var key = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status"))
            .GetProperty("alerts")[0].GetProperty("key").GetString();

        using var skipped = await owner.PostAsJsonAsync("/api/alerts/skip", new { key });
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
        var status = await skipped.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(status.GetProperty("alerts")[0].GetProperty("skipped").GetBoolean());
        Assert.False(status.GetProperty("alerts")[1].GetProperty("skipped").GetBoolean());
        Assert.True(await _store.IsSkippedAsync(key!, CancellationToken.None));

        using var undone = await owner.PostAsJsonAsync("/api/alerts/unskip", new { key });
        Assert.Equal(HttpStatusCode.OK, undone.StatusCode);
        Assert.False(await _store.IsSkippedAsync(key!, CancellationToken.None));
    }

    [Fact]
    public async Task Only_an_occurrence_on_the_list_can_be_skipped()
    {
        using var owner = Owner();

        using var unknown = await owner.PostAsJsonAsync("/api/alerts/skip", new { key = "not-on-the-list|20261028T130000Z" });
        using var missing = await owner.PostAsJsonAsync("/api/alerts/skip", new { });
        using var huge = await owner.PostAsJsonAsync("/api/alerts/skip", new { key = new string('k', 5000) });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
        Assert.Empty(await _store.SkippedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_test_button_sends_one_emergency_message_that_repeats_until_acknowledged()
    {
        using var owner = Owner();

        using var response = await owner.PostAsync("/api/alerts/test", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(_pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("60", sent.Form["retry"]);
        Assert.Equal("10800", sent.Form["expire"]);
        Assert.Equal("Test alert", sent.Form["title"]);
        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal("sent", status.GetProperty("lastSend").GetProperty("outcome").GetString());
        Assert.Equal("Test alert", status.GetProperty("lastSend").GetProperty("title").GetString());
    }

    /// <summary>The form as the page sends it, at the configuration's defaults, with the changes given.</summary>
    internal static Dictionary<string, object?> Form(params (string Field, object? Value)[] changes)
    {
        var form = new Dictionary<string, object?>
        {
            ["priority"] = 2,
            ["repeatSeconds"] = 60,
            ["stopAfterMinutes"] = 180,
            ["sound"] = "",
            ["defaultLeadMinutes"] = 10,
            ["pollMinutes"] = 5,
            ["lookaheadHours"] = 48,
            ["includeAllDay"] = false,
            ["timeZone"] = "",
            ["ownerEmails"] = Array.Empty<string>()
        };
        foreach (var (field, value) in changes) form[field] = value;
        return form;
    }

    [Fact]
    public async Task The_status_carries_the_settings_in_force_and_the_bounds_the_form_needs()
    {
        using var owner = Owner();

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        var settings = status.GetProperty("settings");
        Assert.Equal(2, settings.GetProperty("priority").GetInt32());
        Assert.Equal(60, settings.GetProperty("repeatSeconds").GetInt32());
        Assert.Equal(180, settings.GetProperty("stopAfterMinutes").GetInt32());
        Assert.Equal("", settings.GetProperty("sound").GetString());
        Assert.Equal(10, settings.GetProperty("defaultLeadMinutes").GetInt32());
        Assert.Equal(5, settings.GetProperty("pollMinutes").GetInt32());
        Assert.Equal(48, settings.GetProperty("lookaheadHours").GetInt32());
        Assert.False(settings.GetProperty("includeAllDay").GetBoolean());
        Assert.Equal("", settings.GetProperty("timeZone").GetString());
        Assert.Empty(settings.GetProperty("ownerEmails").EnumerateArray());

        var bounds = status.GetProperty("bounds");
        Assert.Equal(30, bounds.GetProperty("repeatSeconds").GetProperty("min").GetInt32());
        Assert.Equal(10800, bounds.GetProperty("repeatSeconds").GetProperty("max").GetInt32());
        Assert.Equal(180, bounds.GetProperty("stopAfterMinutes").GetProperty("max").GetInt32());
        Assert.Equal(336, bounds.GetProperty("lookaheadHours").GetProperty("max").GetInt32());
        Assert.Equal(50, bounds.GetProperty("maxEmergencySounds").GetInt32());
        Assert.Equal(10, bounds.GetProperty("maxOwnerEmails").GetInt32());
        Assert.Equal(23, bounds.GetProperty("sounds").GetArrayLength());
    }

    public static IEnumerable<object?[]> OutOfBounds =>
    [
        ["priority", -1],
        ["priority", 3],
        ["priority", null],
        ["repeatSeconds", 29],
        ["repeatSeconds", 10801],
        ["stopAfterMinutes", 0],
        ["stopAfterMinutes", 181],
        ["sound", "foghorn"],
        ["sound", null],
        ["defaultLeadMinutes", -1],
        ["defaultLeadMinutes", 1441],
        ["pollMinutes", 0],
        ["pollMinutes", 61],
        ["lookaheadHours", 0],
        ["lookaheadHours", 337],
        ["includeAllDay", null],
        ["timeZone", "Mars/Olympus_Mons"],
        ["timeZone", null],
        ["ownerEmails", new[] { "not an address" }],
        ["ownerEmails", new[] { "" }],
        ["ownerEmails", Enumerable.Range(0, 11).Select(n => $"owner{n}@example.test").ToArray()],
        ["ownerEmails", null]
    ];

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public async Task A_setting_outside_its_bounds_is_refused_by_name_and_nothing_is_saved(string field, object? value)
    {
        using var owner = Owner();

        using var response = await owner.PutAsJsonAsync("/api/alerts/settings", Form((field, value)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages), $"no error for {field}: {errors}");
        Assert.NotEmpty(messages.EnumerateArray());
        Assert.Single(errors.EnumerateObject());
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Every_bound_itself_is_accepted()
    {
        using var owner = Owner();

        using var low = await owner.PutAsJsonAsync("/api/alerts/settings", Form(
            ("priority", 0), ("repeatSeconds", 30), ("stopAfterMinutes", 1), ("defaultLeadMinutes", 0),
            ("pollMinutes", 1), ("lookaheadHours", 1), ("sound", "none")));
        using var high = await owner.PutAsJsonAsync("/api/alerts/settings", Form(
            ("priority", 2), ("repeatSeconds", 10800), ("stopAfterMinutes", 180), ("defaultLeadMinutes", 1440),
            ("pollMinutes", 60), ("lookaheadHours", 336), ("sound", "pushover"),
            ("ownerEmails", Enumerable.Range(0, 10).Select(n => $"owner{n}@example.test").ToArray())));

        Assert.Equal(HttpStatusCode.OK, low.StatusCode);
        Assert.Equal(HttpStatusCode.OK, high.StatusCode);
    }

    [Fact]
    public async Task A_valid_save_is_stored_answers_with_the_state_and_comes_back_in_the_status()
    {
        using var owner = Owner();

        using var saved = await owner.PutAsJsonAsync("/api/alerts/settings", Form(
            ("priority", 1), ("repeatSeconds", 120), ("stopAfterMinutes", 30), ("sound", "siren"),
            ("pollMinutes", 2), ("lookaheadHours", 72), ("includeAllDay", true), ("timeZone", " Asia/Amman "),
            ("ownerEmails", new[] { " neb@work.example " })));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var answer = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("settings");
        Assert.Equal(1, answer.GetProperty("priority").GetInt32());
        var stored = (await _store.SettingsAsync(CancellationToken.None))!;
        Assert.Equal(["neb@work.example"], stored.OwnerEmails);
        Assert.Equal(
            new AlertSettings(1, 120, 30, "siren", 10, 2, 72, true, "Asia/Amman", stored.OwnerEmails), stored);

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        var settings = status.GetProperty("settings");
        Assert.Equal(120, settings.GetProperty("repeatSeconds").GetInt32());
        Assert.Equal(30, settings.GetProperty("stopAfterMinutes").GetInt32());
        Assert.Equal("siren", settings.GetProperty("sound").GetString());
        Assert.Equal(72, settings.GetProperty("lookaheadHours").GetInt32());
        Assert.True(settings.GetProperty("includeAllDay").GetBoolean());
        Assert.Equal("Asia/Amman", settings.GetProperty("timeZone").GetString());
        Assert.Equal("neb@work.example", settings.GetProperty("ownerEmails")[0].GetString());
        Assert.Equal(2, status.GetProperty("pollMinutes").GetInt32());
    }

    [Fact]
    public async Task A_new_lead_moves_the_planned_alert_in_the_answer_to_the_save()
    {
        using var owner = Owner();

        using var saved = await owner.PutAsJsonAsync("/api/alerts/settings", Form(("defaultLeadMinutes", 30)));

        // The review has no reminder of its own: 14:00 New York, 18:00 UTC.
        var state = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var alerts = state.GetProperty("alerts").EnumerateArray().ToList();
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 17, 30).ToDateTimeOffset(), alerts[1].GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal(30, state.GetProperty("defaultLeadMinutes").GetInt32());
    }

    [Fact]
    public async Task The_test_button_follows_the_saved_settings()
    {
        using var owner = Owner();
        using var saved = await owner.PutAsJsonAsync("/api/alerts/settings", Form(
            ("repeatSeconds", 120), ("stopAfterMinutes", 30), ("sound", "tugboat")));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var response = await owner.PostAsync("/api/alerts/test", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(_pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("120", sent.Form["retry"]);
        Assert.Equal("1800", sent.Form["expire"]);
        Assert.Equal("tugboat", sent.Form["sound"]);
    }

    [Fact]
    public async Task Pushover_refusing_the_test_is_a_bad_gateway_that_names_only_the_status()
    {
        _pushover.Then(() => RecordingHandler.Text(HttpStatusCode.BadRequest, "{\"user\":\"invalid\",\"status\":0}"));
        using var owner = Owner();

        using var response = await owner.PostAsync("/api/alerts/test", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal("HTTP 400", text);
    }

    [Fact]
    public async Task Ten_presses_a_minute_then_the_eleventh_is_refused()
    {
        using var owner = Owner();

        for (var press = 0; press < AlertsEndpoints.DefaultActionsPerMinute; press++)
        {
            using var ok = await owner.PostAsync("/api/alerts/unmute", null);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var refused = await owner.PostAsync("/api/alerts/test", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Empty(_pushover.Requests);
    }

    [Fact]
    public async Task A_deployment_missing_a_secret_names_it_and_maps_nothing_else()
    {
        using var partial = App(Settings(secrets: false));
        using var owner = partial.CreateClient().SignedInAs(partial.Factory.Services, AdminRouteTests.Owner);

        using var response = await owner.GetAsync("/api/alerts/status");
        var text = await response.Content.ReadAsStringAsync();
        var status = JsonDocument.Parse(text).RootElement;

        Assert.False(status.GetProperty("configured").GetBoolean());
        Assert.Equal(
            ["Alerts__PushoverAppToken", "Alerts__PushoverUserKey"],
            status.GetProperty("missing").EnumerateArray().Select(name => name.GetString()));
        Assert.DoesNotContain("feedsecret", text);
        var endpoints = partial.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        Assert.DoesNotContain(endpoints, endpoint => (endpoint as RouteEndpoint)?.RoutePattern.RawText == "/api/alerts/test");
        Assert.Null(partial.Factory.Services.GetService<CalendarAlertWorker>());
    }

    [Fact]
    public async Task Without_an_owner_sign_in_the_status_says_off_to_anyone_and_names_nothing()
    {
        using var bare = new TestApp(new Dictionary<string, string?>
        {
            ["ClientAddress:ForwardedHops"] = "0",
            ["Alerts:CalendarIcsUrl"] = FeedUrl
        });
        using var visitor = bare.CreateClient();

        var status = await visitor.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.False(status.GetProperty("configured").GetBoolean());
        Assert.Empty(status.GetProperty("missing").EnumerateArray());
    }

    [Fact]
    public void Traces_leave_out_the_calendar_address_and_keep_every_other_call()
    {
        var filter = _app.Factory.Services.GetRequiredService<IOptionsMonitor<HttpClientTraceInstrumentationOptions>>()
            .CurrentValue.FilterHttpRequestMessage;

        Assert.NotNull(filter);
        Assert.False(filter(new HttpRequestMessage(HttpMethod.Get, FeedUrl)));
        Assert.True(filter(new HttpRequestMessage(HttpMethod.Post, PushoverClient.Endpoint)));
        Assert.True(filter(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/subscriptions")));
    }

    [Fact]
    public void The_development_fakes_are_never_wired_outside_development()
    {
        var settings = Settings();
        settings["Alerts:Fake"] = "true";
        using var production = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IHostedService>();
        });

        Assert.NotNull(production.Factory.Services.GetService<CalendarAlertWorker>());
        Assert.Null(production.Factory.Services.GetService<FakeAlertServices>());
        var routes = production.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToList();
        Assert.Contains("/api/alerts/test", routes);
        Assert.DoesNotContain("/api/alerts/fake/reset", routes);
    }

    public void Dispose() => _app.Dispose();
}
