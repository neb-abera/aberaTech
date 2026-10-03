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
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// Routine alarms ring through the server as well as the phone. From every
/// entry point. The status lists each ring under routineRings, in the zone
/// the phone sends. The worker sends a due ring to Pushover with the
/// calendar alarms' checks. POST /ack takes a ring's key from the page or
/// the phone. An edit, a switch off and a delete change what rings.
/// </summary>
public sealed class AlertRoutineRingRouteTests : IDisposable
{
    /// <summary>12:00 UTC, 15:00 in Amman, on Wednesday 28 October 2026.</summary>
    private static readonly Instant Noon = Instant.FromUtc(2026, 10, 28, 12, 0);

    private const string Receipt = "rcptroutine000000000000000001";

    private readonly FakeClock _clock = new(Noon);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FakeGoogleCalendar _google = new();
    private readonly CapturedLogs _logs = new();

    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK, $"{{\"status\":1,\"request\":\"r\",\"receipt\":\"{Receipt}\"}}", "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")])),
        "text/calendar"));

    private readonly TestApp _app;

    public AlertRoutineRingRouteTests()
    {
        var settings = AlertsRouteTests.Settings();
        settings["RateLimits:AlertsActionsPerMinute"] = "1000";
        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton<IAlertStore>(_store);
            services.AddSingleton<IAlertDeviceStore>(_devices);
            services.AddSingleton<ILoggerProvider>(_logs);
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
        Worker.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose() => _app.Dispose();

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    /// <summary>A paired phone that sends its zone on every request, as the app does.</summary>
    private async Task<HttpClient> PhoneAsync(string? zone = "Asia/Amman")
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Neb's iPhone" });
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", token);
        if (zone is not null) phone.DefaultRequestHeaders.Add(AlertDeviceZones.Header, zone);
        return phone;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private static string Body(string label, int hour, int minute, string days = "[1,2,3,4,5,6,7]", bool enabled = true) =>
        $"{{\"label\":\"{label}\",\"hour\":{hour},\"minute\":{minute},\"days\":{days},\"enabled\":{(enabled ? "true" : "false")},\"snoozeMinutes\":9}}";

    private async Task<Guid> CreateAsync(HttpClient client, string label, int hour, int minute, string days = "[1,2,3,4,5,6,7]")
    {
        using var response = await Send(client, "POST", "/api/alerts/routines", Body(label, hour, minute, days));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routines").EnumerateArray()
            .Single(routine => routine.GetProperty("label").GetString() == label).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> RingsIn(HttpClient client) =>
        [.. (await client.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("routineRings").EnumerateArray()];

    private IReadOnlyList<RecordingHandler.Seen> Messages =>
        [.. _pushover.Requests.Where(seen => seen.Url.AbsolutePath == "/1/messages.json")];

    private IReadOnlyList<RecordingHandler.Seen> Cancels =>
        [.. _pushover.Requests.Where(seen => seen.Url.AbsolutePath.EndsWith("/cancel.json", StringComparison.Ordinal))];

    // ------------------------------------------------------------------ the status

    [Fact]
    public async Task The_status_lists_each_ring_with_exactly_the_contract_fields_in_the_phones_zone()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(phone, "Wake up", 6, 30);
        _clock.Now = Noon;

        var rings = await RingsIn(phone);

        // Thursday 06:30 in Amman is 03:30 UTC: the next ring inside 24 hours.
        var ring = Assert.Single(rings);
        Assert.Equal(
            ["key", "routineId", "label", "alertAt", "startsAt", "acknowledged", "acknowledgedAt", "acknowledgedVia"],
            ring.EnumerateObject().Select(field => field.Name));
        Assert.Equal($"routine:{id:D}:2026-10-29T06:30", ring.GetProperty("key").GetString());
        Assert.Equal(id, ring.GetProperty("routineId").GetGuid());
        Assert.Equal("Wake up", ring.GetProperty("label").GetString());
        Assert.Equal(Instant.FromUtc(2026, 10, 29, 3, 30).ToDateTimeOffset(), ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Matches(@"^2026-10-29T03:30:00(\+00:00|Z)$", ring.GetProperty("alertAt").GetString());
        // The saved StopAfterMinutes, 180 by default.
        Assert.Equal(Instant.FromUtc(2026, 10, 29, 6, 30).ToDateTimeOffset(), ring.GetProperty("startsAt").GetDateTimeOffset());
        Assert.False(ring.GetProperty("acknowledged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ring.GetProperty("acknowledgedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, ring.GetProperty("acknowledgedVia").ValueKind);
    }

    [Fact]
    public async Task The_status_lists_rings_from_StopAfter_ago_to_24_hours_ahead()
    {
        using var phone = await PhoneAsync("UTC");
        _clock.Now = Noon - Duration.FromDays(2);
        // The window is 09:00 today, 180 minutes ago, to 12:00 tomorrow.
        await CreateAsync(phone, "A 09:01", 9, 1);
        await CreateAsync(phone, "B 08:59", 8, 59);
        await CreateAsync(phone, "C 12:01", 12, 1);
        _clock.Now = Noon;

        var rings = (await RingsIn(phone))
            .Select(ring => (ring.GetProperty("label").GetString(), ring.GetProperty("alertAt").GetDateTimeOffset()))
            .ToList();

        Assert.Equal(
            [
                ("A 09:01", Instant.FromUtc(2026, 10, 28, 9, 1).ToDateTimeOffset()),
                ("C 12:01", Instant.FromUtc(2026, 10, 28, 12, 1).ToDateTimeOffset()),
                ("B 08:59", Instant.FromUtc(2026, 10, 29, 8, 59).ToDateTimeOffset()),
                ("A 09:01", Instant.FromUtc(2026, 10, 29, 9, 1).ToDateTimeOffset())
            ],
            rings);
    }

    [Fact]
    public async Task Rings_are_never_listed_under_alerts()
    {
        using var phone = await PhoneAsync();
        await CreateAsync(phone, "Wake up", 6, 30);

        var status = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.DoesNotContain(status.GetProperty("alerts").EnumerateArray(), alert => alert.GetProperty("title").GetString() == "Wake up");
        Assert.Single(status.GetProperty("routineRings").EnumerateArray());
    }

    // ------------------------------------------------------------------ the zone

    [Fact]
    public async Task The_zone_is_the_one_the_most_recently_seen_phone_sent()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Wake up", 6, 30);
        _clock.Now = Noon;

        // No phone has sent a zone: the settings' fallback, UTC here.
        Assert.Equal($"routine:{id:D}:2026-10-29T06:30", (await RingsIn(owner)).Single().GetProperty("key").GetString());
        Assert.Equal(
            Instant.FromUtc(2026, 10, 29, 6, 30).ToDateTimeOffset(),
            (await RingsIn(owner)).Single().GetProperty("alertAt").GetDateTimeOffset());

        using var amman = await PhoneAsync("Asia/Amman");
        await amman.GetAsync("/api/alerts/status");
        Assert.Equal(
            Instant.FromUtc(2026, 10, 29, 3, 30).ToDateTimeOffset(),
            (await RingsIn(owner)).Single().GetProperty("alertAt").GetDateTimeOffset());

        // A second phone in New York, seen later, moves the rings to its zone.
        _clock.Now = Noon + Duration.FromMinutes(5);
        using var newYork = await PhoneAsync("America/New_York");
        await newYork.GetAsync("/api/alerts/status");
        Assert.Equal(
            Instant.FromUtc(2026, 10, 29, 10, 30).ToDateTimeOffset(),
            (await RingsIn(owner)).Single().GetProperty("alertAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Asia/Amman\r\nX-Evil: 1")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    public async Task A_zone_the_database_does_not_know_is_ignored_and_never_logged(string zone)
    {
        using var owner = Owner();
        using var phone = await PhoneAsync(zone: null);
        phone.DefaultRequestHeaders.TryAddWithoutValidation(AlertDeviceZones.Header, zone);

        using var response = await phone.GetAsync("/api/alerts/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, _devices.ZoneWrites);
        Assert.Null(await _devices.LatestTimeZoneAsync(CancellationToken.None));
        if (zone.Length > 0) Assert.All(_logs.Entries, entry => Assert.DoesNotContain(zone, entry.Everything));
    }

    [Fact]
    public async Task A_zone_is_written_when_it_changes_and_not_on_every_request()
    {
        using var phone = await PhoneAsync("Asia/Amman");

        for (var i = 0; i < 3; i++) await phone.GetAsync("/api/alerts/status");

        Assert.Equal(1, _devices.ZoneWrites);
        Assert.Equal("Asia/Amman", await _devices.LatestTimeZoneAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ the send

    [Fact]
    public async Task A_due_ring_goes_to_Pushover_once_as_an_alarm_and_its_receipt_is_kept()
    {
        using var phone = await PhoneAsync("UTC");
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(phone, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);

        await Worker.TickAsync(CancellationToken.None);
        await Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(Messages);
        Assert.Equal("Take meds", sent.Form["title"]);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("60", sent.Form["retry"]);
        Assert.Equal("10800", sent.Form["expire"]);
        var key = $"routine:{id:D}:2026-10-28T12:00";
        Assert.Equal(Receipt, (await _store.DeliveryAsync(key, CancellationToken.None))?.Receipt);
    }

    [Fact]
    public async Task A_ring_waits_for_the_backup_delay_so_the_phone_rings_first()
    {
        using var owner = Owner();
        var form = AlertsRouteTests.Form(("backupDelaySeconds", 60));
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/alerts/settings", form)).StatusCode);
        _clock.Now = Noon - Duration.FromHours(1);
        await CreateAsync(owner, "Take meds", 12, 0);

        _clock.Now = Noon + Duration.FromSeconds(30);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Empty(Messages);

        _clock.Now = Noon + Duration.FromSeconds(61);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Messages);
    }

    [Fact]
    public async Task A_muted_ring_sends_nothing()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        await CreateAsync(owner, "Take meds", 12, 0);
        _clock.Now = Noon;
        await Send(owner, "POST", "/api/alerts/mute", "{\"until\":\"hour\"}");
        _clock.Now = Noon + Duration.FromSeconds(5);

        await Worker.TickAsync(CancellationToken.None);

        Assert.Empty(Messages);
    }

    // ------------------------------------------------------------------ acknowledge

    [Fact]
    public async Task Acknowledging_a_listed_ring_in_the_browser_stops_Pushover_before_it_sends()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);
        var key = $"routine:{id:D}:2026-10-28T12:00";

        using var response = await owner.PostAsJsonAsync("/api/alerts/ack", new { key, via = "browser" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ring = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routineRings").EnumerateArray()
            .Single(item => item.GetProperty("key").GetString() == key);
        Assert.True(ring.GetProperty("acknowledged").GetBoolean());
        Assert.Equal("browser", ring.GetProperty("acknowledgedVia").GetString());
        Assert.Equal(_clock.Now.ToDateTimeOffset(), ring.GetProperty("acknowledgedAt").GetDateTimeOffset());
        // The acknowledgement's start is the ring's StartsAt.
        Assert.True(await _store.IsAcknowledgedAsync(key, CancellationToken.None));

        await Worker.TickAsync(CancellationToken.None);
        Assert.Empty(Messages);
    }

    [Fact]
    public async Task Acknowledging_a_ring_on_the_phone_after_Pushover_sent_it_cancels_the_repeats_and_pushes_the_phones()
    {
        using var phone = await PhoneAsync("UTC");
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(phone, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Messages);
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = $"routine:{id:D}:2026-10-28T12:00", via = "phone" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/1/receipts/{Receipt}/cancel.json", Assert.Single(Cancels).Url.AbsolutePath);
        Assert.Equal(version + 1, await _devices.PlanVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_sent_ring_is_still_acknowledged_after_it_drops_off_the_list()
    {
        using var phone = await PhoneAsync("UTC");
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(phone, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);
        await Worker.TickAsync(CancellationToken.None);
        _clock.Now = Noon + Duration.FromHours(4);

        using var response = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = $"routine:{id:D}:2026-10-28T12:00", via = "phone" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("2026-10-27T12:00")]
    [InlineData("2026-10-30T12:00")]
    [InlineData("2026-10-28T12:01")]
    public async Task A_ring_neither_listed_nor_sent_is_404(string when)
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);

        using var response = await owner.PostAsJsonAsync("/api/alerts/ack", new { key = $"routine:{id:D}:{when}", via = "browser" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_claim_an_acknowledgement_came_from_Pushover()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);

        using var response = await owner.PostAsJsonAsync("/api/alerts/ack", new { key = $"routine:{id:D}:2026-10-28T12:00", via = "pushover" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _store.AcknowledgementsAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ edit, switch off, delete

    [Fact]
    public async Task An_edit_while_a_ring_rings_keeps_that_ring_and_moves_the_later_ones()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromMinutes(1);
        var ringing = $"routine:{id:D}:2026-10-28T12:00";

        using var response = await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body("Gym", 18, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rings = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routineRings").EnumerateArray()
            .Select(ring => (ring.GetProperty("key").GetString(), ring.GetProperty("label").GetString())).ToList();
        Assert.Equal([(ringing, "Take meds"), ($"routine:{id:D}:2026-10-28T18:00", "Gym")], rings);
        Assert.Empty(Cancels);
    }

    [Fact]
    public async Task An_edit_to_a_time_already_past_today_does_not_ring_today()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Take meds", 18, 0);
        _clock.Now = Noon;

        using var response = await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body("Take meds", 11, 30));

        var rings = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routineRings").EnumerateArray()
            .Select(ring => ring.GetProperty("key").GetString()).ToList();
        Assert.Equal([$"routine:{id:D}:2026-10-29T11:30"], rings);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Empty(Messages);
    }

    [Fact]
    public async Task Switching_a_ringing_routine_off_stops_it_everywhere_and_cancels_Pushover()
    {
        using var phone = await PhoneAsync("UTC");
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(phone, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Messages);

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}", Body("Take meds", 12, 0, enabled: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routineRings").EnumerateArray());
        Assert.Equal($"/1/receipts/{Receipt}/cancel.json", Assert.Single(Cancels).Url.AbsolutePath);
    }

    [Fact]
    public async Task Deleting_a_ringing_routine_stops_it_everywhere_and_cancels_Pushover()
    {
        using var phone = await PhoneAsync("UTC");
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(phone, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromSeconds(5);
        await Worker.TickAsync(CancellationToken.None);

        using var response = await Send(phone, "DELETE", $"/api/alerts/routines/{id}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routineRings").EnumerateArray());
        Assert.Single(Cancels);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Single(Messages);
    }

    [Fact]
    public async Task A_held_ring_is_dropped_when_its_routine_is_switched_off_later()
    {
        using var owner = Owner();
        _clock.Now = Noon - Duration.FromHours(1);
        var id = await CreateAsync(owner, "Take meds", 12, 0);
        _clock.Now = Noon + Duration.FromMinutes(1);
        await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body("Gym", 18, 0));
        Assert.Single(await _store.HeldRingsAsync(CancellationToken.None));

        await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body("Gym", 18, 0, enabled: false));

        Assert.Empty(await _store.HeldRingsAsync(CancellationToken.None));
        Assert.Empty(await RingsIn(owner));
    }
}
