using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The compose app's alerts, as `make up` and the browser suite meet them:
/// Development, the real worker and the real database, with the calendar
/// and Pushover in memory. What the browser suite presses has to work here
/// first.
/// </summary>
public sealed class AlertsDevelopmentTests : IDisposable
{
    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public AlertsDevelopmentTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("alerts_dev");
        _app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = _database.ConnectionString,
                ["ClientAddress:ForwardedHops"] = "0",
                ["Admin:GoogleClientId"] = "development-placeholder",
                ["Admin:GoogleClientSecret"] = "development-placeholder",
                ["Admin:AllowedEmails:0"] = AdminRouteTests.Owner,
                ["Alerts:CalendarIcsUrl"] = "https://calendar.example.test/basic.ics",
                ["Alerts:PushoverAppToken"] = "development-placeholder",
                ["Alerts:PushoverUserKey"] = "development-placeholder",
                ["Alerts:Fake"] = "true"
            },
            environment: "Development");
    }

    [PostgresFact]
    public async Task The_fake_calendar_is_read_and_the_test_alert_reaches_the_fake_pushover()
    {
        using var owner = _app!.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

        JsonElement status = default;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
            if (status.GetProperty("lastFetchAt").ValueKind != JsonValueKind.Null) break;
            await Task.Delay(100);
        }

        Assert.Equal(JsonValueKind.Null, status.GetProperty("lastFetchError").ValueKind);
        // The fake account has one upload, listed before the built-ins.
        var sound = status.GetProperty("bounds").GetProperty("sounds")[0];
        Assert.Equal(FakeAlertServices.CustomSound, sound.GetProperty("name").GetString());
        Assert.True(sound.GetProperty("custom").GetBoolean());
        var titles = status.GetProperty("alerts").EnumerateArray().Select(alert => alert.GetProperty("title").GetString()).ToList();
        Assert.Contains("E2E standup", titles);
        Assert.Contains("E2E review", titles);
        Assert.DoesNotContain("E2E holiday", titles);
        Assert.DoesNotContain("E2E cancelled", titles);
        // The development calendar marks the review #critical and not the standup.
        var critical = status.GetProperty("alerts").EnumerateArray()
            .ToDictionary(alert => alert.GetProperty("title").GetString()!, alert => alert.GetProperty("critical").GetBoolean());
        Assert.True(critical["E2E review"]);
        Assert.False(critical["E2E standup"]);

        using var sent = await owner.PostAsync("/api/alerts/test", null);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var message = Assert.Single(_app.Factory.Services.GetRequiredService<FakeAlertServices>().Sent);
        Assert.Equal("2", message.Priority);
        Assert.Equal("Test alert", message.Title);

        using var muted = await owner.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        Assert.Equal(HttpStatusCode.OK, muted.StatusCode);
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal(JsonValueKind.String, after.GetProperty("mutedUntil").ValueKind);
    }

    [PostgresFact]
    public async Task A_registered_phone_is_pushed_through_the_fake_apple_with_a_key_made_at_start()
    {
        using var owner = _app!.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);
        using var paired = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Development phone" });
        var token = (await paired.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        using var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var apnsToken = new string('d', 64);

        using var registered = await phone.PutAsJsonAsync(
            "/api/alerts/devices/me/push", new { apnsToken, environment = "sandbox" });
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.True(status.GetProperty("push").GetProperty("on").GetBoolean());
        using var muted = await phone.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        Assert.Equal(HttpStatusCode.OK, muted.StatusCode);

        // The sender runs on its own and is woken by the change.
        JsonElement pushes = default;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            pushes = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/fake/pushes");
            if (pushes.GetArrayLength() > 0) break;
            await Task.Delay(100);
        }

        var push = Assert.Single(pushes.EnumerateArray());
        Assert.Equal(ApnsClient.SandboxHost, push.GetProperty("host").GetString());
        Assert.Equal("background", push.GetProperty("pushType").GetString());
        Assert.Equal("tech.abera.alarms", push.GetProperty("topic").GetString());
        Assert.DoesNotContain(apnsToken, pushes.GetRawText());
        var devices = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/devices");
        Assert.True(devices[0].GetProperty("push").GetBoolean());
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
