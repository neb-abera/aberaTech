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
/// Routine alarms from every entry point: the page's cookie and a paired
/// phone's token on the four routes, each field's bounds, the label's
/// default, the days put in order, 404 for an unknown id, 409 past 50, and
/// the status listing them by time.
/// </summary>
public sealed class AlertRoutineRouteTests : IDisposable
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

    public AlertRoutineRouteTests()
    {
        var settings = AlertsRouteTests.Settings();
        // Enough presses for the 51 creates below. The limit itself is AlertsRouteTests'.
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

    private static async Task<JsonElement[]> RoutinesIn(HttpResponseMessage response) =>
        [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routines").EnumerateArray()];

    private const string Wake = "{\"label\":\"Wake up\",\"hour\":6,\"minute\":30,\"days\":[1,2,3,4,5]}";

    private static string Whole(string label = "Wake up", int hour = 6, int minute = 30, string days = "[1,2,3,4,5]", bool enabled = true, int snooze = 9) =>
        $"{{\"label\":\"{label}\",\"hour\":{hour},\"minute\":{minute},\"days\":{days},\"enabled\":{(enabled ? "true" : "false")},\"snoozeMinutes\":{snooze}}}";

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task A_create_answers_201_with_the_whole_state_and_the_routine_listed_with_its_fields()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", Wake);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(state.GetProperty("configured").GetBoolean());
        Assert.True(state.TryGetProperty("alerts", out _));
        var routine = Assert.Single(state.GetProperty("routines").EnumerateArray());
        Assert.Equal(
            ["id", "label", "hour", "minute", "days", "enabled", "snoozeMinutes", "updatedAt"],
            routine.EnumerateObject().Select(field => field.Name));
        Assert.True(routine.GetProperty("id").GetGuid() != Guid.Empty);
        Assert.Equal("Wake up", routine.GetProperty("label").GetString());
        Assert.Equal(6, routine.GetProperty("hour").GetInt32());
        Assert.Equal(30, routine.GetProperty("minute").GetInt32());
        Assert.Equal([1, 2, 3, 4, 5], routine.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
        Assert.True(routine.GetProperty("enabled").GetBoolean());
        Assert.Equal(AlertRoutines.DefaultSnoozeMinutes, routine.GetProperty("snoozeMinutes").GetInt32());
        Assert.Equal(Eight.ToDateTimeOffset(), routine.GetProperty("updatedAt").GetDateTimeOffset());
        // ISO 8601 with its offset.
        Assert.Matches(@"^2026-10-28T12:00:00(\+00:00|Z)$", routine.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task The_server_makes_the_id_and_ignores_one_in_the_body()
    {
        using var owner = Owner();
        var mine = Guid.NewGuid();

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            $"{{\"id\":\"{mine}\",\"hour\":6,\"minute\":0,\"days\":[]}}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(mine, (await RoutinesIn(response)).Single().GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("{\"hour\":6,\"minute\":0,\"days\":[]}")]
    [InlineData("{\"label\":\"\",\"hour\":6,\"minute\":0,\"days\":[]}")]
    [InlineData("{\"label\":\"   \",\"hour\":6,\"minute\":0,\"days\":[]}")]
    [InlineData("{\"label\":null,\"hour\":6,\"minute\":0,\"days\":[]}")]
    public async Task An_empty_blank_or_missing_label_is_stored_as_Alarm(string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Alarm", (await RoutinesIn(response)).Single().GetProperty("label").GetString());
    }

    [Fact]
    public async Task A_label_is_trimmed_and_60_characters_are_taken()
    {
        using var owner = Owner();
        var sixty = new string('a', 60);

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            $"{{\"label\":\"  {sixty}  \",\"hour\":6,\"minute\":0,\"days\":[]}}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(sixty, (await RoutinesIn(response)).Single().GetProperty("label").GetString());
    }

    [Fact]
    public async Task Days_are_put_in_order()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", "{\"hour\":6,\"minute\":0,\"days\":[7,1,5,3]}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([1, 3, 5, 7], (await RoutinesIn(response)).Single().GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
    }

    [Fact]
    public async Task Enabled_false_and_a_snooze_are_taken_on_create()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            "{\"hour\":23,\"minute\":59,\"days\":[1,2,3,4,5,6,7],\"enabled\":false,\"snoozeMinutes\":30}");

        var routine = (await RoutinesIn(response)).Single();
        Assert.False(routine.GetProperty("enabled").GetBoolean());
        Assert.Equal(30, routine.GetProperty("snoozeMinutes").GetInt32());
        Assert.Equal(23, routine.GetProperty("hour").GetInt32());
        Assert.Equal(59, routine.GetProperty("minute").GetInt32());
    }

    /// <summary>Each bound, one past it, and the field the refusal names.</summary>
    public static IEnumerable<object[]> BadBodies =>
    [
        ["hour", "{\"hour\":-1,\"minute\":0,\"days\":[]}"],
        ["hour", "{\"hour\":24,\"minute\":0,\"days\":[]}"],
        ["hour", "{\"minute\":0,\"days\":[]}"],
        ["minute", "{\"hour\":6,\"minute\":-1,\"days\":[]}"],
        ["minute", "{\"hour\":6,\"minute\":60,\"days\":[]}"],
        ["minute", "{\"hour\":6,\"days\":[]}"],
        ["days", "{\"hour\":6,\"minute\":0}"],
        ["days", "{\"hour\":6,\"minute\":0,\"days\":null}"],
        ["days", "{\"hour\":6,\"minute\":0,\"days\":[0]}"],
        ["days", "{\"hour\":6,\"minute\":0,\"days\":[8]}"],
        ["days", "{\"hour\":6,\"minute\":0,\"days\":[1,1]}"],
        ["days", "{\"hour\":6,\"minute\":0,\"days\":[5,3,5]}"],
        ["snoozeMinutes", "{\"hour\":6,\"minute\":0,\"days\":[],\"snoozeMinutes\":0}"],
        ["snoozeMinutes", "{\"hour\":6,\"minute\":0,\"days\":[],\"snoozeMinutes\":31}"],
        ["label", $"{{\"label\":\"{new string('a', 61)}\",\"hour\":6,\"minute\":0,\"days\":[]}}"],
        ["label", "{\"label\":\"wake\\u0007up\",\"hour\":6,\"minute\":0,\"days\":[]}"],
        ["label", "{\"label\":\"wake\\nup\",\"hour\":6,\"minute\":0,\"days\":[]}"]
    ];

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task A_create_out_of_bounds_is_400_naming_the_field_and_stores_nothing(string field, string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(23, 59, 30)]
    public async Task The_bounds_themselves_are_taken(int hour, int minute, int snooze)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            $"{{\"hour\":{hour},\"minute\":{minute},\"days\":[1,7],\"snoozeMinutes\":{snooze}}}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_51st_is_409_with_a_detail_and_nothing_is_stored()
    {
        for (var n = 0; n < AlertRoutines.MaxRoutines; n++)
        {
            Assert.True(await _store.AddRoutineAsync(
                new AlertRoutine(Guid.NewGuid(), $"R{n}", 6, 0, [], true, 9, Eight), CancellationToken.None));
        }

        using var owner = Owner();
        using var response = await Send(owner, "POST", "/api/alerts/routines", Wake);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        Assert.Contains("50", detail, StringComparison.Ordinal);
        Assert.Equal(AlertRoutines.MaxRoutines, (await _store.RoutinesAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Fifty_creates_through_the_route_are_taken_and_the_next_is_refused()
    {
        using var owner = Owner();
        for (var n = 0; n < AlertRoutines.MaxRoutines; n++)
        {
            using var made = await Send(owner, "POST", "/api/alerts/routines", "{\"hour\":6,\"minute\":0,\"days\":[]}");
            Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        }

        using var response = await Send(owner, "POST", "/api/alerts/routines", "{\"hour\":6,\"minute\":0,\"days\":[]}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ------------------------------------------------------------------ update and delete

    [Fact]
    public async Task An_update_replaces_every_field_keeps_the_id_and_answers_200_with_the_state()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/routines", Wake);
        var id = (await RoutinesIn(created)).Single().GetProperty("id").GetGuid();
        _clock.Now = Eight + Duration.FromMinutes(3);

        using var response = await Send(owner, "PUT", $"/api/alerts/routines/{id}",
            Whole(label: "  Gym ", hour: 5, minute: 15, days: "[6,1]", enabled: false, snooze: 4));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var routine = (await RoutinesIn(response)).Single();
        Assert.Equal(id, routine.GetProperty("id").GetGuid());
        Assert.Equal("Gym", routine.GetProperty("label").GetString());
        Assert.Equal(5, routine.GetProperty("hour").GetInt32());
        Assert.Equal(15, routine.GetProperty("minute").GetInt32());
        Assert.Equal([1, 6], routine.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
        Assert.False(routine.GetProperty("enabled").GetBoolean());
        Assert.Equal(4, routine.GetProperty("snoozeMinutes").GetInt32());
        Assert.Equal((Eight + Duration.FromMinutes(3)).ToDateTimeOffset(), routine.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_phone_turns_a_ring_once_routine_off_with_a_put()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/routines", "{\"label\":\"Nap\",\"hour\":14,\"minute\":0,\"days\":[]}");
        var id = (await RoutinesIn(created)).Single().GetProperty("id").GetGuid();
        using var phone = await PhoneAsync();

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}", Whole(label: "Nap", hour: 14, minute: 0, days: "[]", enabled: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(Assert.Single(await _store.RoutinesAsync(CancellationToken.None)).Enabled);
    }

    [Theory]
    [InlineData("enabled", "{\"hour\":6,\"minute\":0,\"days\":[],\"snoozeMinutes\":9}")]
    [InlineData("snoozeMinutes", "{\"hour\":6,\"minute\":0,\"days\":[],\"enabled\":true}")]
    [InlineData("days", "{\"hour\":6,\"minute\":0,\"days\":[2,2],\"enabled\":true,\"snoozeMinutes\":9}")]
    [InlineData("hour", "{\"hour\":24,\"minute\":0,\"days\":[],\"enabled\":true,\"snoozeMinutes\":9}")]
    public async Task An_update_is_the_whole_body_and_a_missing_or_bad_field_is_400(string field, string body)
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/routines", Wake);
        var id = (await RoutinesIn(created)).Single().GetProperty("id").GetGuid();

        using var response = await Send(owner, "PUT", $"/api/alerts/routines/{id}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Equal("Wake up", Assert.Single(await _store.RoutinesAsync(CancellationToken.None)).Label);
    }

    [Fact]
    public async Task Delete_removes_it_and_answers_200_with_the_state()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/routines", Wake);
        var id = (await RoutinesIn(created)).Single().GetProperty("id").GetGuid();

        using var response = await Send(owner, "DELETE", $"/api/alerts/routines/{id}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await RoutinesIn(response));
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task An_unknown_id_is_404(string method)
    {
        using var owner = Owner();
        await Send(owner, "POST", "/api/alerts/routines", Wake);

        using var response = await Send(owner, method, $"/api/alerts/routines/{Guid.NewGuid()}", method == "PUT" ? Whole() : null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Wake up", Assert.Single(await _store.RoutinesAsync(CancellationToken.None)).Label);
    }

    [Fact]
    public async Task A_second_delete_is_404()
    {
        using var owner = Owner();
        using var created = await Send(owner, "POST", "/api/alerts/routines", Wake);
        var id = (await RoutinesIn(created)).Single().GetProperty("id").GetGuid();
        await Send(owner, "DELETE", $"/api/alerts/routines/{id}", null);

        using var response = await Send(owner, "DELETE", $"/api/alerts/routines/{id}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ status

    [Fact]
    public async Task The_status_lists_every_routine_by_hour_minute_then_label()
    {
        using var owner = Owner();
        foreach (var body in new[]
                 {
                     "{\"label\":\"b\",\"hour\":7,\"minute\":0,\"days\":[]}",
                     "{\"label\":\"late\",\"hour\":22,\"minute\":5,\"days\":[]}",
                     "{\"label\":\"a\",\"hour\":7,\"minute\":0,\"days\":[],\"enabled\":false}",
                     "{\"label\":\"early\",\"hour\":6,\"minute\":45,\"days\":[1]}",
                     "{\"label\":\"c\",\"hour\":6,\"minute\":50,\"days\":[]}"
                 })
        {
            await Send(owner, "POST", "/api/alerts/routines", body);
        }

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal(
            ["early", "c", "a", "b", "late"],
            status.GetProperty("routines").EnumerateArray().Select(routine => routine.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task The_status_carries_an_empty_list_before_any_routine()
    {
        using var owner = Owner();

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal(JsonValueKind.Array, status.GetProperty("routines").ValueKind);
        Assert.Empty(status.GetProperty("routines").EnumerateArray());
    }

    [Fact]
    public async Task A_routine_is_never_an_alert_and_rings_through_Pushover_as_an_alarm_titled_with_its_label()
    {
        using var owner = Owner();
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 29);
        await Send(owner, "POST", "/api/alerts/routines", "{\"label\":\"Now\",\"hour\":12,\"minute\":30,\"days\":[1,2,3,4,5,6,7]}");
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 31);

        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.DoesNotContain(
            status.GetProperty("alerts").EnumerateArray(), alert => alert.GetProperty("title").GetString() == "Now");
        var sent = Assert.Single(_pushover.Requests);
        Assert.Equal("Now", sent.Form["title"]);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.StartsWith("Routine alarm, 8:30 AM", sent.Form["message"]);
    }

    // ------------------------------------------------------------------ who may call

    [Fact]
    public async Task A_paired_phone_may_call_all_four_routes()
    {
        using var phone = await PhoneAsync();

        using var created = await Send(phone, "POST", "/api/alerts/routines", Wake);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await RoutinesIn(created)).Single().GetProperty("id").GetGuid();

        using var status = await phone.GetAsync("/api/alerts/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Single(await RoutinesIn(status));

        using var updated = await Send(phone, "PUT", $"/api/alerts/routines/{id}", Whole(label: "Phone"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using var deleted = await Send(phone, "DELETE", $"/api/alerts/routines/{id}", null);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("POST", "/api/alerts/routines", Wake)]
    [InlineData("PUT", "/api/alerts/routines/0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10", Wake)]
    [InlineData("DELETE", "/api/alerts/routines/0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10", null)]
    public async Task No_cookie_and_no_token_is_401(string method, string path, string? body)
    {
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_revoked_phones_token_is_401()
    {
        using var phone = await PhoneAsync();
        var device = Assert.Single(await _devices.ListAsync(CancellationToken.None));
        await _devices.RevokeAsync(device.Id, CancellationToken.None);

        using var response = await Send(phone, "POST", "/api/alerts/routines", Wake);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
