using System.Net;
using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The development calendar behind `make up` and the browser suite, on an
/// app that has been up for hours. Its events were placed from the process
/// start, so the suite failed once the standup began, 3 h in. The reset the
/// suite calls first places them from the present.
/// </summary>
public sealed class DevelopmentCalendarTests : IDisposable
{
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly FakeClock _clock = new(Start);
    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public DevelopmentCalendarTests()
    {
        // Development seeds the scheduling database at start.
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("alerts_reset");
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
            services =>
            {
                // The worker is driven by hand, at the fake clock's time.
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IClock>(_clock);
            },
            environment: "Development");
    }

    private CalendarAlertWorker Worker => _app!.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private HttpClient Owner() => _app!.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private static List<string?> Titles(JsonElement state) =>
        [.. state.GetProperty("alerts").EnumerateArray().Select(alert => alert.GetProperty("title").GetString())];

    /// <summary>
    /// 2 h 50 m: the standup's reminder has passed. 3 h 10 m: the standup
    /// has begun. A day: every event has.
    /// </summary>
    [PostgresFact]
    public async Task After_hours_of_uptime_the_reset_puts_every_alert_ahead_of_the_present()
    {
        await Worker.TickAsync(CancellationToken.None);
        using var owner = Owner();

        foreach (var minutesUp in new[] { 170, 190, 24 * 60 })
        {
            _clock.Now = Start + Duration.FromMinutes(minutesUp);
            await Worker.TickAsync(CancellationToken.None);

            using var response = await owner.PostAsync("/api/alerts/fake/reset", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var state = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(_clock.Now.ToDateTimeOffset(), state.GetProperty("lastFetchAt").GetDateTimeOffset());
            Assert.Equal(["E2E standup", "E2E review"], Titles(state));
            foreach (var alert in state.GetProperty("alerts").EnumerateArray())
            {
                Assert.True(alert.GetProperty("alertAt").GetDateTimeOffset() > _clock.Now.ToDateTimeOffset(), $"{minutesUp} min");
            }
        }
    }

    [PostgresFact]
    public async Task A_skip_holds_across_reads_after_the_reset()
    {
        _clock.Now = Start + Duration.FromHours(4);
        using var owner = Owner();
        using var reset = await owner.PostAsync("/api/alerts/fake/reset", null);
        var key = JsonDocument.Parse(await reset.Content.ReadAsStringAsync()).RootElement
            .GetProperty("alerts")[0].GetProperty("key").GetString();

        using var skipped = await owner.PostAsync("/api/alerts/skip",
            new StringContent(JsonSerializer.Serialize(new { key }), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
        _clock.Now += Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);

        using var status = await owner.GetAsync("/api/alerts/status");
        var first = JsonDocument.Parse(await status.Content.ReadAsStringAsync()).RootElement.GetProperty("alerts")[0];
        Assert.Equal(key, first.GetProperty("key").GetString());
        Assert.True(first.GetProperty("skipped").GetBoolean());
    }

    [PostgresFact]
    public async Task A_visitor_cannot_reset_the_calendar_or_read_what_the_fake_pushover_took()
    {
        using var visitor = _app!.CreateClient();

        using var response = await visitor.PostAsync("/api/alerts/fake/reset", null);
        using var sent = await visitor.GetAsync("/api/alerts/fake/sent");
        using var fail = await visitor.PostAsync("/api/alerts/fake/fail", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sent.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, fail.StatusCode);
    }

    [PostgresFact]
    public async Task The_fake_pushover_shows_the_browser_suite_what_the_test_alert_asked_for()
    {
        using var owner = Owner();
        using var none = await owner.GetAsync("/api/alerts/fake/sent");
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);

        using var saved = await owner.PutAsync("/api/alerts/settings", new StringContent(
            JsonSerializer.Serialize(AlertsRouteTests.Form(("repeatSeconds", 120), ("stopAfterMinutes", 30), ("sound", "siren"))),
            System.Text.Encoding.UTF8,
            "application/json"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var test = await owner.PostAsync("/api/alerts/test", null);
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);

        using var sent = await owner.GetAsync("/api/alerts/fake/sent");
        var message = JsonDocument.Parse(await sent.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("2", message.GetProperty("priority").GetString());
        Assert.Equal("120", message.GetProperty("retry").GetString());
        Assert.Equal("1800", message.GetProperty("expire").GetString());
        Assert.Equal("siren", message.GetProperty("sound").GetString());
        Assert.Equal("Test alert", message.GetProperty("title").GetString());
    }

    /// <summary>
    /// The browser suite's way to show the page a calendar that cannot be
    /// read: the fake answers 404, as production did with a wrong address,
    /// until the next reset.
    /// </summary>
    [PostgresFact]
    public async Task A_failed_read_keeps_the_last_good_list_and_says_when_it_was_until_the_reset()
    {
        using var owner = Owner();
        using var good = await owner.PostAsync("/api/alerts/fake/reset", null);
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        var readAt = _clock.Now;
        _clock.Now += Duration.FromMinutes(7);

        using var failed = await owner.PostAsync("/api/alerts/fake/fail", null);

        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        var state = JsonDocument.Parse(await failed.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("HTTP 404", state.GetProperty("lastFetchError").GetString());
        Assert.Equal(_clock.Now.ToDateTimeOffset(), state.GetProperty("lastFetchAt").GetDateTimeOffset());
        Assert.Equal(readAt.ToDateTimeOffset(), state.GetProperty("lastSuccessAt").GetDateTimeOffset());
        Assert.Equal(["E2E standup", "E2E review"], Titles(state));

        _clock.Now += Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);
        using var still = await owner.GetAsync("/api/alerts/status");
        Assert.Equal("HTTP 404",
            JsonDocument.Parse(await still.Content.ReadAsStringAsync()).RootElement.GetProperty("lastFetchError").GetString());

        using var reset = await owner.PostAsync("/api/alerts/fake/reset", null);
        var after = JsonDocument.Parse(await reset.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, after.GetProperty("lastFetchError").ValueKind);
        Assert.Equal(_clock.Now.ToDateTimeOffset(), after.GetProperty("lastSuccessAt").GetDateTimeOffset());
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
