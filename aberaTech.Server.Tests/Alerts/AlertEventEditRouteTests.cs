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
/// Editing and deleting a listed event, from the owner's page and a paired
/// phone: one occurrence, or every occurrence of a series. Google is
/// <see cref="FakeGoogleCalendar"/> and the feed is a handler whose answer
/// the test changes, so a test can hold the feed stale or let it catch up.
/// </summary>
public sealed class AlertEventEditRouteTests : IDisposable
{
    /// <summary>08:00 New York time on Wednesday 28 October 2026.</summary>
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private const string StandupKey = "standup@google.com|20261028T130000Z";

    private const string DailyToday = "daily@google.com|20261028T140000Z";

    private const string DailyTomorrow = "daily@google.com|20261029T140000Z";

    private static string Standup(string summary = "Standup") => Event(
        "standup@google.com", summary, "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")]);

    /// <summary>10:00 to 10:30 New York time every day for a week from 28 October.</summary>
    private static string Daily(params string[] extra) => Event(
        "daily@google.com", "Daily", "20261028T100000", "20261028T103000", "Room 2",
        extra: ["RRULE:FREQ=DAILY;COUNT=7", .. extra], alarms: [Popup("-PT10M")]);

    private readonly FakeClock _clock = new(Eight);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FakeGoogleCalendar _google = new();
    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\",\"receipt\":\"rcpt30lettersanddigits0000000a\"}", "application/json"));
    private readonly RecordingHandler _calendar;
    private readonly TestApp _app;
    private string _feed = Ics(Standup(), Daily());

    public AlertEventEditRouteTests()
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
        _google.Seed("standup@google.com", "Dial-in: room 1", summary: "Standup");
        _google.Seed("daily@google.com", recurring: true, start: Instant.FromUtc(2026, 10, 28, 14, 0), summary: "Daily");
        Read();
    }

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private void Read() => Worker.ReadNowAsync(CancellationToken.None).GetAwaiter().GetResult();

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

    private Task<long> VersionAsync() => _devices.PlanVersionAsync(CancellationToken.None);

    private static Task<HttpResponseMessage> Put(HttpClient client, string json) =>
        client.PutAsync("/api/alerts/events", new StringContent(json, Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> Delete(HttpClient client, string key, string scope) =>
        client.PostAsJsonAsync("/api/alerts/events/delete", new { key, scope });

    private static async Task<List<JsonElement>> AlertsOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alerts").EnumerateArray().ToList();

    private async Task<List<JsonElement>> StatusAlerts()
    {
        using var owner = Owner();
        return (await owner.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("alerts").EnumerateArray().ToList();
    }

    private static JsonElement ByKey(List<JsonElement> alerts, string key) =>
        Assert.Single(alerts, alert => alert.GetProperty("key").GetString() == key);

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        Instant.FromUtc(2026, month, day, hour, minute).ToDateTimeOffset();

    private IEnumerable<RecordingHandler.Seen> Sends =>
        _pushover.Requests.Where(request => request.Url.AbsolutePath == "/1/messages.json");

    private IEnumerable<RecordingHandler.Seen> Cancels =>
        _pushover.Requests.Where(request => request.Url.AbsolutePath.StartsWith("/1/receipts/", StringComparison.Ordinal));

    // ----- What the list carries -----

    [Fact]
    public async Task Each_listed_occurrence_says_whether_it_repeats_and_when_it_ends()
    {
        var alerts = await StatusAlerts();

        var standup = ByKey(alerts, StandupKey);
        Assert.False(standup.GetProperty("recurring").GetBoolean());
        Assert.Equal(At(10, 28, 13, 30), standup.GetProperty("endsAt").GetDateTimeOffset());
        var daily = ByKey(alerts, DailyTomorrow);
        Assert.True(daily.GetProperty("recurring").GetBoolean());
        Assert.Equal(At(10, 29, 14, 30), daily.GetProperty("endsAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_moved_occurrence_of_a_series_is_recurring_and_an_event_with_no_end_has_none()
    {
        var moved = Event(
            "daily@google.com", "Daily moved", "20261029T120000", "20261029T123000",
            extra: ["RECURRENCE-ID;TZID=America/New_York:20261029T100000"]);
        var open = string.Join("\r\n",
            "BEGIN:VEVENT", "DTSTART;TZID=America/New_York:20261028T160000", "UID:open@google.com", "SUMMARY:Open", "END:VEVENT");
        _feed = Ics(Standup(), Daily(), moved, open);
        Read();

        var alerts = await StatusAlerts();

        Assert.True(ByKey(alerts, "daily@google.com|20261029T160000Z").GetProperty("recurring").GetBoolean());
        var single = ByKey(alerts, "open@google.com|20261028T200000Z");
        Assert.False(single.GetProperty("recurring").GetBoolean());
        Assert.Equal(JsonValueKind.Null, single.GetProperty("endsAt").ValueKind);
    }

    [Fact]
    public async Task A_created_event_is_not_recurring_and_carries_its_end()
    {
        using var owner = Owner();
        using var created = await owner.PostAsJsonAsync(
            "/api/alerts/events",
            new { title = "Dentist", startsAt = "2026-10-28T11:00:00-04:00", durationMinutes = 45, type = "alarm" });

        var dentist = Assert.Single(await AlertsOf(created), alert => alert.GetProperty("title").GetString() == "Dentist");
        Assert.False(dentist.GetProperty("recurring").GetBoolean());
        Assert.Equal(At(10, 28, 15, 45), dentist.GetProperty("endsAt").GetDateTimeOffset());
    }

    // ----- Edit -----

    [Fact]
    public async Task An_occurrence_edit_of_a_single_event_patches_the_event_and_is_listed_before_the_feed_has_it()
    {
        using var phone = await PhoneAsync();
        var before = await VersionAsync();

        using var response = await Put(phone, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup moved",
             "startsAt":"2026-10-28T09:30:00-04:00","durationMinutes":45,"location":"Room 9"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alerts = await AlertsOf(response);
        Assert.DoesNotContain(alerts, alert => alert.GetProperty("key").GetString() == StandupKey);
        var moved = ByKey(alerts, "standup@google.com|20261028T133000Z");
        Assert.Equal("Standup moved", moved.GetProperty("title").GetString());
        Assert.Equal("Room 9", moved.GetProperty("location").GetString());
        Assert.Equal(At(10, 28, 13, 30), moved.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(At(10, 28, 14, 15), moved.GetProperty("endsAt").GetDateTimeOffset());
        // The reminder is left alone: still 15 minutes before.
        Assert.Equal(At(10, 28, 13, 15), moved.GetProperty("alertAt").GetDateTimeOffset());

        var write = Assert.Single(_google.Writes);
        Assert.Equal("patch-master", write.Kind);
        Assert.Equal("Standup moved", write.Summary);
        Assert.Equal("2026-10-28T09:30:00-04:00", write.Start);
        Assert.Equal("2026-10-28T10:15:00-04:00", write.End);
        Assert.Empty(_google.Lookups);
        Assert.Equal(before + 1, await VersionAsync());

        // Still so after a reload, while the feed is stale.
        Read();
        Assert.Equal("Standup moved", ByKey(await StatusAlerts(), "standup@google.com|20261028T133000Z").GetProperty("title").GetString());
        Assert.Single(await _store.EventChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_edit_leaves_the_description_the_mark_and_the_stored_type_alone()
    {
        using var owner = Owner();
        using var alarm = await owner.PutAsJsonAsync("/api/alerts/event-type", new { key = StandupKey, type = "alarm" });
        Assert.Equal("Dial-in: room 1\n#critical", _google.Description("standup@google.com"));

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:10:00-04:00"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(_google.Writes[^1].Body!).RootElement;
        Assert.False(body.TryGetProperty("description", out _));
        Assert.False(body.TryGetProperty("reminders", out _));
        Assert.False(body.TryGetProperty("location", out _));
        Assert.Equal("Dial-in: room 1\n#critical", _google.Description("standup@google.com"));
        Assert.Equal("alarm", await _store.EventTypeAsync("standup@google.com", CancellationToken.None));
        var moved = ByKey(await AlertsOf(response), "standup@google.com|20261028T131000Z");
        Assert.Equal("alarm", moved.GetProperty("type").GetString());
        Assert.Equal("set", moved.GetProperty("typeFrom").GetString());
        // No location in the body leaves the event's own.
        Assert.Equal("Room 1", moved.GetProperty("location").GetString());
    }

    [Fact]
    public async Task A_critical_event_stays_critical_after_an_edit()
    {
        _feed = Ics(Standup("Standup #critical"), Daily());
        Read();
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:20:00-04:00"}
            """);

        var moved = ByKey(await AlertsOf(response), "standup@google.com|20261028T132000Z");
        Assert.True(moved.GetProperty("critical").GetBoolean());
        Assert.Equal("alarm", moved.GetProperty("type").GetString());
        Assert.Equal("critical", moved.GetProperty("typeFrom").GetString());
    }

    [Fact]
    public async Task Without_a_duration_the_edit_keeps_the_length_and_the_end_moves_with_the_start()
    {
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T11:00:00-04:00","durationMinutes":null}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = ByKey(await AlertsOf(response), "standup@google.com|20261028T150000Z");
        Assert.Equal(At(10, 28, 15, 30), moved.GetProperty("endsAt").GetDateTimeOffset());
        Assert.Equal("2026-10-28T11:30:00-04:00", _google.Writes[^1].End);
    }

    [Fact]
    public async Task A_lead_sets_one_popup_reminder_and_the_alert_follows_it()
    {
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:00:00-04:00","leadMinutes":40}
            """);

        var reminders = JsonDocument.Parse(_google.Writes[^1].Body!).RootElement.GetProperty("reminders");
        Assert.False(reminders.GetProperty("useDefault").GetBoolean());
        var popup = Assert.Single(reminders.GetProperty("overrides").EnumerateArray());
        Assert.Equal("popup", popup.GetProperty("method").GetString());
        Assert.Equal(40, popup.GetProperty("minutes").GetInt32());
        Assert.Equal(40, _google.Writes[^1].PopupMinutes);
        var same = ByKey(await AlertsOf(response), StandupKey);
        Assert.Equal(At(10, 28, 12, 20), same.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal("reminder", same.GetProperty("source").GetString());
    }

    [Fact]
    public async Task A_null_location_clears_it()
    {
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:00:00-04:00","location":null}
            """);

        Assert.Equal("", JsonDocument.Parse(_google.Writes[^1].Body!).RootElement.GetProperty("location").GetString());
        Assert.Equal(JsonValueKind.Null, ByKey(await AlertsOf(response), StandupKey).GetProperty("location").ValueKind);
        Assert.Null(_google.Master("standup@google.com")!.Value.Location);
    }

    [Fact]
    public async Task An_occurrence_edit_of_a_series_patches_that_instance_alone()
    {
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"daily@google.com|20261029T140000Z","scope":"occurrence","title":"Daily late","startsAt":"2026-10-29T11:00:00-04:00"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lookup = Assert.Single(_google.Lookups);
        Assert.Equal("2026-10-29T14:00:00Z", lookup.TimeMin);
        var write = Assert.Single(_google.Writes);
        Assert.Equal("patch-instance", write.Kind);
        Assert.EndsWith("_20261029T140000Z", write.Target);
        Assert.Equal("2026-10-29T11:00:00-04:00", write.Start);
        Assert.Equal("2026-10-29T11:30:00-04:00", write.End);
        // The series is untouched in Google and on the list.
        Assert.Equal(("Daily", Instant.FromUtc(2026, 10, 28, 14, 0)), (_google.Master("daily@google.com")!.Value.Summary, _google.Master("daily@google.com")!.Value.Start));
        var alerts = await AlertsOf(response);
        Assert.Equal("Daily", ByKey(alerts, DailyToday).GetProperty("title").GetString());
        Assert.Equal("Daily late", ByKey(alerts, "daily@google.com|20261029T150000Z").GetProperty("title").GetString());
        Assert.DoesNotContain(alerts, alert => alert.GetProperty("key").GetString() == DailyTomorrow);
    }

    [Fact]
    public async Task A_series_edit_moves_the_master_by_the_change_and_keeps_the_rule()
    {
        using var phone = await PhoneAsync();

        using var response = await Put(phone, """
            {"key":"daily@google.com|20261029T140000Z","scope":"series","title":"Daily sync","startsAt":"2026-10-29T11:30:00-04:00"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_google.Lookups);
        var write = Assert.Single(_google.Writes);
        Assert.Equal("patch-master", write.Kind);
        Assert.Equal("2026-10-28T11:30:00-04:00", write.Start);
        Assert.Equal("2026-10-28T12:00:00-04:00", write.End);
        Assert.Equal("America/New_York", write.TimeZone);
        var body = JsonDocument.Parse(write.Body!).RootElement;
        Assert.False(body.TryGetProperty("recurrence", out _));
        Assert.False(body.TryGetProperty("description", out _));

        var alerts = await AlertsOf(response);
        var daily = alerts.Where(alert => alert.GetProperty("title").GetString() == "Daily sync").ToList();
        Assert.Equal(
            [At(10, 28, 15, 30), At(10, 29, 15, 30)],
            daily.Select(alert => alert.GetProperty("startsAt").GetDateTimeOffset()));
        Assert.All(daily, alert => Assert.True(alert.GetProperty("recurring").GetBoolean()));
        Assert.DoesNotContain(alerts, alert => alert.GetProperty("title").GetString() == "Daily");
    }

    [Fact]
    public async Task A_series_edit_across_a_date_change_and_the_end_of_daylight_time_keeps_the_wall_clock()
    {
        // A daily 09:00 from Friday 30 October. Daylight time ends at 02:00
        // on Sunday 1 November. At 08:00 on Saturday 31 October the list has
        // Saturday's 09:00 EDT and Sunday's 09:00 EST.
        _feed = Ics(Event(
            "late@google.com", "Late", "20261030T090000", "20261030T093000",
            extra: ["RRULE:FREQ=DAILY;COUNT=7"], alarms: [Popup("-PT10M")]));
        _google.Seed("late@google.com", recurring: true, start: Instant.FromUtc(2026, 10, 30, 13, 0), summary: "Late");
        _clock.Now = Instant.FromUtc(2026, 10, 31, 12, 0);
        Read();
        using var owner = Owner();

        // Saturday 09:00 EDT to Sunday 10:00 EST: a day and an hour on the
        // wall clock, 26 hours of elapsed time.
        using var response = await Put(owner, """
            {"key":"late@google.com|20261031T130000Z","scope":"series","title":"Late","startsAt":"2026-11-01T10:00:00-05:00"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var write = Assert.Single(_google.Writes);
        // Friday 09:00 EDT plus a day and an hour is Saturday 10:00 EDT, not 11:00.
        Assert.Equal("2026-10-31T10:00:00-04:00", write.Start);
        Assert.Equal("2026-10-31T10:30:00-04:00", write.End);
        Assert.Equal(Instant.FromUtc(2026, 10, 31, 14, 0), _google.Master("late@google.com")!.Value.Start);
        var starts = (await AlertsOf(response)).Select(alert => alert.GetProperty("startsAt").GetDateTimeOffset()).ToList();
        Assert.Equal([At(11, 1, 15), At(11, 2, 15)], starts);
    }

    [Fact]
    public async Task A_series_edit_with_a_duration_sets_every_length_and_one_without_keeps_the_masters()
    {
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"daily@google.com|20261028T140000Z","scope":"series","title":"Daily","startsAt":"2026-10-28T10:00:00-04:00","durationMinutes":60}
            """);

        Assert.Equal("2026-10-28T11:00:00-04:00", _google.Writes[^1].End);
        Assert.All(
            (await AlertsOf(response)).Where(alert => alert.GetProperty("title").GetString() == "Daily"),
            alert => Assert.Equal(
                alert.GetProperty("startsAt").GetDateTimeOffset().AddHours(1), alert.GetProperty("endsAt").GetDateTimeOffset()));
    }

    [Fact]
    public async Task Series_on_an_event_that_does_not_repeat_edits_the_event()
    {
        using var owner = Owner();

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"series","title":"Standup","startsAt":"2026-10-28T09:05:00-04:00"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("patch-master", Assert.Single(_google.Writes).Kind);
        Assert.False(Assert.Single(await _store.EventChangesAsync(CancellationToken.None)).Series);
    }

    [Fact]
    public async Task The_pending_edit_is_dropped_once_the_feed_shows_it_and_the_moved_occurrence_is_listed_once()
    {
        using var owner = Owner();
        using var response = await Put(owner, """
            {"key":"daily@google.com|20261029T140000Z","scope":"series","title":"Daily sync","startsAt":"2026-10-29T11:30:00-04:00"}
            """);

        // Google's feed catches up: the series at 11:30.
        _feed = Ics(Standup(), Event(
            "daily@google.com", "Daily sync", "20261028T113000", "20261028T120000", "Room 2",
            extra: ["RRULE:FREQ=DAILY;COUNT=7"], alarms: [Popup("-PT10M")]));
        Read();

        Assert.Empty(await _store.EventChangesAsync(CancellationToken.None));
        var daily = (await StatusAlerts()).Where(alert => alert.GetProperty("title").GetString() == "Daily sync").ToList();
        // Moved once, never twice.
        Assert.Equal([At(10, 28, 15, 30), At(10, 29, 15, 30)], daily.Select(alert => alert.GetProperty("startsAt").GetDateTimeOffset()));
    }

    [Fact]
    public async Task The_pending_edit_is_dropped_once_both_starts_have_passed()
    {
        using var owner = Owner();
        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:30:00-04:00"}
            """);

        _clock.Now = Instant.FromUtc(2026, 10, 28, 13, 15);
        Read();
        Assert.Single(await _store.EventChangesAsync(CancellationToken.None));

        _clock.Now = Instant.FromUtc(2026, 10, 28, 13, 30);
        Read();
        Assert.Empty(await _store.EventChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_skip_goes_with_the_occurrence_when_its_start_moves()
    {
        using var owner = Owner();
        using var skip = await owner.PostAsJsonAsync("/api/alerts/skip", new { key = StandupKey });

        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:30:00-04:00"}
            """);

        Assert.True(ByKey(await AlertsOf(response), "standup@google.com|20261028T133000Z").GetProperty("skipped").GetBoolean());
    }

    [Fact]
    public async Task A_start_moved_after_its_alert_went_does_not_send_it_again()
    {
        _feed = Ics(Standup("Standup #critical"), Daily());
        Read();
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 50);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Sends);

        using var owner = Owner();
        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:05:00-04:00"}
            """);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Worker.TickAsync(CancellationToken.None);
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 55);
        await Worker.TickAsync(CancellationToken.None);

        // The feed catches up, and the alert still went once.
        _feed = Ics(Event("standup@google.com", "Standup #critical", "20261028T090500", "20261028T093500", "Room 1", alarms: [Popup("-PT15M")]), Daily());
        Read();
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Sends);
    }

    [Fact]
    public async Task A_start_moved_before_its_alert_sends_once_at_the_new_time_and_never_at_the_old()
    {
        _feed = Ics(Standup("Standup #critical"), Daily());
        Read();
        using var owner = Owner();
        using var response = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T10:00:00-04:00"}
            """);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 46);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Empty(Sends);

        _clock.Now = Instant.FromUtc(2026, 10, 28, 13, 46);
        await Worker.TickAsync(CancellationToken.None);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Sends);
        Assert.NotNull(await _store.DeliveryAsync("standup@google.com|20261028T140000Z", CancellationToken.None));
        Assert.Null(await _store.DeliveryAsync(StandupKey, CancellationToken.None));
    }

    [Fact]
    public async Task A_created_event_still_pending_takes_the_edit_in_its_row()
    {
        using var owner = Owner();
        using var created = await owner.PostAsJsonAsync(
            "/api/alerts/events",
            new { title = "Dentist", startsAt = "2026-10-28T11:00:00-04:00", durationMinutes = 30, type = "alarm" });
        var uid = _google.Inserted.Single();

        using var response = await Put(owner, $$"""
            {"key":"{{uid}}|20261028T150000Z","scope":"occurrence","title":"Dentist moved","startsAt":"2026-10-28T12:00:00-04:00"}
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(await _store.CreatedEventsAsync(CancellationToken.None));
        Assert.Equal(("Dentist moved", Instant.FromUtc(2026, 10, 28, 16, 0), Instant.FromUtc(2026, 10, 28, 16, 30)), (row.Title, row.StartsAt, row.EndsAt));
        var moved = Assert.Single(await AlertsOf(response), alert => alert.GetProperty("title").GetString() == "Dentist moved");
        Assert.Equal($"{uid}|20261028T160000Z", moved.GetProperty("key").GetString());
    }

    // ----- Delete -----

    [Fact]
    public async Task Deleting_one_occurrence_of_a_series_cancels_that_instance_alone()
    {
        using var phone = await PhoneAsync();
        var before = await VersionAsync();

        using var response = await Delete(phone, DailyTomorrow, "occurrence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alerts = await AlertsOf(response);
        Assert.DoesNotContain(alerts, alert => alert.GetProperty("key").GetString() == DailyTomorrow);
        ByKey(alerts, DailyToday);
        var write = Assert.Single(_google.Writes);
        Assert.Equal("delete-instance", write.Kind);
        Assert.EndsWith("_20261029T140000Z", write.Target);
        Assert.Single(_google.Cancelled("daily@google.com"));
        Assert.NotNull(_google.Master("daily@google.com"));
        Assert.Equal(before + 1, await VersionAsync());

        // Kept out while the feed still lists it.
        Read();
        Assert.DoesNotContain(await StatusAlerts(), alert => alert.GetProperty("key").GetString() == DailyTomorrow);
        Assert.Single(await _store.EventChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_a_series_deletes_the_master_and_every_occurrence_goes()
    {
        using var owner = Owner();

        using var response = await Delete(owner, DailyTomorrow, "series");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(await AlertsOf(response), alert => alert.GetProperty("title").GetString() == "Daily");
        Assert.Equal("delete-master", Assert.Single(_google.Writes).Kind);
        Assert.Null(_google.Master("daily@google.com"));
        Assert.Empty(_google.Lookups);
    }

    [Fact]
    public async Task Deleting_a_single_event_deletes_it()
    {
        using var owner = Owner();

        using var response = await Delete(owner, StandupKey, "occurrence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(await AlertsOf(response), alert => alert.GetProperty("title").GetString() == "Standup");
        Assert.Equal("delete-master", Assert.Single(_google.Writes).Kind);
    }

    [Fact]
    public async Task A_deleted_alert_is_never_sent()
    {
        _feed = Ics(Standup("Standup #critical"), Daily());
        Read();
        using var owner = Owner();
        using var response = await Delete(owner, StandupKey, "occurrence");

        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 46);
        Read();
        await Worker.TickAsync(CancellationToken.None);

        Assert.Empty(Sends);
        Assert.Null(await _store.DeliveryAsync(StandupKey, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_a_ringing_alarm_cancels_its_repeats()
    {
        _feed = Ics(Standup("Standup #critical"), Daily());
        Read();
        _clock.Now = Instant.FromUtc(2026, 10, 28, 12, 46);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Sends);
        Assert.Empty(Cancels);

        using var owner = Owner();
        using var response = await Delete(owner, StandupKey, "occurrence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancel = Assert.Single(Cancels);
        Assert.Contains("rcpt30lettersanddigits0000000a", cancel.Url.AbsolutePath);
    }

    [Fact]
    public async Task Deleting_a_created_event_the_feed_does_not_have_forgets_its_row_too()
    {
        using var owner = Owner();
        using var created = await owner.PostAsJsonAsync(
            "/api/alerts/events",
            new { title = "Dentist", startsAt = "2026-10-28T11:00:00-04:00", durationMinutes = 30, type = "alarm" });
        var uid = _google.Inserted.Single();

        using var response = await Delete(owner, $"{uid}|20261028T150000Z", "occurrence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await _store.CreatedEventsAsync(CancellationToken.None));
        Assert.DoesNotContain(await AlertsOf(response), alert => alert.GetProperty("title").GetString() == "Dentist");
        Assert.Equal(["insert", "delete-master"], _google.Writes.Select(write => write.Kind));
    }

    [Fact]
    public async Task The_pending_deletion_is_dropped_once_the_feed_no_longer_lists_the_occurrence()
    {
        using var owner = Owner();
        using var response = await Delete(owner, DailyTomorrow, "occurrence");

        _feed = Ics(Standup(), Daily("EXDATE;TZID=America/New_York:20261029T100000"));
        Read();

        Assert.Empty(await _store.EventChangesAsync(CancellationToken.None));
        var alerts = await StatusAlerts();
        Assert.DoesNotContain(alerts, alert => alert.GetProperty("key").GetString() == DailyTomorrow);
        ByKey(alerts, DailyToday);
    }

    [Fact]
    public async Task A_deletion_made_on_an_edited_occurrence_finds_it_at_its_new_start()
    {
        using var owner = Owner();
        using var edit = await Put(owner, """
            {"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"Standup","startsAt":"2026-10-28T09:30:00-04:00"}
            """);

        using var response = await Delete(owner, "standup@google.com|20261028T133000Z", "occurrence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Read();
        Assert.DoesNotContain(await StatusAlerts(), alert => alert.GetProperty("title").GetString() == "Standup");
    }

    // ----- Refusals -----

    [Fact]
    public async Task Without_a_sign_in_both_routes_are_401()
    {
        using var anonymous = _app.CreateClient();

        using var edit = await Put(anonymous, """{"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"A","startsAt":"2026-10-28T09:30:00-04:00"}""");
        using var delete = await Delete(anonymous, StandupKey, "occurrence");

        Assert.Equal(HttpStatusCode.Unauthorized, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
        Assert.Equal(0, _google.Calls);
    }

    [Fact]
    public async Task A_key_not_on_the_list_is_404_and_nothing_is_written_or_pushed()
    {
        using var owner = Owner();
        var before = await VersionAsync();

        using var edit = await Put(owner, """{"key":"nobody@google.com|20261028T130000Z","scope":"occurrence","title":"A","startsAt":"2026-10-28T09:30:00-04:00"}""");
        using var delete = await Delete(owner, "nobody@google.com|20261028T130000Z", "series");

        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(0, _google.Calls);
        Assert.Equal(before, await VersionAsync());
        Assert.Empty(await _store.EventChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_key_is_read_from_the_body_alone()
    {
        using var owner = Owner();
        var key = Uri.EscapeDataString(StandupKey);

        using var edit = await owner.PutAsync(
            $"/api/alerts/events?key={key}",
            new StringContent("""{"scope":"occurrence","title":"A","startsAt":"2026-10-28T09:30:00-04:00"}""", Encoding.UTF8, "application/json"));
        using var delete = await owner.PostAsync(
            $"/api/alerts/events/delete?key={key}",
            new StringContent("""{"scope":"occurrence"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.True((await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("key", out _));
        Assert.True((await delete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("key", out _));
        Assert.Equal(0, _google.Calls);
    }

    private const string Good = "\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"";

    public static IEnumerable<object[]> BadEdits =>
    [
        ["key", "{\"scope\":\"occurrence\",\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["key", "{\"key\":\"" + new string('k', 201) + "\",\"scope\":\"occurrence\",\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["scope", "{\"key\":\"standup@google.com|20261028T130000Z\",\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["scope", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"all\",\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["title", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\" \",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["title", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"" + new string('t', 201) + "\",\"startsAt\":\"2026-10-28T09:30:00-04:00\"}"],
        ["startsAt", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"A\",\"startsAt\":\"2026-10-28T09:30:00\"}"],
        ["startsAt", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"A\",\"startsAt\":\"2026-10-28T07:59:00-04:00\"}"],
        ["startsAt", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"A\",\"startsAt\":\"2027-10-29T12:00:01Z\"}"],
        ["startsAt", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"occurrence\",\"title\":\"A\"}"],
        ["durationMinutes", "{" + Good + ",\"durationMinutes\":4}"],
        ["durationMinutes", "{" + Good + ",\"durationMinutes\":1441}"],
        ["location", "{" + Good + ",\"location\":\"" + new string('l', 201) + "\"}"],
        ["leadMinutes", "{" + Good + ",\"leadMinutes\":-1}"],
        ["leadMinutes", "{" + Good + ",\"leadMinutes\":1441}"]
    ];

    [Theory]
    [MemberData(nameof(BadEdits))]
    public async Task An_edit_outside_its_bounds_is_refused_by_field_and_nothing_is_written(string field, string body)
    {
        using var owner = Owner();
        var before = await VersionAsync();

        using var response = await Put(owner, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Single(errors.EnumerateObject());
        Assert.Equal(0, _google.Calls);
        Assert.Equal(before, await VersionAsync());
    }

    public static IEnumerable<object[]> EditEdges =>
    [
        ["{" + Good + ",\"durationMinutes\":5,\"leadMinutes\":0,\"location\":\"" + new string('l', 200) + "\"}"],
        ["{" + Good + ",\"durationMinutes\":1440,\"leadMinutes\":1440}"],
        ["{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"series\",\"title\":\"" + new string('t', 200) + "\",\"startsAt\":\"2027-10-29T12:00:00Z\"}"]
    ];

    [Theory]
    [MemberData(nameof(EditEdges))]
    public async Task Every_edit_bound_itself_is_accepted(string body)
    {
        using var owner = Owner();

        using var response = await Put(owner, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static IEnumerable<object[]> BadDeletes =>
    [
        ["key", "{\"scope\":\"occurrence\"}"],
        ["key", "{\"key\":\"\",\"scope\":\"series\"}"],
        ["scope", "{\"key\":\"standup@google.com|20261028T130000Z\"}"],
        ["scope", "{\"key\":\"standup@google.com|20261028T130000Z\",\"scope\":\"Series\"}"]
    ];

    [Theory]
    [MemberData(nameof(BadDeletes))]
    public async Task A_deletion_without_a_key_or_a_scope_is_refused_by_field(string field, string body)
    {
        using var owner = Owner();

        using var response = await owner.PostAsync("/api/alerts/events/delete", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Equal(0, _google.Calls);
    }

    public static IEnumerable<object[]> Conflicts =>
    [
        ["someone else's", CalendarWriteMessages.OnlyOrganizer],
        ["no calendar", CalendarWriteMessages.NotConnected],
        ["no edit access", CalendarWriteMessages.NotConnected],
        ["another calendar", CalendarWriteMessages.OtherCalendar]
    ];

    private void Break(string failure)
    {
        switch (failure)
        {
            case "someone else's": _google.Seed("standup@google.com", organisedHere: false); break;
            case "no calendar": _google.Grant = null; break;
            case "no edit access": _google.Grant = new CalendarGrant("primary", Feeds.Owner, false); break;
            case "another calendar": _google.Grant = new CalendarGrant("primary", "someone@example.test", true); break;
            case "google refuses": _google.RefusingWrites = (HttpStatusCode.InternalServerError, "backendError"); break;
            case "not found": _google.Clear(); break;
            case "unreachable": _google.Unreachable = true; break;
            case "sign-in refused": _google.TokenRefused = true; break;
        }
    }

    [Theory]
    [MemberData(nameof(Conflicts))]
    public async Task A_refusal_the_owner_can_fix_is_409_with_the_reason_and_nothing_is_kept_or_pushed(string failure, string message)
    {
        Break(failure);
        using var owner = Owner();
        var before = await VersionAsync();

        using var edit = await Put(owner, """{"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"A","startsAt":"2026-10-28T09:30:00-04:00"}""");
        using var delete = await Delete(owner, StandupKey, "occurrence");

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal(message, (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(message, (await delete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Empty(_google.Writes);
        Assert.Empty(await _store.EventChangesAsync(CancellationToken.None));
        Assert.Equal(before, await VersionAsync());
        ByKey(await StatusAlerts(), StandupKey);
    }

    public static IEnumerable<object[]> GatewayFailures =>
    [
        ["google refuses", "Google refused the change (HTTP 500)."],
        ["not found", CalendarWriteMessages.NotFound],
        ["unreachable", CalendarWriteMessages.Unreachable],
        ["sign-in refused", CalendarWriteMessages.SignInRefused]
    ];

    [Theory]
    [MemberData(nameof(GatewayFailures))]
    public async Task Any_other_google_failure_is_502_with_a_short_reason_and_nothing_is_kept_or_pushed(string failure, string message)
    {
        Break(failure);
        using var owner = Owner();
        var before = await VersionAsync();

        using var edit = await Put(owner, """{"key":"daily@google.com|20261029T140000Z","scope":"occurrence","title":"A","startsAt":"2026-10-29T11:00:00-04:00"}""");
        using var delete = await Delete(owner, DailyTomorrow, "occurrence");
        var text = await edit.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, edit.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, delete.StatusCode);
        Assert.Equal(message, JsonDocument.Parse(text).RootElement.GetProperty("detail").GetString());
        Assert.Equal(message, (await delete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.DoesNotContain("development-token", text);
        Assert.Empty(_google.Writes);
        Assert.Empty(await _store.EventChangesAsync(CancellationToken.None));
        Assert.Equal(before, await VersionAsync());
    }

    [Fact]
    public async Task Both_routes_share_the_actions_limit()
    {
        using var owner = Owner();
        for (var press = 0; press < AlertsEndpoints.DefaultActionsPerMinute; press++)
        {
            using var ok = await owner.PostAsync("/api/alerts/unmute", null);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var edit = await Put(owner, """{"key":"standup@google.com|20261028T130000Z","scope":"occurrence","title":"A","startsAt":"2026-10-28T09:30:00-04:00"}""");
        using var delete = await Delete(owner, StandupKey, "occurrence");

        Assert.Equal(HttpStatusCode.TooManyRequests, edit.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, delete.StatusCode);
        Assert.Equal(0, _google.Calls);
    }

    public void Dispose() => _app.Dispose();
}
