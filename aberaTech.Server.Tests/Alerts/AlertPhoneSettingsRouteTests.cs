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
/// The phone's alarm sound and snooze from every entry point: PUT
/// phone-settings from the page's cookie and a paired phone's token, the
/// settings form's PUT settings, and the status that carries them. A change
/// pushes the phones once. The same values again push nothing.
/// </summary>
public sealed class AlertPhoneSettingsRouteTests : IDisposable
{
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly ApnsHandler _apple = new();
    private readonly FakeGoogleCalendar _google = new();

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")])),
        "text/calendar"));

    private readonly TestApp _app;

    public AlertPhoneSettingsRouteTests()
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
        _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _app.Dispose();
        _key.Dispose();
    }

    private AlertPushWorker Pushes => _app.Factory.Services.GetRequiredService<AlertPushWorker>();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private HttpClient Phone(string token)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string path, string body) =>
        client.PutAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));

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

    private static long VersionIn(ApnsHandler.Seen push) =>
        JsonDocument.Parse(push.Body).RootElement.GetProperty("v").GetInt64();

    // ------------------------------------------------------------------ who

    [Fact]
    public async Task A_paired_phone_changes_its_sound_and_snooze_and_is_still_refused_the_settings_form_with_403()
    {
        var token = await RegisteredPhoneAsync();
        using var phone = Phone(token);

        using var changed = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"beacon\",\"snoozeMinutes\":5}");
        using var form = await phone.PutAsJsonAsync("/api/alerts/settings", AlertsRouteTests.Form(("phoneSound", "siren")));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, form.StatusCode);
        var stored = (await _store.SettingsAsync(CancellationToken.None))!;
        Assert.Equal(("beacon", 5), (stored.PhoneSound, stored.PhoneSnoozeMinutes));
    }

    [Fact]
    public async Task The_owners_cookie_changes_them_too()
    {
        using var owner = Owner();

        using var changed = await Put(owner, "/api/alerts/phone-settings", "{\"sound\":\"rise\",\"snoozeMinutes\":12}");

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var stored = (await _store.SettingsAsync(CancellationToken.None))!;
        Assert.Equal(("rise", 12), (stored.PhoneSound, stored.PhoneSnoozeMinutes));
    }

    [Fact]
    public async Task No_cookie_and_no_token_is_401_and_nothing_is_saved()
    {
        using var visitor = _app.CreateClient();

        using var response = await Put(visitor, "/api/alerts/phone-settings", "{\"sound\":\"chime\",\"snoozeMinutes\":10}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_revoked_phones_token_is_401()
    {
        using var owner = Owner();
        using var paired = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Old phone" });
        var device = await paired.Content.ReadFromJsonAsync<JsonElement>();
        using var revoked = await owner.DeleteAsync($"/api/alerts/devices/{device.GetProperty("id").GetString()}");
        using var phone = Phone(device.GetProperty("token").GetString()!);

        using var response = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"chime\",\"snoozeMinutes\":10}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ bounds

    public static IEnumerable<object?[]> BadBodies =>
    [
        ["sound", "{\"sound\":\"foghorn\",\"snoozeMinutes\":9}"],
        ["sound", "{\"sound\":\"Chime\",\"snoozeMinutes\":9}"],
        ["sound", "{\"sound\":\"\",\"snoozeMinutes\":9}"],
        ["sound", "{\"sound\":null,\"snoozeMinutes\":9}"],
        ["sound", "{\"snoozeMinutes\":9}"],
        ["snoozeMinutes", "{\"sound\":\"chime\",\"snoozeMinutes\":0}"],
        ["snoozeMinutes", "{\"sound\":\"chime\",\"snoozeMinutes\":31}"],
        ["snoozeMinutes", "{\"sound\":\"chime\",\"snoozeMinutes\":-1}"],
        ["snoozeMinutes", "{\"sound\":\"chime\",\"snoozeMinutes\":null}"],
        ["snoozeMinutes", "{\"sound\":\"chime\"}"]
    ];

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task A_value_outside_its_bounds_is_400_naming_the_field_and_nothing_is_saved_or_pushed(string field, string body)
    {
        var token = await RegisteredPhoneAsync();
        var before = await _devices.PlanVersionAsync(CancellationToken.None);
        using var phone = Phone(token);

        using var response = await Put(phone, "/api/alerts/phone-settings", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages), $"no error for {field}: {errors}");
        Assert.NotEmpty(messages.EnumerateArray());
        Assert.Single(errors.EnumerateObject());
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
        Assert.Equal(before, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Both_missing_names_both()
    {
        using var owner = Owner();

        using var response = await Put(owner, "/api/alerts/phone-settings", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(["snoozeMinutes", "sound"], errors.EnumerateObject().Select(error => error.Name).Order());
    }

    [Theory]
    [InlineData("default", 1)]
    [InlineData("pulse", 30)]
    [InlineData("chime", 9)]
    [InlineData("rise", 2)]
    [InlineData("siren", 29)]
    [InlineData("beacon", 15)]
    public async Task Every_sound_and_both_snooze_bounds_are_taken(string sound, int snooze)
    {
        using var owner = Owner();
        // Something other than the value sent, so the save changes the row.
        await _store.SaveSettingsAsync(
            AlertSettings.Defaults(new AlertsOptions()) with { PhoneSound = sound == "pulse" ? "chime" : "pulse", PhoneSnoozeMinutes = 7 },
            Eight,
            CancellationToken.None);

        using var response = await Put(owner, "/api/alerts/phone-settings", $"{{\"sound\":\"{sound}\",\"snoozeMinutes\":{snooze}}}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = (await _store.SettingsAsync(CancellationToken.None))!;
        Assert.Equal((sound, snooze), (stored.PhoneSound, stored.PhoneSnoozeMinutes));
    }

    // ------------------------------------------------------------------ what is kept and shown

    [Fact]
    public async Task The_status_carries_the_defaults_and_the_exact_bounds()
    {
        using var owner = Owner();

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal("default", status.GetProperty("settings").GetProperty("phoneSound").GetString());
        Assert.Equal(9, status.GetProperty("settings").GetProperty("phoneSnoozeMinutes").GetInt32());
        var bounds = status.GetProperty("bounds");
        Assert.Equal(
            """[{"value":"default","label":"iPhone default"},{"value":"pulse","label":"Pulse"},{"value":"chime","label":"Chime"},{"value":"rise","label":"Rise"},{"value":"siren","label":"Siren"},{"value":"beacon","label":"Beacon"}]""",
            bounds.GetProperty("phoneSounds").GetRawText());
        Assert.Equal("""{"min":1,"max":30}""", bounds.GetProperty("phoneSnoozeMinutes").GetRawText());
    }

    [Fact]
    public async Task A_change_is_kept_answers_with_the_whole_state_and_leaves_every_other_setting_alone()
    {
        var token = await RegisteredPhoneAsync();
        var saved = AlertSettings.Defaults(new AlertsOptions { RepeatSeconds = 120, StopAfterMinutes = 30 }) with
        {
            Sound = "siren",
            BackupDelaySeconds = 60
        };
        await _store.SaveSettingsAsync(saved, Eight, CancellationToken.None);
        using var phone = Phone(token);

        using var response = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"chime\",\"snoozeMinutes\":15}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Standup", answer.GetProperty("alerts")[0].GetProperty("title").GetString());
        Assert.Equal("chime", answer.GetProperty("settings").GetProperty("phoneSound").GetString());
        Assert.Equal(15, answer.GetProperty("settings").GetProperty("phoneSnoozeMinutes").GetInt32());
        Assert.Equal(saved with { PhoneSound = "chime", PhoneSnoozeMinutes = 15 }, await _store.SettingsAsync(CancellationToken.None));

        using var owner = Owner();
        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal("chime", status.GetProperty("settings").GetProperty("phoneSound").GetString());
        Assert.Equal(15, status.GetProperty("settings").GetProperty("phoneSnoozeMinutes").GetInt32());
        Assert.Equal(120, status.GetProperty("settings").GetProperty("repeatSeconds").GetInt32());
        Assert.Equal("siren", status.GetProperty("settings").GetProperty("sound").GetString());
    }

    // ------------------------------------------------------------------ pushes

    [Fact]
    public async Task A_change_pushes_the_phones_once()
    {
        var token = await RegisteredPhoneAsync();
        var before = await _devices.PlanVersionAsync(CancellationToken.None);
        using var phone = Phone(token);

        using var response = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"siren\",\"snoozeMinutes\":9}");
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var push = Assert.Single(_apple.Requests);
        Assert.Equal(before + 1, VersionIn(push));
        Assert.Equal(before + 1, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_same_values_again_change_nothing_and_push_nothing()
    {
        var token = await RegisteredPhoneAsync();
        using var phone = Phone(token);
        using var first = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"pulse\",\"snoozeMinutes\":4}");
        var after = await _devices.PlanVersionAsync(CancellationToken.None);
        _clock.Now = Eight + Duration.FromMinutes(5);
        await Pushes.PassAsync(CancellationToken.None);
        Assert.Single(_apple.Requests);

        _clock.Now = Eight + Duration.FromMinutes(10);
        using var again = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"pulse\",\"snoozeMinutes\":4}");
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(after, await _devices.PlanVersionAsync(CancellationToken.None));
        Assert.Single(_apple.Requests);
    }

    [Fact]
    public async Task The_defaults_sent_before_anything_is_saved_write_no_row_and_push_nothing()
    {
        var token = await RegisteredPhoneAsync();
        var before = await _devices.PlanVersionAsync(CancellationToken.None);
        using var phone = Phone(token);

        using var response = await Put(phone, "/api/alerts/phone-settings", "{\"sound\":\"default\",\"snoozeMinutes\":9}");
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await _store.SettingsAsync(CancellationToken.None));
        Assert.Equal(before, await _devices.PlanVersionAsync(CancellationToken.None));
        Assert.Empty(_apple.Requests);
    }

    [Fact]
    public async Task The_settings_form_saves_the_phone_fields_and_a_change_pushes_once()
    {
        await RegisteredPhoneAsync();
        var before = await _devices.PlanVersionAsync(CancellationToken.None);
        using var owner = Owner();

        using var response = await owner.PutAsJsonAsync(
            "/api/alerts/settings", AlertsRouteTests.Form(("phoneSound", "beacon"), ("phoneSnoozeMinutes", 20)));
        await Pushes.PassAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("settings");
        Assert.Equal("beacon", answer.GetProperty("phoneSound").GetString());
        Assert.Equal(20, answer.GetProperty("phoneSnoozeMinutes").GetInt32());
        var stored = (await _store.SettingsAsync(CancellationToken.None))!;
        Assert.Equal(("beacon", 20), (stored.PhoneSound, stored.PhoneSnoozeMinutes));
        var push = Assert.Single(_apple.Requests);
        Assert.Equal(before + 1, VersionIn(push));
    }

    [Fact]
    public async Task A_settings_save_that_leaves_the_phone_fields_as_they_are_does_not_bump_the_plan()
    {
        await RegisteredPhoneAsync();
        var before = await _devices.PlanVersionAsync(CancellationToken.None);
        using var owner = Owner();

        using var response = await owner.PutAsJsonAsync("/api/alerts/settings", AlertsRouteTests.Form(("notificationPriority", 1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Phone_settings_share_the_actions_limit()
    {
        var settings = AlertsRouteTests.Settings();
        using var app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton<IAlertStore>(new InMemoryAlertStore());
            services.AddSingleton<IAlertDeviceStore>(new InMemoryAlertDeviceStore());
            services.AddSingleton(new AlertsOptions
            {
                CalendarIcsUrl = AlertsRouteTests.FeedUrl,
                PushoverAppToken = AlertsRouteTests.AppToken,
                PushoverUserKey = AlertsRouteTests.UserKey,
                PushoverRetrySeconds = 0
            });
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
        });
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        for (var press = 0; press < AlertsEndpoints.DefaultActionsPerMinute; press++)
        {
            using var ok = await owner.PostAsync("/api/alerts/unmute", null);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var refused = await Put(owner, "/api/alerts/phone-settings", "{\"sound\":\"chime\",\"snoozeMinutes\":10}");
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }
}
