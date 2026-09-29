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
/// The two routes that write to Google Calendar, from the owner's page and a
/// paired phone: an event's type, which adds or removes #critical in the
/// event's description, and a new event, which is listed and alerts before
/// Google's feed carries it. Google is <see cref="FakeGoogleCalendar"/> and
/// the feed is a handler whose answer the test changes.
/// </summary>
public sealed class AlertEventRouteTests : IDisposable
{
    /// <summary>08:00 New York time on Wednesday 28 October 2026.</summary>
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private static readonly string Standing = Event(
        "standup@google.com", "Standup", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]);

    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FakeGoogleCalendar _google = new();
    private readonly RecordingHandler _pushover =
        new(() => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));
    private readonly RecordingHandler _calendar;
    private readonly TestApp _app;
    private string _feed = Ics(Standing);

    public AlertEventRouteTests()
    {
        _calendar = new RecordingHandler(() => RecordingHandler.Text(HttpStatusCode.OK, _feed, "text/calendar"));
        _app = new TestApp(AlertsRouteTests.Settings(), services =>
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
        _google.Seed("standup@google.com", "Dial-in: room 1");
        Worker.ReadNowAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private const string StandupKey = "standup@google.com|20261028T130000Z";

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private async Task<HttpClient> PhoneAsync()
    {
        using var owner = Owner();
        using var paired = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Phone" });
        var token = (await paired.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return phone;
    }

    private static async Task<JsonElement> StateOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static object NewEvent(
        string title = "Dentist",
        string startsAt = "2026-10-28T11:00:00-04:00",
        int durationMinutes = 30,
        string? location = "Main St",
        string type = "alarm",
        int? leadMinutes = 20) =>
        new { title, startsAt, durationMinutes, location, type, leadMinutes };

    [Fact]
    public async Task Alarm_from_the_phone_adds_the_mark_once_in_google_and_default_from_the_page_removes_it()
    {
        using var phone = await PhoneAsync();

        using var first = await phone.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "alarm" });
        using var second = await phone.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "alarm" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var state = await StateOf(second);
        Assert.Equal(JsonValueKind.Null, state.GetProperty("calendarWrite").ValueKind);
        Assert.Equal("alarm", state.GetProperty("alerts")[0].GetProperty("type").GetString());
        Assert.Equal("Dial-in: room 1\n#critical", _google.Description("standup@google.com"));
        Assert.Single(_google.Writes);

        using var owner = Owner();
        using var back = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "default" });
        Assert.Equal(JsonValueKind.Null, (await StateOf(back)).GetProperty("calendarWrite").ValueKind);
        Assert.Equal("Dial-in: room 1", _google.Description("standup@google.com"));
        Assert.Null(await _store.EventTypeAsync("standup@google.com", CancellationToken.None));
    }

    [Fact]
    public async Task Notification_and_none_remove_every_mark_in_any_case()
    {
        _google.Seed("standup@google.com", "Bring #Critical docs\n#critical");
        using var owner = Owner();

        using var response = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "notification" });

        Assert.Equal(JsonValueKind.Null, (await StateOf(response)).GetProperty("calendarWrite").ValueKind);
        Assert.Equal("Bring docs", _google.Description("standup@google.com"));
        Assert.Equal("notification", await _store.EventTypeAsync("standup@google.com", CancellationToken.None));
    }

    public static IEnumerable<object[]> WriteFailures =>
    [
        ["no calendar", CalendarWriteMessages.NotConnected],
        ["no edit access", CalendarWriteMessages.NotConnected],
        ["another calendar", CalendarWriteMessages.OtherCalendar],
        ["sign-in refused", CalendarWriteMessages.SignInRefused],
        ["not found", CalendarWriteMessages.NotFound],
        ["someone else's", CalendarWriteMessages.OrganisedElsewhere],
        ["google refuses", "Google refused the change (HTTP 403)."],
        ["unreachable", CalendarWriteMessages.Unreachable]
    ];

    [Theory]
    [MemberData(nameof(WriteFailures))]
    public async Task A_write_that_does_not_happen_still_stores_the_choice_and_says_why(string failure, string message)
    {
        switch (failure)
        {
            case "no calendar": _google.Grant = null; break;
            case "no edit access": _google.Grant = new CalendarGrant("primary", Feeds.Owner, false); break;
            case "another calendar": _google.Grant = new CalendarGrant("primary", "someone@example.test", true); break;
            case "sign-in refused": _google.TokenRefused = true; break;
            case "not found": _google.Clear(); break;
            case "someone else's": _google.Seed("standup@google.com", organisedHere: false); break;
            case "google refuses": _google.Refusing = (HttpStatusCode.Forbidden, "insufficientPermissions"); break;
            case "unreachable": _google.Unreachable = true; break;
        }

        using var owner = Owner();
        using var response = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "alarm" });
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(message, JsonDocument.Parse(text).RootElement.GetProperty("calendarWrite").GetString());
        Assert.Equal("alarm", await _store.EventTypeAsync("standup@google.com", CancellationToken.None));
        Assert.Empty(_google.Writes);
        Assert.DoesNotContain("development-token", text);
        Assert.DoesNotContain("feedsecret", text);
        Assert.DoesNotContain("googleapis", text);
    }

    [Fact]
    public async Task The_status_never_carries_a_calendar_write_message()
    {
        _google.Grant = null;
        using var owner = Owner();
        using var set = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "alarm" });
        Assert.Equal(CalendarWriteMessages.NotConnected, (await StateOf(set)).GetProperty("calendarWrite").GetString());

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal(JsonValueKind.Null, status.GetProperty("calendarWrite").ValueKind);
    }

    [Fact]
    public async Task A_new_event_from_the_phone_is_listed_at_once_with_its_type_before_the_feed_has_it()
    {
        using var phone = await PhoneAsync();

        using var response = await phone.PostAsJsonAsync("/api/alerts/events", NewEvent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var alerts = (await StateOf(response)).GetProperty("alerts").EnumerateArray().ToList();
        var dentist = alerts.Single(alert => alert.GetProperty("title").GetString() == "Dentist");
        var uid = _google.Inserted.Single();
        Assert.Equal($"{uid}|20261028T150000Z", dentist.GetProperty("key").GetString());
        Assert.Equal("Main St", dentist.GetProperty("location").GetString());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 15, 0).ToDateTimeOffset(), dentist.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 14, 40).ToDateTimeOffset(), dentist.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal("reminder", dentist.GetProperty("source").GetString());
        Assert.Equal("alarm", dentist.GetProperty("type").GetString());
        Assert.Equal("set", dentist.GetProperty("typeFrom").GetString());
        Assert.True(dentist.GetProperty("critical").GetBoolean());
        Assert.Equal(["Standup", "Dentist"], alerts.Select(alert => alert.GetProperty("title").GetString()));

        var insert = Assert.Single(_google.Writes);
        Assert.Equal("#critical", insert.Description);
        Assert.Equal(20, insert.PopupMinutes);
        Assert.Equal("alarm", await _store.EventTypeAsync(uid, CancellationToken.None));

        // Skip, test and acknowledge find it like any listed alert.
        using var owner = Owner();
        using var skip = await owner.PostAsJsonAsync("/api/alerts/skip", new { key = $"{uid}|20261028T150000Z" });
        Assert.Equal(HttpStatusCode.OK, skip.StatusCode);
        using var test = await owner.PostAsJsonAsync("/api/alerts/test-event", new { key = $"{uid}|20261028T150000Z" });
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        using var ack = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = $"{uid}|20261028T150000Z", via = "phone" });
        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
    }

    [Fact]
    public async Task Without_a_lead_the_new_event_takes_the_saved_default_lead()
    {
        using var owner = Owner();
        await _store.SaveSettingsAsync(
            AlertSettings.Defaults(new AlertsOptions()) with { DefaultLeadMinutes = 25 }, Eight, CancellationToken.None);

        using var response = await owner.PostAsJsonAsync(
            "/api/alerts/events",
            new { title = "Lunch", startsAt = "2026-10-28T16:00:00Z", durationMinutes = 60, type = "notification" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(25, _google.Writes.Single().PopupMinutes);
        Assert.Null(_google.Writes.Single().Description);
        var lunch = (await StateOf(response)).GetProperty("alerts").EnumerateArray()
            .Single(alert => alert.GetProperty("title").GetString() == "Lunch");
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 15, 35).ToDateTimeOffset(), lunch.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal("notification", lunch.GetProperty("type").GetString());
        Assert.False(lunch.GetProperty("critical").GetBoolean());
    }

    [Fact]
    public async Task The_stored_event_gives_way_once_the_feed_carries_its_uid_under_the_same_key()
    {
        using var owner = Owner();
        using var created = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());
        var uid = _google.Inserted.Single();
        using var skip = await owner.PostAsJsonAsync("/api/alerts/skip", new { key = $"{uid}|20261028T150000Z" });
        Assert.Single(await _store.CreatedEventsAsync(CancellationToken.None));

        // Google's feed catches up, with the reminder the event was made with.
        _feed = Ics(Standing, Event(uid, "Dentist #critical", "20261028T110000", "20261028T113000", "Main St", alarms: [Popup("-PT20M")]));
        await Worker.ReadNowAsync(CancellationToken.None);

        Assert.Empty(await _store.CreatedEventsAsync(CancellationToken.None));
        var alerts = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray().ToList();
        var dentist = Assert.Single(alerts, alert => alert.GetProperty("title").GetString() == "Dentist");
        Assert.Equal($"{uid}|20261028T150000Z", dentist.GetProperty("key").GetString());
        Assert.True(dentist.GetProperty("skipped").GetBoolean());
        Assert.Equal("alarm", dentist.GetProperty("type").GetString());
    }

    [Fact]
    public async Task The_stored_event_is_dropped_once_it_starts_even_if_the_feed_never_has_it()
    {
        using var owner = Owner();
        using var created = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());

        _clock.Now = Instant.FromUtc(2026, 10, 28, 15, 0);
        await Worker.ReadNowAsync(CancellationToken.None);

        Assert.Empty(await _store.CreatedEventsAsync(CancellationToken.None));
        var alerts = (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray();
        Assert.DoesNotContain(alerts, alert => alert.GetProperty("title").GetString() == "Dentist");
    }

    [Fact]
    public async Task A_second_replica_lists_the_new_event_from_the_database()
    {
        using var owner = Owner();
        using var created = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());

        // Another process: its own status in memory, the same database.
        var other = new AlertsStatus(new AlertsOptions());
        other.Planned(AlertPlanner.Plan(_feed, Eight, AlertSettings.Defaults(new AlertsOptions())), Eight);
        var plan = await AlertsPlan.CurrentAsync(
            other.Snapshot(), _store, AlertSettings.Defaults(new AlertsOptions()), Eight, CancellationToken.None);

        Assert.Contains(plan, alert => alert.Title == "Dentist" && alert.EventId == _google.Inserted.Single());
    }

    public static IEnumerable<object?[]> BadNewEvents =>
    [
        ["title", "{\"title\":\"\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["title", "{\"title\":\"   \",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["title", "{\"title\":\"" + new string('t', 201) + "\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["title", "{\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["startsAt", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["startsAt", "{\"title\":\"A\",\"startsAt\":\"tomorrow\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["startsAt", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T12:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["startsAt", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T07:00:00-04:00\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["startsAt", "{\"title\":\"A\",\"startsAt\":\"2027-10-29T12:00:01Z\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["startsAt", "{\"title\":\"A\",\"durationMinutes\":30,\"type\":\"alarm\"}"],
        ["durationMinutes", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":4,\"type\":\"alarm\"}"],
        ["durationMinutes", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":1441,\"type\":\"alarm\"}"],
        ["durationMinutes", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"type\":\"alarm\"}"],
        ["location", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"location\":\"" + new string('l', 201) + "\",\"type\":\"alarm\"}"],
        ["type", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"default\"}"],
        ["type", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"ALARM\"}"],
        ["type", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30}"],
        ["leadMinutes", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\",\"leadMinutes\":-1}"],
        ["leadMinutes", "{\"title\":\"A\",\"startsAt\":\"2026-10-28T15:00:00Z\",\"durationMinutes\":30,\"type\":\"alarm\",\"leadMinutes\":1441}"]
    ];

    [Theory]
    [MemberData(nameof(BadNewEvents))]
    public async Task A_new_event_outside_its_bounds_is_refused_by_field_and_nothing_is_written(string field, string body)
    {
        using var owner = Owner();

        using var response = await owner.PostAsync("/api/alerts/events", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Single(errors.EnumerateObject());
        Assert.Equal(0, _google.Calls);
        Assert.Empty(await _store.CreatedEventsAsync(CancellationToken.None));
    }

    public static IEnumerable<object?[]> EdgesAccepted =>
    [
        ["{\"title\":\"A\",\"startsAt\":\"2026-10-28T12:00:01Z\",\"durationMinutes\":5,\"type\":\"none\",\"leadMinutes\":0}"],
        ["{\"title\":\"" + new string('t', 200) + "\",\"startsAt\":\"2027-10-29T12:00:00Z\",\"durationMinutes\":1440,\"location\":\"" + new string('l', 200) + "\",\"type\":\"notification\",\"leadMinutes\":1440}"],
        ["{\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00.250-04:00\",\"durationMinutes\":30,\"location\":\"  \",\"type\":\"alarm\"}"]
    ];

    [Theory]
    [MemberData(nameof(EdgesAccepted))]
    public async Task Every_bound_itself_is_accepted(string body)
    {
        using var owner = Owner();

        using var response = await owner.PostAsync("/api/alerts/events", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await _store.CreatedEventsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task No_calendar_with_edit_access_is_a_conflict_that_says_so_and_nothing_is_kept()
    {
        _google.Grant = new CalendarGrant("primary", Feeds.Owner, false);
        using var owner = Owner();

        using var response = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(CalendarWriteMessages.NotConnected, (await StateOf(response)).GetProperty("detail").GetString());
        Assert.Empty(await _store.CreatedEventsAsync(CancellationToken.None));
        Assert.Empty(await _store.EventTypesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_connected_calendar_that_is_not_the_feeds_is_a_conflict_that_says_so()
    {
        _google.Grant = new CalendarGrant("primary", "someone@example.test", true);
        using var owner = Owner();

        using var response = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(CalendarWriteMessages.OtherCalendar, (await StateOf(response)).GetProperty("detail").GetString());
        Assert.Equal(0, _google.Calls);
    }

    [Fact]
    public async Task Google_refusing_the_new_event_is_a_bad_gateway_with_a_short_reason()
    {
        _google.Refusing = (HttpStatusCode.BadRequest, null);
        using var owner = Owner();

        using var response = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Google refused the change (HTTP 400).", JsonDocument.Parse(text).RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("development-token", text);
        Assert.Empty(await _store.CreatedEventsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_new_event_shares_the_actions_limit()
    {
        using var owner = Owner();
        for (var press = 0; press < AlertsEndpoints.DefaultActionsPerMinute; press++)
        {
            using var ok = await owner.PostAsync("/api/alerts/unmute", null);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var refused = await owner.PostAsJsonAsync("/api/alerts/events", NewEvent());

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(0, _google.Calls);
    }

    public void Dispose() => _app.Dispose();
}
