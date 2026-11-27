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
/// One ring of a routine alarm and its steps moved later, from every entry
/// point: the delay PUT and DELETE from the phone and the page, the
/// status's rings and delays, the worker's sends to Pushover and their
/// repeats, a ring stopped before the delay ringing again, the one-time
/// routine, and the 14 days kept. Neb asked for this on 2026-11-23, to do
/// his morning beauty routine after his workout.
/// </summary>
public sealed class AlertRoutineDelayRouteTests : IDisposable
{
    /// <summary>Wednesday 28 October 2026, 12:00 UTC.</summary>
    private static readonly Instant Noon = Instant.FromUtc(2026, 10, 28, 12, 0);

    private static readonly Guid Meds = Guid.Parse("5a1e0000-0000-4000-8000-000000000001");
    private static readonly Guid Beauty = Guid.Parse("5a1e0000-0000-4000-8000-000000000002");

    private readonly FakeClock _clock = new(Noon);
    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FakeGoogleCalendar _google = new();
    private readonly CapturedLogs _logs = new();

    private static int _receipts;

    /// <summary>A receipt of its own for every send: one receipt answers for one delivery.</summary>
    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        $"{{\"status\":1,\"request\":\"r\",\"receipt\":\"rcptdelaysxx{Interlocked.Increment(ref _receipts):D16}\"}}",
        "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")])),
        "text/calendar"));

    private readonly TestApp _app;

    public AlertRoutineDelayRouteTests()
    {
        _app = App(kestrel: false);
        Worker.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private TestApp App(bool kestrel)
    {
        var settings = AlertsRouteTests.Settings();
        settings["Alerts:TimeZone"] = "UTC";
        settings["RateLimits:AlertsActionsPerMinute"] = "1000";
        return new TestApp(
            settings,
            services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IClock>(_clock);
                services.AddSingleton<IAlertStore>(_store);
                services.AddSingleton<IAlertDeviceStore>(_devices);
                services.AddSingleton<ILoggerProvider>(_logs);
                services.AddSingleton(new AlertsOptions
                {
                    TimeZone = "UTC",
                    CalendarIcsUrl = AlertsRouteTests.FeedUrl,
                    PushoverAppToken = AlertsRouteTests.AppToken,
                    PushoverUserKey = AlertsRouteTests.UserKey,
                    PushoverRetrySeconds = 0
                });
                services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => _calendar);
                services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
                services.AddSingleton<IAlertCalendarGrant>(_google);
                services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
            },
            kestrel: kestrel);
    }

    public void Dispose() => _app.Dispose();

    private CalendarAlertWorker Worker => _app.Factory.Services.GetRequiredService<CalendarAlertWorker>();

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private async Task<HttpClient> PhoneAsync(string zone = "UTC")
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync("/api/alerts/devices", new { name = "Neb's iPhone" });
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", token);
        phone.DefaultRequestHeaders.Add(AlertDeviceZones.Header, zone);
        return phone;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private static readonly string TwoSteps =
        $"[{{\"id\":\"{Meds}\",\"label\":\"Take medication\",\"offsetMinutes\":5}},{{\"id\":\"{Beauty}\",\"label\":\"Beauty care routine\",\"offsetMinutes\":5}}]";

    private static string Body(int hour = 12, int minute = 0, string days = "[1,2,3,4,5,6,7]", string? steps = null, string label = "Wake up")
    {
        var tail = steps is null ? "" : $",\"steps\":{steps}";
        return $"{{\"label\":\"{label}\",\"hour\":{hour},\"minute\":{minute},\"days\":{days},\"enabled\":true,\"snoozeMinutes\":9{tail}}}";
    }

    /// <summary>A routine at 12:00 UTC with the two steps, saved an hour before the clock's time.</summary>
    private async Task<Guid> CreateAsync(HttpClient client, string days = "[1,2,3,4,5,6,7]", string? steps = null, int hour = 12)
    {
        var saved = _clock.Now;
        var before = (await _store.RoutinesAsync(CancellationToken.None)).Select(routine => routine.Id).ToHashSet();
        _clock.Now = saved - Duration.FromHours(1);
        using var response = await Send(client, "POST", "/api/alerts/routines", Body(hour: hour, days: days, steps: steps ?? TwoSteps));
        _clock.Now = saved;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routines").EnumerateArray()
            .Select(routine => routine.GetProperty("id").GetGuid())
            .Single(id => !before.Contains(id));
    }

    private static string CompletionPath(Guid routine, Guid step, string cycle) =>
        $"/api/alerts/routines/{routine}/steps/{step}/cycles/{cycle}/completion";

    private static string OutcomePath(Guid routine, string cycle) =>
        $"/api/alerts/routines/{routine}/cycles/{cycle}/outcome";

    private static string DelayPath(Guid routine, string cycle) =>
        $"/api/alerts/routines/{routine}/cycles/{cycle}/delay";

    private static Dictionary<string, DateTimeOffset> RingTimes(JsonElement state) =>
        state.GetProperty("routineRings").EnumerateArray().ToDictionary(
            ring => ring.GetProperty("key").GetString()!,
            ring => ring.GetProperty("alertAt").GetDateTimeOffset());

    private static DateTimeOffset At(int minutesAfterNoon) => (Noon + Duration.FromMinutes(minutesAfterNoon)).ToDateTimeOffset();

    private static List<string?> Keys(JsonElement state) =>
        [.. state.GetProperty("routineRings").EnumerateArray().Select(ring => ring.GetProperty("key").GetString())];

    private static async Task<List<string?>> KeysIn(HttpResponseMessage response) => Keys(await StateOf(response));

    private static async Task<JsonElement> StateOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement[]> RingsIn(HttpClient client) =>
        [.. (await client.GetFromJsonAsync<JsonElement>("/api/alerts/status")).GetProperty("routineRings").EnumerateArray()];

    private IReadOnlyList<RecordingHandler.Seen> Messages =>
        [.. _pushover.Requests.Where(seen => seen.Url.AbsolutePath == "/1/messages.json")];

    private IReadOnlyList<RecordingHandler.Seen> Cancels =>
        [.. _pushover.Requests.Where(seen => seen.Url.AbsolutePath.EndsWith("/cancel.json", StringComparison.Ordinal))];

    private static string Key(Guid routine, string cycle, Guid? step = null) =>
        step is { } id ? $"routine:{routine:D}:{cycle}:step:{id:D}" : $"routine:{routine:D}:{cycle}";

    // ------------------------------------------------------------------ shift

    [Fact]
    public async Task A_delay_ahead_moves_the_ring_and_its_steps_keeps_their_keys_and_is_in_routineDelays_in_the_contract_shape()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":90}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        var rings = RingTimes(state);
        Assert.Equal(At(90), rings[Key(id, "2026-10-28T12:00")]);
        Assert.Equal(At(95), rings[Key(id, "2026-10-28T12:00", Meds)]);
        Assert.Equal(At(100), rings[Key(id, "2026-10-28T12:00", Beauty)]);
        var delay = Assert.Single(state.GetProperty("routineDelays").EnumerateArray());
        Assert.Equal(["routineId", "cycle", "minutes", "at"], delay.EnumerateObject().Select(field => field.Name));
        Assert.Equal(id, delay.GetProperty("routineId").GetGuid());
        Assert.Equal("2026-10-28T12:00", delay.GetProperty("cycle").GetString());
        Assert.Equal(90, delay.GetProperty("minutes").GetInt32());
        Assert.Equal((Noon - Duration.FromMinutes(30)).ToDateTimeOffset(), delay.GetProperty("at").GetDateTimeOffset());
        Assert.True(await _devices.PlanVersionAsync(CancellationToken.None) > version);

        // Pushover sends each ring at its new time and not before.
        _clock.Now = Noon + Duration.FromMinutes(11);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Empty(Messages);
        _clock.Now = Noon + Duration.FromMinutes(96);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Take medication"], Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task A_second_delay_replaces_the_first_and_counts_from_the_planned_ring()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":30}");

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":45}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        Assert.Equal(At(45), RingTimes(state)[Key(id, "2026-10-28T12:00")]);
        Assert.Equal(45, Assert.Single(state.GetProperty("routineDelays").EnumerateArray()).GetProperty("minutes").GetInt32());
        Assert.Equal(45, Assert.Single(await _store.DelaysAsync(Instant.MinValue, CancellationToken.None)).Minutes);
    }

    [Fact]
    public async Task The_longest_delay_moves_a_ring_into_the_next_day_and_the_status_lists_it()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone, days: "[3]");

        await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":720}");
        _clock.Now = Noon + Duration.FromHours(11);

        var rings = (await RingsIn(phone)).ToDictionary(ring => ring.GetProperty("key").GetString()!, ring => ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal(At(720), rings[Key(id, "2026-10-28T12:00")]);
        Assert.Equal(At(730), rings[Key(id, "2026-10-28T12:00", Beauty)]);
    }

    /// <summary>
    /// Neb asked on 2026-11-26 to delay past three hours. A ring moved three
    /// days rings then with its steps, and the status still lists its delay
    /// and a step checked off in it after the two days it lists.
    /// </summary>
    [Fact]
    public async Task A_three_day_delay_rings_then_with_its_steps_and_the_status_keeps_its_delay_and_check_offs()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone, days: "[3]");

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), $"{{\"minutes\":{3 * 1440}}}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _clock.Now = Noon + Duration.FromDays(3) - Duration.FromMinutes(30);

        var rings = (await RingsIn(phone)).ToDictionary(ring => ring.GetProperty("key").GetString()!, ring => ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal(At(3 * 1440), rings[Key(id, "2026-10-28T12:00")]);
        Assert.Equal(At(3 * 1440 + 10), rings[Key(id, "2026-10-28T12:00", Beauty)]);

        using var done = await Send(phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-28T12:00/completion", "{}");
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var status = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal(3 * 1440, Assert.Single(status.GetProperty("routineDelays").EnumerateArray()).GetProperty("minutes").GetInt32());
        Assert.Equal(Meds, Assert.Single(status.GetProperty("routineCompletions").EnumerateArray()).GetProperty("stepId").GetGuid());
    }

    [Fact]
    public async Task Done_still_silences_the_routines_own_ring_and_the_delay_moves_its_steps()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\"}");

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":60}");

        var rings = RingTimes(await StateOf(response));
        Assert.DoesNotContain(Key(id, "2026-10-28T12:00"), rings.Keys);
        Assert.Equal(At(65), rings[Key(id, "2026-10-28T12:00", Meds)]);

        await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"skipped\"}");
        Assert.DoesNotContain(await RingsIn(phone), ring => ring.GetProperty("key").GetString()!.StartsWith(Key(id, "2026-10-28T12:00"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_step_checked_off_stays_off_when_its_cycle_is_delayed()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":60}");

        var rings = RingTimes(await StateOf(response));
        Assert.DoesNotContain(Key(id, "2026-10-28T12:00", Meds), rings.Keys);
        Assert.Equal(At(70), rings[Key(id, "2026-10-28T12:00", Beauty)]);
    }

    // ------------------------------------------------------------------ rang already

    [Fact]
    public async Task A_ring_stopped_before_the_delay_rings_again_at_its_new_time()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(1);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up"], Messages.Select(sent => sent.Form["title"]));
        using var stop = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(id, "2026-10-28T12:00"), via = "phone" });
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        var cancels = Cancels.Count;

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":30}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ring = (await StateOf(response)).GetProperty("routineRings").EnumerateArray()
            .Single(ring => ring.GetProperty("key").GetString() == Key(id, "2026-10-28T12:00"));
        Assert.Equal(At(30), ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.False(ring.GetProperty("acknowledged").GetBoolean());
        Assert.False(await _store.IsAcknowledgedAsync(Key(id, "2026-10-28T12:00"), CancellationToken.None));
        // The ring that was ringing has its repeats canceled.
        Assert.True(Cancels.Count > cancels);

        _clock.Now = Noon + Duration.FromMinutes(31);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Wake up"], Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task A_ring_ringing_now_stops_and_its_steps_wait_for_their_new_time()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Take medication"], Messages.Select(sent => sent.Form["title"]));

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":20}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, Cancels.Count);
        var rings = RingTimes(await StateOf(response));
        Assert.Equal(At(20), rings[Key(id, "2026-10-28T12:00")]);
        Assert.Equal(At(25), rings[Key(id, "2026-10-28T12:00", Meds)]);
        Assert.Equal(At(30), rings[Key(id, "2026-10-28T12:00", Beauty)]);
        Assert.DoesNotContain(_store.Claims.Keys, key => key.StartsWith(Key(id, "2026-10-28T12:00"), StringComparison.Ordinal));

        _clock.Now = Noon + Duration.FromMinutes(26);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Take medication", "Wake up", "Take medication"], Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task A_stopped_ring_still_behind_after_the_delay_stays_stopped()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(20);
        await Worker.TickAsync(CancellationToken.None);
        await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(id, "2026-10-28T12:00"), via = "phone" });

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":5}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await _store.IsAcknowledgedAsync(Key(id, "2026-10-28T12:00"), CancellationToken.None));
        Assert.True(_store.Claims.ContainsKey(Key(id, "2026-10-28T12:00")));
    }

    // ------------------------------------------------------------------ undo

    [Fact]
    public async Task Undo_puts_the_rings_back_at_their_planned_time()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":60}");
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await Send(phone, "DELETE", DelayPath(id, "2026-10-28T12:00"), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        Assert.Equal(At(0), RingTimes(state)[Key(id, "2026-10-28T12:00")]);
        Assert.Equal(At(5), RingTimes(state)[Key(id, "2026-10-28T12:00", Meds)]);
        Assert.Empty(state.GetProperty("routineDelays").EnumerateArray());
        Assert.True(await _devices.PlanVersionAsync(CancellationToken.None) > version);
    }

    [Fact]
    public async Task Undo_of_a_deleted_routines_delay_works_and_an_unknown_routine_is_404()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":10}");
        await Send(phone, "DELETE", $"/api/alerts/routines/{id}", null);

        using var undo = await Send(phone, "DELETE", DelayPath(id, "2026-10-28T12:00"), null);
        using var again = await Send(phone, "DELETE", DelayPath(id, "2026-10-28T12:00"), null);

        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        Assert.Empty(await _store.DelaysAsync(Instant.MinValue, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Undo_on_a_routine_with_no_delay_is_200()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "DELETE", DelayPath(id, "2026-10-28T12:00"), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------ bounds

    [Theory]
    [InlineData("2026-10-28T12:01")]
    [InlineData("2026-10-13T12:00")]
    [InlineData("2026-10-31T12:00")]
    [InlineData("tomorrow")]
    [InlineData("2026-10-28T12:00:00")]
    public async Task A_cycle_that_is_not_a_ring_of_the_routine_in_the_window_is_400(string cycle)
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", DelayPath(id, cycle), "{\"minutes\":30}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await _store.DelaysAsync(Instant.MinValue, CancellationToken.None));
    }

    [Fact]
    public async Task The_next_cycle_may_be_delayed_and_an_unknown_routine_is_404()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, days: "[1,2,3,4,5]");

        using var thursday = await Send(phone, "PUT", DelayPath(id, "2026-10-29T12:00"), "{\"minutes\":30}");
        using var saturday = await Send(phone, "PUT", DelayPath(id, "2026-10-31T12:00"), "{\"minutes\":30}");
        using var unknown = await Send(phone, "PUT", DelayPath(Guid.NewGuid(), "2026-10-28T12:00"), "{\"minutes\":30}");

        Assert.Equal(HttpStatusCode.OK, thursday.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, saturday.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // Today's ring keeps its planned time. Thursday's moves.
        Assert.Equal(At(0), RingTimes(await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status"))[Key(id, "2026-10-28T12:00")]);
        _clock.Now = Noon + Duration.FromHours(13);
        var rings = (await RingsIn(phone)).ToDictionary(ring => ring.GetProperty("key").GetString()!, ring => ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal(At(24 * 60 + 30), rings[Key(id, "2026-10-29T12:00")]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"minutes\":null}")]
    [InlineData("{\"minutes\":0}")]
    [InlineData("{\"minutes\":-5}")]
    [InlineData("{\"minutes\":10081}")]
    [InlineData("null")]
    public async Task Minutes_outside_1_to_10080_are_refused_under_minutes(string body)
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["minutes"], (await StateOf(response)).GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.Empty(await _store.DelaysAsync(Instant.MinValue, CancellationToken.None));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10080)]
    public async Task Each_end_of_the_range_is_taken(int minutes)
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), $"{{\"minutes\":{minutes}}}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(minutes, Assert.Single(await _store.DelaysAsync(Instant.MinValue, CancellationToken.None)).Minutes);
    }

    [Fact]
    public async Task A_body_that_is_not_JSON_is_refused()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var request = new HttpRequestMessage(HttpMethod.Put, DelayPath(id, "2026-10-28T12:00"))
        {
            Content = new StringContent("minutes=30", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        using var form = await phone.SendAsync(request);
        using var broken = await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, form.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.Empty(await _store.DelaysAsync(Instant.MinValue, CancellationToken.None));
    }

    // ------------------------------------------------------------------ one-time routines

    [Fact]
    public async Task A_delayed_one_time_routine_stays_until_its_steps_have_stopped_at_their_new_time()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromHours(2);
        var id = await CreateAsync(phone, days: "[]");
        await Send(phone, "PUT", DelayPath(id, "2026-10-28T12:00"), "{\"minutes\":300}");

        // Past the planned steps' stop, before the delayed ones' ring.
        _clock.Now = Noon + Duration.FromMinutes(10 + 180 + 1);
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));

        _clock.Now = Noon + Duration.FromMinutes(301);
        using var last = await Send(phone, "PUT", CompletionPath(id, Beauty, "2026-10-28T12:00"), "{}");
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ retention

    [Fact]
    public async Task A_pass_deletes_delays_whose_cycle_is_older_than_14_days()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(20);
        var id = await CreateAsync(phone);
        _clock.Now = Noon - Duration.FromDays(13);
        await Send(phone, "PUT", DelayPath(id, "2026-10-14T12:00"), "{\"minutes\":10}");
        await Send(phone, "PUT", DelayPath(id, "2026-10-15T12:00"), "{\"minutes\":10}");
        await Send(phone, "PUT", DelayPath(id, "2026-10-16T12:00"), "{\"minutes\":10}");
        Assert.Equal(3, (await _store.DelaysAsync(Instant.MinValue, CancellationToken.None)).Count);
        _clock.Now = Noon + Duration.FromDays(1) + Duration.FromHours(1);

        await Worker.ReadNowAsync(CancellationToken.None);

        Assert.Equal(
            ["2026-10-16T12:00"],
            (await _store.DelaysAsync(Instant.MinValue, CancellationToken.None)).Select(delay => delay.Cycle));
    }

    [Fact]
    public async Task The_status_lists_delays_from_two_days_back()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(5);
        var id = await CreateAsync(phone);
        _clock.Now = Noon - Duration.FromDays(4);
        using var old = await Send(phone, "PUT", DelayPath(id, "2026-10-24T12:00"), "{\"minutes\":10}");
        _clock.Now = Noon - Duration.FromDays(2);
        using var recent = await Send(phone, "PUT", DelayPath(id, "2026-10-27T12:00"), "{\"minutes\":10}");
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (old.StatusCode, recent.StatusCode));
        _clock.Now = Noon;

        var state = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        Assert.Equal(["2026-10-27T12:00"], state.GetProperty("routineDelays").EnumerateArray().Select(delay => delay.GetProperty("cycle").GetString()));
    }

    // ------------------------------------------------------------------ who may call

    public static IEnumerable<object?[]> DelayRoutes =>
    [
        ["PUT", $"/api/alerts/routines/{Guid.Empty}/cycles/2026-10-28T12:00/delay", "{\"minutes\":30}"],
        ["DELETE", $"/api/alerts/routines/{Guid.Empty}/cycles/2026-10-28T12:00/delay", null],
    ];

    [Theory]
    [MemberData(nameof(DelayRoutes))]
    public async Task No_cookie_and_no_token_is_401_and_the_refusal_is_logged(string method, string path, string? body)
    {
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(_logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.SignInRequired);
    }

    [Theory]
    [MemberData(nameof(DelayRoutes))]
    public async Task A_strangers_Google_account_is_403(string method, string path, string? body)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, "stranger@example.test");

        using var response = await Send(stranger, method, path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DelayRoutes))]
    public async Task A_wrong_phone_token_is_401(string method, string path, string? body)
    {
        using var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", "aat_" + new string('A', 43));

        using var response = await Send(phone, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DelayRoutes))]
    public async Task A_paired_phone_and_the_owner_reach_both_delay_routes(string method, string path, string? body)
    {
        using var phone = await PhoneAsync();
        using var owner = Owner();

        using var fromPhone = await Send(phone, method, path, body);
        using var fromOwner = await Send(owner, method, path, body);

        Assert.Equal(HttpStatusCode.NotFound, fromPhone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fromOwner.StatusCode);
    }
}
