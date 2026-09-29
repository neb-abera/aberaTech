using System.Net;
using System.Net.Http.Json;
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
/// The development Google Calendar behind `make up` and the browser suite:
/// the real database and the real client, with Google's API in memory. A
/// type change and a new event are recorded where the browser suite reads
/// them, a paired phone cannot read that record, and the reset deletes the
/// events the suite created.
/// </summary>
public sealed class DevelopmentGoogleTests : IDisposable
{
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly FakeClock _clock = new(Start);
    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public DevelopmentGoogleTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("alerts_google");
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
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IClock>(_clock);
            },
            environment: "Development");
    }

    private HttpClient Owner() => _app!.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    [PostgresFact]
    public async Task A_new_event_and_a_type_change_are_recorded_and_the_reset_deletes_the_event()
    {
        using var owner = Owner();
        using var reset = await owner.PostAsync("/api/alerts/fake/reset", null);
        var standup = (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alerts").EnumerateArray()
            .Single(alert => alert.GetProperty("title").GetString() == "E2E standup").GetProperty("key").GetString();

        using var created = await owner.PostAsJsonAsync("/api/alerts/events", new
        {
            title = "E2E dentist",
            startsAt = "2026-10-28T16:00:00Z",
            durationMinutes = 30,
            type = "alarm",
            leadMinutes = 15
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains(
            (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alerts").EnumerateArray(),
            alert => alert.GetProperty("title").GetString() == "E2E dentist");

        using var typed = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = standup, type = "alarm" });
        Assert.Equal(JsonValueKind.Null, (await typed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("calendarWrite").ValueKind);

        var writes = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/fake/google")).EnumerateArray().ToList();
        Assert.Equal(["insert", "patch"], writes.Select(write => write.GetProperty("kind").GetString()));
        Assert.Equal("E2E dentist", writes[0].GetProperty("summary").GetString());
        Assert.Equal(15, writes[0].GetProperty("popupMinutes").GetInt32());
        Assert.Equal("e2e-standup", writes[1].GetProperty("eventId").GetString());
        Assert.Equal("Dial-in: room 4\n#critical", writes[1].GetProperty("description").GetString());

        // A phone may not read the record.
        using var pair = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Phone" });
        var token = (await pair.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var phone = _app!.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", token);
        foreach (var (method, path) in new[]
                 {
                     ("GET", "/api/alerts/fake/google"), ("POST", "/api/alerts/fake/reset"), ("POST", "/api/alerts/fake/due"),
                     ("GET", "/api/alerts/fake/sent")
                 })
        {
            using var refused = await phone.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The reset deletes it from Google, the feed lists it as cancelled,
        // and the server stops keeping it.
        using var again = await owner.PostAsync("/api/alerts/fake/reset", null);
        var titles = (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alerts").EnumerateArray()
            .Select(alert => alert.GetProperty("title").GetString());
        Assert.DoesNotContain("E2E dentist", titles);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>("/api/alerts/fake/google")).EnumerateArray());
        await using var scope = _app.Factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IAlertStore>().CreatedEventsAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
