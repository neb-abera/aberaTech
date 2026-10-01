using System.Net;
using System.Net.Http.Json;
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
/// Countdowns from every entry point: the page's cookie and a paired
/// phone's token on the four routes, each field's bounds, the label's
/// default, 404 for an unknown id, 409 past 50, and the status listing them
/// by target.
/// </summary>
public sealed class AlertCountdownRouteTests : IDisposable
{
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FakeGoogleCalendar _google = new();

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")])),
        "text/calendar"));

    private readonly TestApp _app;

    public AlertCountdownRouteTests()
    {
        var settings = AlertsRouteTests.Settings();
        // Enough presses for the creates below. The limit itself is AlertsRouteTests..
        settings["RateLimits:AlertsActionsPerMinute"] = "1000";
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
                PushoverRetrySeconds = 0
            });
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
            services.AddSingleton<IAlertCalendarGrant>(_google);
            services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
        });
        _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose() => _app.Dispose();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private async Task<HttpClient> PhoneAsync()
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Neb's iPhone" });
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return phone;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private static async Task<JsonElement[]> CountdownsIn(HttpResponseMessage response) =>
        [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("countdowns").EnumerateArray()];

    private const string Home = "{\"label\":\"Home\",\"targetAt\":\"2027-03-01T09:00:00+03:00\",\"timeZone\":\"Asia/Amman\"}";

    private static string Body(string label = "Home", string targetAt = "2027-03-01T09:00:00+03:00", string timeZone = "Asia/Amman") =>
        $"{{\"label\":\"{label}\",\"targetAt\":\"{targetAt}\",\"timeZone\":\"{timeZone}\"}}";

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task A_create_answers_201_with_the_whole_state_and_the_countdown_listed_with_its_fields()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Home);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(state.GetProperty("configured").GetBoolean());
        Assert.True(state.TryGetProperty("routines", out _));
        var countdown = Assert.Single(state.GetProperty("countdowns").EnumerateArray());
        Assert.Equal(
            ["id", "label", "targetAt", "timeZone", "updatedAt"],
            countdown.EnumerateObject().Select(field => field.Name));
        Assert.True(countdown.GetProperty("id").GetGuid() != Guid.Empty);
        Assert.Equal("Home", countdown.GetProperty("label").GetString());
        Assert.Equal(Instant.FromUtc(2027, 3, 1, 6, 0).ToDateTimeOffset(), countdown.GetProperty("targetAt").GetDateTimeOffset());
        Assert.Equal("Asia/Amman", countdown.GetProperty("timeZone").GetString());
        Assert.Equal(Eight.ToDateTimeOffset(), countdown.GetProperty("updatedAt").GetDateTimeOffset());
        // ISO 8601 with its offset, which the phone and the page parse.
        Assert.Matches(@"^2027-03-01T06:00:00(\+00:00|Z)$", countdown.GetProperty("targetAt").GetString());
    }

    [Fact]
    public async Task The_server_makes_the_id_and_ignores_one_in_the_body()
    {
        using var owner = Owner();
        var mine = Guid.NewGuid();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns",
            $"{{\"id\":\"{mine}\",\"targetAt\":\"2027-03-01T09:00:00+03:00\",\"timeZone\":\"UTC\"}}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(mine, (await CountdownsIn(response)).Single().GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("{\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"UTC\"}")]
    [InlineData("{\"label\":\"\",\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"UTC\"}")]
    [InlineData("{\"label\":\"   \",\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"UTC\"}")]
    [InlineData("{\"label\":null,\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"UTC\"}")]
    public async Task An_empty_blank_or_missing_label_is_stored_as_Countdown(string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Countdown", (await CountdownsIn(response)).Single().GetProperty("label").GetString());
    }

    [Fact]
    public async Task A_label_is_trimmed_and_60_characters_are_taken()
    {
        using var owner = Owner();
        var sixty = new string('a', 60);

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Body(label: $"  {sixty}  "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(sixty, (await CountdownsIn(response)).Single().GetProperty("label").GetString());
    }

    [Theory]
    [InlineData("2027-03-01T06:00:00Z")]
    [InlineData("2027-03-01T06:00:00.000Z")]
    [InlineData(" 2027-03-01T09:00:00+03:00 ")]
    [InlineData("2027-03-01T01:00:00-05:00")]
    public async Task A_target_is_taken_with_any_offset_and_stored_as_the_same_instant(string targetAt)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Body(targetAt: targetAt));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            Instant.FromUtc(2027, 3, 1, 6, 0).ToDateTimeOffset(),
            (await CountdownsIn(response)).Single().GetProperty("targetAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_target_in_the_past_is_taken_and_counts_up()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Body(label: "Deployed", targetAt: "2026-06-01T00:00:00+03:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("1900-01-01T00:00:00Z")]
    [InlineData("2200-01-01T00:00:00Z")]
    public async Task The_bounds_themselves_are_taken(string targetAt)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Body(targetAt: targetAt));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>Each bound, one past it, and the field the refusal names.</summary>
    public static IEnumerable<object[]> BadBodies =>
    [
        ["targetAt", "{\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":null,\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":\"\",\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":\"2027-03-01\",\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":\"2027-03-01T09:00:00\",\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":\"next spring\",\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":\"1899-12-31T23:59:59Z\",\"timeZone\":\"UTC\"}"],
        ["targetAt", "{\"targetAt\":\"2200-01-01T00:00:01Z\",\"timeZone\":\"UTC\"}"],
        ["timeZone", "{\"targetAt\":\"2027-03-01T09:00:00Z\"}"],
        ["timeZone", "{\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"\"}"],
        ["timeZone", "{\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"Mars/Olympus\"}"],
        ["timeZone", "{\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"+03:00\"}"],
        ["timeZone", $"{{\"targetAt\":\"2027-03-01T09:00:00Z\",\"timeZone\":\"{new string('a', 65)}\"}}"],
        ["label", Body(label: new string('a', 61))],
        ["label", Body(label: "home\\u0007")],
        ["label", Body(label: "home\\nagain")]
    ];

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task A_create_out_of_bounds_is_400_naming_the_field_and_stores_nothing(string field, string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_51st_is_409_with_a_detail_and_nothing_is_stored()
    {
        for (var n = 0; n < AlertCountdowns.MaxCountdowns; n++)
        {
            Assert.True(await _store.AddCountdownAsync(
                new AlertCountdown(Guid.NewGuid(), $"C{n}", Eight, "UTC", Eight), CancellationToken.None));
        }

        using var owner = Owner();
        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Home);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        Assert.Contains("50", detail, StringComparison.Ordinal);
        Assert.Equal(AlertCountdowns.MaxCountdowns, (await _store.CountdownsAsync(CancellationToken.None)).Count);
    }

    // ------------------------------------------------------------------ update and delete

    [Fact]
    public async Task An_update_replaces_every_field_keeps_the_id_and_answers_200_with_the_state()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        _clock.Now = Eight + Duration.FromMinutes(3);

        using var response = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}",
            Body(label: "  Leave ", targetAt: "2027-01-10T08:00:00-05:00", timeZone: "America/New_York"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var countdown = (await CountdownsIn(response)).Single();
        Assert.Equal(id, countdown.GetProperty("id").GetGuid());
        Assert.Equal("Leave", countdown.GetProperty("label").GetString());
        Assert.Equal(Instant.FromUtc(2027, 1, 10, 13, 0).ToDateTimeOffset(), countdown.GetProperty("targetAt").GetDateTimeOffset());
        Assert.Equal("America/New_York", countdown.GetProperty("timeZone").GetString());
        Assert.Equal((Eight + Duration.FromMinutes(3)).ToDateTimeOffset(), countdown.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("targetAt", "{\"label\":\"Home\",\"timeZone\":\"UTC\"}")]
    [InlineData("timeZone", "{\"label\":\"Home\",\"targetAt\":\"2027-03-01T09:00:00Z\"}")]
    public async Task An_update_with_a_missing_or_bad_field_is_400_and_changes_nothing(string field, string body)
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();

        using var response = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Equal("Asia/Amman", Assert.Single(await _store.CountdownsAsync(CancellationToken.None)).TimeZone);
    }

    [Fact]
    public async Task Delete_removes_it_and_answers_200_with_the_state()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();

        using var response = await Send(owner, "DELETE", $"/api/alerts/countdowns/{id}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await CountdownsIn(response));
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task An_unknown_id_is_404(string method)
    {
        using var owner = Owner();
        await Send(owner, "POST", "/api/alerts/countdowns", Home);

        using var response = await Send(owner, method, $"/api/alerts/countdowns/{Guid.NewGuid()}", method == "PUT" ? Body(label: "Other") : null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Home", Assert.Single(await _store.CountdownsAsync(CancellationToken.None)).Label);
    }

    // ------------------------------------------------------------------ status

    [Fact]
    public async Task The_status_lists_every_countdown_by_target_then_label()
    {
        using var owner = Owner();
        foreach (var body in new[]
                 {
                     Body(label: "b", targetAt: "2027-03-01T06:00:00Z"),
                     Body(label: "late", targetAt: "2028-01-01T00:00:00Z"),
                     Body(label: "a", targetAt: "2027-03-01T09:00:00+03:00"),
                     Body(label: "passed", targetAt: "2026-01-01T00:00:00Z")
                 })
        {
            await Send(owner, "POST", "/api/alerts/countdowns", body);
        }

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal(
            ["passed", "a", "b", "late"],
            status.GetProperty("countdowns").EnumerateArray().Select(countdown => countdown.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task The_status_carries_an_empty_list_before_any_countdown()
    {
        using var owner = Owner();

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal(JsonValueKind.Array, status.GetProperty("countdowns").ValueKind);
        Assert.Empty(status.GetProperty("countdowns").EnumerateArray());
    }

    [Fact]
    public async Task A_countdown_reaching_its_target_sends_nothing_through_Pushover()
    {
        using var owner = Owner();
        await Send(owner, "POST", "/api/alerts/countdowns", Body(label: "Now", targetAt: "2026-10-28T12:01:00Z"));
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 2);

        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.DoesNotContain(
            status.GetProperty("alerts").EnumerateArray(), alert => alert.GetProperty("title").GetString() == "Now");
        Assert.Empty(_pushover.Requests);
    }

    // ------------------------------------------------------------------ who may call

    [Fact]
    public async Task A_paired_phone_may_call_all_four_routes()
    {
        using var phone = await PhoneAsync();

        using var created = await Send(phone, "POST", "/api/alerts/countdowns", Home);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();

        using var status = await phone.GetAsync("/api/alerts/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Single(await CountdownsIn(status));

        using var updated = await Send(phone, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Phone"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using var deleted = await Send(phone, "DELETE", $"/api/alerts/countdowns/{id}", null);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("GET", "/api/alerts/status", null)]
    [InlineData("POST", "/api/alerts/countdowns", Home)]
    [InlineData("PUT", "/api/alerts/countdowns/0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10", Home)]
    [InlineData("DELETE", "/api/alerts/countdowns/0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10", null)]
    public async Task No_cookie_and_no_token_is_401_and_a_visitor_never_sees_a_countdown(string method, string path, string? body)
    {
        await _store.AddCountdownAsync(
            new AlertCountdown(Guid.Parse("0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10"), "Home", Eight, "Asia/Amman", Eight), CancellationToken.None);
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("Home", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("Home", Assert.Single(await _store.CountdownsAsync(CancellationToken.None)).Label);
    }

    [Fact]
    public async Task A_revoked_phones_token_is_401()
    {
        using var phone = await PhoneAsync();
        var device = Assert.Single(await _devices.ListAsync(CancellationToken.None));
        await _devices.RevokeAsync(device.Id, CancellationToken.None);

        using var response = await Send(phone, "POST", "/api/alerts/countdowns", Home);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }
}
