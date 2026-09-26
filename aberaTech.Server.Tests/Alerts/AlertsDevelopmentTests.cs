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
        var titles = status.GetProperty("alerts").EnumerateArray().Select(alert => alert.GetProperty("title").GetString()).ToList();
        Assert.Contains("E2E standup", titles);
        Assert.Contains("E2E review", titles);
        Assert.DoesNotContain("E2E holiday", titles);
        Assert.DoesNotContain("E2E cancelled", titles);

        using var sent = await owner.PostAsync("/api/alerts/test", null);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var message = Assert.Single(_app.Factory.Services.GetRequiredService<FakeAlertServices>().Sent);
        Assert.Equal("1", message.Priority);
        Assert.Equal("Test alert", message.Title);

        using var muted = await owner.PostAsJsonAsync("/api/alerts/mute", new { until = "hour" });
        Assert.Equal(HttpStatusCode.OK, muted.StatusCode);
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal(JsonValueKind.String, after.GetProperty("mutedUntil").ValueKind);
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
