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
/// default, 404 for an unknown id, 409 past 50, the status listing them
/// by target, the calendar event each makes, moves and deletes, its alarm,
/// and the zone a new one starts in.
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

    private static string Body(
        string label = "Home", string targetAt = "2027-03-01T09:00:00+03:00", string timeZone = "Asia/Amman", bool? alarm = null) =>
        alarm is { } on
            ? $"{{\"label\":\"{label}\",\"targetAt\":\"{targetAt}\",\"timeZone\":\"{timeZone}\",\"alarm\":{(on ? "true" : "false")}}}"
            : $"{{\"label\":\"{label}\",\"targetAt\":\"{targetAt}\",\"timeZone\":\"{timeZone}\"}}";

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
            ["id", "label", "targetAt", "timeZone", "updatedAt", "alarm", "onCalendar", "repeat", "repeatEvery"],
            countdown.EnumerateObject().Select(field => field.Name));
        Assert.True(countdown.GetProperty("id").GetGuid() != Guid.Empty);
        Assert.Equal("Home", countdown.GetProperty("label").GetString());
        Assert.Equal(Instant.FromUtc(2027, 3, 1, 6, 0).ToDateTimeOffset(), countdown.GetProperty("targetAt").GetDateTimeOffset());
        Assert.Equal("Asia/Amman", countdown.GetProperty("timeZone").GetString());
        Assert.Equal(Eight.ToDateTimeOffset(), countdown.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.False(countdown.GetProperty("alarm").GetBoolean());
        Assert.True(countdown.GetProperty("onCalendar").GetBoolean());
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
    public async Task A_countdown_with_no_alarm_reaching_its_target_sends_nothing_through_Pushover()
    {
        using var owner = Owner();
        await Send(owner, "POST", "/api/alerts/countdowns", Body(label: "Now", targetAt: "2026-10-28T12:01:00Z"));
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 2);

        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.All(
            status.GetProperty("alerts").EnumerateArray().Where(alert => alert.GetProperty("title").GetString() == "Now"),
            alert => Assert.Equal(AlertTypes.None, alert.GetProperty("type").GetString()));
        Assert.Empty(_pushover.Requests);
    }

    // ------------------------------------------------------------------ calendar event and alarm

    private string OnlyEvent() => Assert.Single(_google.Inserted);

    private async Task<JsonElement> StatusAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/alerts/status");

    [Fact]
    public async Task A_create_puts_an_event_at_the_target_on_the_calendar_with_no_alarm()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Home);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var uid = OnlyEvent();
        var made = _google.Master(uid)!.Value;
        Assert.Equal("Home", made.Summary);
        Assert.Equal(Instant.FromUtc(2027, 3, 1, 6, 0), made.Start);
        Assert.Equal(Instant.FromUtc(2027, 3, 1, 6, 30), made.End);
        Assert.Equal(0, made.Popup);
        Assert.DoesNotContain(AlertPlanner.CriticalMark, _google.Description(uid) ?? "", StringComparison.Ordinal);
        Assert.Equal(AlertTypes.None, (await _store.EventTypesAsync(CancellationToken.None))[AlertPlanner.EventIdFor(uid)]);
        Assert.Equal(uid, Assert.Single(await _store.CountdownsAsync(CancellationToken.None)).CalendarEventId);
    }

    [Fact]
    public async Task A_countdown_with_its_alarm_on_is_an_alarm_at_the_target_and_rings_through_Pushover()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns",
            Body(label: "Leave", targetAt: "2026-10-28T14:00:00Z", timeZone: "America/New_York", alarm: true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await CountdownsIn(response)).Single().GetProperty("alarm").GetBoolean());
        var uid = OnlyEvent();
        Assert.Contains(AlertPlanner.CriticalMark, _google.Description(uid), StringComparison.Ordinal);
        var alert = (await StatusAsync(owner)).GetProperty("alerts").EnumerateArray()
            .Single(item => item.GetProperty("title").GetString() == "Leave");
        Assert.Equal(AlertTypes.Alarm, alert.GetProperty("type").GetString());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 14, 0).ToDateTimeOffset(), alert.GetProperty("alertAt").GetDateTimeOffset());

        _clock.Now = Instant.FromUtc(2026, 10, 28, 14, 0, 30);
        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        Assert.Contains(_pushover.Requests, request => request.Form.Values.Any(value => value.Contains("Leave", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Google_refusing_the_event_stores_no_countdown(bool unreachable)
    {
        using var owner = Owner();
        if (unreachable) _google.Unreachable = true;
        else _google.RefusingWrites = (HttpStatusCode.Forbidden, null);

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Home);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
        Assert.Empty(await _store.EventTypesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task No_calendar_connected_is_409_and_stores_nothing()
    {
        using var owner = Owner();
        _google.Grant = null;

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", Home);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(CalendarWriteMessages.NotConnected,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_update_moves_and_renames_the_event_and_switching_the_alarm_on_marks_it()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        var uid = OnlyEvent();

        using var response = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}",
            Body(label: "Leave", targetAt: "2027-01-10T08:00:00-05:00", timeZone: "America/New_York", alarm: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(uid, OnlyEvent());
        var moved = _google.Master(uid)!.Value;
        Assert.Equal("Leave", moved.Summary);
        Assert.Equal(Instant.FromUtc(2027, 1, 10, 13, 0), moved.Start);
        Assert.Equal(Instant.FromUtc(2027, 1, 10, 13, 30), moved.End);
        Assert.Contains(AlertPlanner.CriticalMark, _google.Description(uid), StringComparison.Ordinal);
        Assert.Equal(AlertTypes.Alarm, (await _store.EventTypesAsync(CancellationToken.None))[AlertPlanner.EventIdFor(uid)]);
        Assert.True((await CountdownsIn(response)).Single().GetProperty("alarm").GetBoolean());
    }

    [Fact]
    public async Task Switching_the_alarm_off_removes_the_mark_and_an_update_without_the_field_keeps_it()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Body(alarm: true));
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        var uid = OnlyEvent();

        // An older phone app sends no alarm field: the alarm stays on.
        using var kept = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Renamed"));
        Assert.True((await CountdownsIn(kept)).Single().GetProperty("alarm").GetBoolean());
        Assert.Contains(AlertPlanner.CriticalMark, _google.Description(uid), StringComparison.Ordinal);

        using var off = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Renamed", alarm: false));

        Assert.False((await CountdownsIn(off)).Single().GetProperty("alarm").GetBoolean());
        Assert.DoesNotContain(AlertPlanner.CriticalMark, _google.Description(uid), StringComparison.Ordinal);
        Assert.Equal(AlertTypes.None, (await _store.EventTypesAsync(CancellationToken.None))[AlertPlanner.EventIdFor(uid)]);
    }

    [Fact]
    public async Task A_countdown_saved_before_events_gets_one_on_its_next_save()
    {
        var id = Guid.NewGuid();
        await _store.AddCountdownAsync(
            new AlertCountdown(id, "Home", Instant.FromUtc(2027, 3, 1, 6, 0), "Asia/Amman", Eight), CancellationToken.None);
        using var owner = Owner();
        Assert.False((await StatusAsync(owner)).GetProperty("countdowns")[0].GetProperty("onCalendar").GetBoolean());

        using var response = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Home);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var uid = OnlyEvent();
        Assert.Equal(Instant.FromUtc(2027, 3, 1, 6, 0), _google.Master(uid)!.Value.Start);
        Assert.True((await CountdownsIn(response)).Single().GetProperty("onCalendar").GetBoolean());
    }

    [Fact]
    public async Task An_event_deleted_on_Google_is_made_again_on_the_next_save()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        var first = OnlyEvent();
        _google.ForgetInserted();

        using var response = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Again"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = OnlyEvent();
        Assert.NotEqual(first, second);
        Assert.Equal("Again", _google.Master(second)!.Value.Summary);
        Assert.Equal(second, Assert.Single(await _store.CountdownsAsync(CancellationToken.None)).CalendarEventId);
    }

    [Fact]
    public async Task Google_refusing_an_edit_changes_nothing()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        _google.RefusingWrites = (HttpStatusCode.Forbidden, null);

        using var response = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Other"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Home", Assert.Single(await _store.CountdownsAsync(CancellationToken.None)).Label);
    }

    [Fact]
    public async Task Delete_deletes_the_event_and_Google_refusing_keeps_the_countdown()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        var uid = OnlyEvent();

        _google.RefusingWrites = (HttpStatusCode.Forbidden, null);
        using var refused = await Send(owner, "DELETE", $"/api/alerts/countdowns/{id}", null);
        Assert.Equal(HttpStatusCode.BadGateway, refused.StatusCode);
        Assert.Single(await _store.CountdownsAsync(CancellationToken.None));

        _google.RefusingWrites = null;
        using var response = await Send(owner, "DELETE", $"/api/alerts/countdowns/{id}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(_google.Master(uid));
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Delete_of_a_countdown_whose_event_is_gone_still_deletes_it()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", Home);
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        _google.ForgetInserted();

        using var response = await Send(owner, "DELETE", $"/api/alerts/countdowns/{id}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await _store.CountdownsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_alarm_countdown_deleted_before_its_target_never_rings()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns",
            Body(label: "Leave", targetAt: "2026-10-28T14:00:00Z", alarm: true));
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();

        await Send(owner, "DELETE", $"/api/alerts/countdowns/{id}", null);
        _clock.Now = Instant.FromUtc(2026, 10, 28, 14, 0, 30);
        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        Assert.DoesNotContain(
            (await StatusAsync(owner)).GetProperty("alerts").EnumerateArray(), alert => alert.GetProperty("title").GetString() == "Leave");
        Assert.DoesNotContain(_pushover.Requests, request => request.Form.Values.Any(value => value.Contains("Leave", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_moved_alarm_countdown_rings_at_its_new_time_only()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns",
            Body(label: "Leave", targetAt: "2026-10-28T14:00:00Z", alarm: true));
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();

        await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Leave", targetAt: "2026-10-28T16:00:00Z"));

        var alerts = (await StatusAsync(owner)).GetProperty("alerts").EnumerateArray()
            .Where(alert => alert.GetProperty("title").GetString() == "Leave").ToList();
        var alert = Assert.Single(alerts);
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 16, 0).ToDateTimeOffset(), alert.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal(AlertTypes.Alarm, alert.GetProperty("type").GetString());
    }

    // ------------------------------------------------------------------ one-time alarms delete themselves

    private static string WithRepeat(string label, string targetAt, string repeat, bool alarm = false) =>
        $"{{\"label\":\"{label}\",\"targetAt\":\"{targetAt}\",\"timeZone\":\"America/New_York\",\"alarm\":{(alarm ? "true" : "false")},\"repeat\":\"{repeat}\"}}";

    private async Task<string> KeyOfAsync(HttpClient client, string title) =>
        (await StatusAsync(client)).GetProperty("alerts").EnumerateArray()
        .Single(alert => alert.GetProperty("title").GetString() == title).GetProperty("key").GetString()!;

    [Fact]
    public async Task A_one_time_countdown_alarm_deletes_the_countdown_once_stopped_and_keeps_the_event()
    {
        using var phone = await PhoneAsync();
        await Send(phone, "POST", "/api/alerts/countdowns", Body(label: "Leave", targetAt: "2026-10-28T14:00:00Z", alarm: true));
        await Send(phone, "POST", "/api/alerts/countdowns", Body(label: "Quiet", targetAt: "2026-10-28T14:00:00Z"));
        var uid = _google.Inserted[0];
        _clock.Now = Instant.FromUtc(2026, 10, 28, 14, 0, 30);
        var key = await KeyOfAsync(phone, "Leave");

        using var stopped = await Send(phone, "POST", "/api/alerts/ack", $"{{\"key\":\"{key}\",\"via\":\"phone\"}}");

        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        Assert.Equal(["Quiet"], (await CountdownsIn(stopped)).Select(countdown => countdown.GetProperty("label").GetString()));
        Assert.NotNull(_google.Master(uid));
    }

    [Fact]
    public async Task A_countdown_alarm_is_kept_until_it_is_stopped()
    {
        using var owner = Owner();
        await Send(owner, "POST", "/api/alerts/countdowns", Body(label: "Leave", targetAt: "2026-10-28T14:00:00Z", alarm: true));
        _clock.Now = Instant.FromUtc(2026, 10, 28, 15, 0);
        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        Assert.Single(await _store.CountdownsAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ countdowns that repeat

    [Fact]
    public async Task A_yearly_countdown_counts_to_its_next_date_and_its_event_repeats_from_there()
    {
        using var owner = Owner();

        using var created = await Send(owner, "POST", "/api/alerts/countdowns",
            WithRepeat("Wedding anniversary", "2019-12-30T00:00:00-05:00", "yearly", alarm: true));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var countdown = (await CountdownsIn(created)).Single();
        Assert.Equal("yearly", countdown.GetProperty("repeat").GetString());
        Assert.Equal(Instant.FromUtc(2026, 12, 30, 5, 0).ToDateTimeOffset(), countdown.GetProperty("targetAt").GetDateTimeOffset());
        var uid = OnlyEvent();
        Assert.Equal(["RRULE:FREQ=YEARLY"], _google.Recurrence(uid));
        Assert.Equal(Instant.FromUtc(2026, 12, 30, 5, 0), _google.Master(uid)!.Value.Start);
        Assert.Contains(AlertPlanner.CriticalMark, _google.Description(uid), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repeating_countdown_moves_to_the_next_date_once_its_time_comes_and_is_never_deleted()
    {
        using var phone = await PhoneAsync();
        await Send(phone, "POST", "/api/alerts/countdowns", WithRepeat("Standup", "2026-10-28T10:00:00-04:00", "weekly", alarm: true));
        _clock.Now = Instant.FromUtc(2026, 10, 28, 14, 0, 30);
        var key = await KeyOfAsync(phone, "Standup");

        using var stopped = await Send(phone, "POST", "/api/alerts/ack", $"{{\"key\":\"{key}\",\"via\":\"phone\"}}");

        var countdown = (await CountdownsIn(stopped)).Single();
        Assert.Equal(Instant.FromUtc(2026, 11, 4, 15, 0).ToDateTimeOffset(), countdown.GetProperty("targetAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Editing_a_repeating_countdown_makes_its_series_again_and_delete_deletes_the_series()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/countdowns", WithRepeat("Birthday", "1991-10-07T09:00:00-04:00", "yearly"));
        var id = (await CountdownsIn(created)).Single().GetProperty("id").GetGuid();
        var first = OnlyEvent();

        using var edited = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}", Body(label: "Neb's birthday", targetAt: "1991-10-07T09:00:00-04:00", timeZone: "America/New_York"));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        // No repeat in the body keeps the saved one.
        Assert.Equal("yearly", (await CountdownsIn(edited)).Single().GetProperty("repeat").GetString());
        Assert.Null(_google.Master(first));
        var second = Assert.Single(_google.Inserted, uid => _google.Master(uid) is not null);
        Assert.Equal("Neb's birthday", _google.Master(second)!.Value.Summary);
        Assert.Equal(Instant.FromUtc(2027, 10, 7, 13, 0), _google.Master(second)!.Value.Start);

        using var deleted = await Send(owner, "DELETE", $"/api/alerts/countdowns/{id}", null);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Null(_google.Master(second));
    }

    [Fact]
    public async Task Every_two_hours_counts_two_hours_on_and_tells_Google_the_interval()
    {
        using var owner = Owner();

        using var created = await Send(owner, "POST", "/api/alerts/countdowns",
            "{\"label\":\"Meds\",\"targetAt\":\"2026-10-28T08:00:00Z\",\"timeZone\":\"America/New_York\",\"repeat\":\"hourly\",\"repeatEvery\":2}");

        var countdown = (await CountdownsIn(created)).Single();
        Assert.Equal(2, countdown.GetProperty("repeatEvery").GetInt32());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 14, 0).ToDateTimeOffset(), countdown.GetProperty("targetAt").GetDateTimeOffset());
        Assert.Equal(["RRULE:FREQ=HOURLY;INTERVAL=2"], _google.Recurrence(OnlyEvent()));

        // An update without the interval keeps it.
        var id = countdown.GetProperty("id").GetGuid();
        using var renamed = await Send(owner, "PUT", $"/api/alerts/countdowns/{id}",
            "{\"label\":\"Pills\",\"targetAt\":\"2026-10-28T08:00:00Z\",\"timeZone\":\"America/New_York\"}");
        Assert.Equal(2, (await CountdownsIn(renamed)).Single().GetProperty("repeatEvery").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task An_interval_outside_1_to_999_is_400(int every)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns",
            $"{{\"targetAt\":\"2027-01-01T00:00:00Z\",\"timeZone\":\"UTC\",\"repeat\":\"daily\",\"repeatEvery\":{every}}}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("repeatEvery", out _));
    }

    [Fact]
    public async Task A_repeat_the_page_would_not_send_is_400()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/countdowns", WithRepeat("X", "2027-01-01T00:00:00Z", "fortnightly"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("repeat", out _));
        Assert.Empty(_google.Inserted);
    }

    // ------------------------------------------------------------------ the zone a new one starts in

    [Fact]
    public async Task A_new_countdown_starts_in_New_York_until_a_phone_reports_its_zone()
    {
        using var owner = Owner();
        Assert.Equal("America/New_York", (await StatusAsync(owner)).GetProperty("defaultTimeZone").GetString());

        using var phone = await PhoneAsync();
        phone.DefaultRequestHeaders.Add(AlertDeviceZones.Header, "Asia/Amman");
        using var reported = await phone.GetAsync("/api/alerts/status");

        Assert.Equal("Asia/Amman", (await StatusAsync(owner)).GetProperty("defaultTimeZone").GetString());
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
