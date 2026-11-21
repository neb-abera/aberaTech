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
/// Steps of a routine alarm, from every entry point: the routine's create
/// and update from the page and the phone, the status's rings and
/// completions, the completion PUT and DELETE, the history, the photo, the
/// worker's sends to Pushover, and the one-time routine that waits for its
/// steps. Neb asked for steps on 2026-11-19: 06:00 Wake up, then Take
/// medication 5 minutes after, then Beauty care 5 minutes after that.
/// </summary>
public sealed class AlertRoutineStepRouteTests : IDisposable
{
    /// <summary>Wednesday 28 October 2026, 12:00 UTC.</summary>
    private static readonly Instant Noon = Instant.FromUtc(2026, 10, 28, 12, 0);

    private const string Receipt = "rcptsteps0000000000000000001";

    private static readonly Guid Meds = Guid.Parse("5a1e0000-0000-4000-8000-000000000001");
    private static readonly Guid Beauty = Guid.Parse("5a1e0000-0000-4000-8000-000000000002");

    /// <summary>The smallest bytes that pass the sniff: FF D8 FF and a little more.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9];

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

    public AlertRoutineStepRouteTests()
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

    // ------------------------------------------------------------------ the routine's steps

    [Fact]
    public async Task A_create_from_the_phone_stores_its_steps_with_its_own_ids_and_lists_exactly_the_contract_fields()
    {
        using var phone = await PhoneAsync();

        using var response = await Send(phone, "POST", "/api/alerts/routines", Body(steps: TwoSteps));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var routine = (await StateOf(response)).GetProperty("routines").EnumerateArray().Single();
        Assert.Equal(
            ["id", "label", "hour", "minute", "days", "enabled", "snoozeMinutes", "updatedAt", "steps"],
            routine.EnumerateObject().Select(field => field.Name));
        var steps = routine.GetProperty("steps").EnumerateArray().ToList();
        Assert.All(steps, step => Assert.Equal(["id", "label", "offsetMinutes"], step.EnumerateObject().Select(field => field.Name)));
        Assert.Equal([Meds, Beauty], steps.Select(step => step.GetProperty("id").GetGuid()));
        Assert.Equal(["Take medication", "Beauty care routine"], steps.Select(step => step.GetProperty("label").GetString()));
        Assert.Equal([5, 5], steps.Select(step => step.GetProperty("offsetMinutes").GetInt32()));
    }

    [Fact]
    public async Task A_step_with_no_id_gets_a_new_one_and_a_blank_label_is_Step()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            Body(steps: "[{\"label\":\"  \",\"offsetMinutes\":0},{\"id\":null,\"label\":\" Floss \",\"offsetMinutes\":1440}]"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var steps = (await StateOf(response)).GetProperty("routines")[0].GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["Step", "Floss"], steps.Select(step => step.GetProperty("label").GetString()));
        Assert.All(steps, step => Assert.NotEqual(Guid.Empty, step.GetProperty("id").GetGuid()));
        Assert.NotEqual(steps[0].GetProperty("id").GetGuid(), steps[1].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_create_with_no_steps_has_none()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", Body());

        Assert.Empty((await StateOf(response)).GetProperty("routines")[0].GetProperty("steps").EnumerateArray());
    }

    [Fact]
    public async Task The_pages_PUT_with_no_steps_keeps_the_stored_steps_and_their_creation_times()
    {
        using var phone = await PhoneAsync();
        using var owner = Owner();
        var id = await CreateAsync(phone);
        var created = (await _store.RoutinesAsync(CancellationToken.None)).Single().Steps.Select(step => step.CreatedAt).ToList();

        // The page's body: every field but steps.
        using var response = await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body(hour: 6, label: "Morning"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var routine = (await StateOf(response)).GetProperty("routines")[0];
        Assert.Equal("Morning", routine.GetProperty("label").GetString());
        Assert.Equal([Meds, Beauty], routine.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid()));
        Assert.Equal(created, (await _store.RoutinesAsync(CancellationToken.None)).Single().Steps.Select(step => step.CreatedAt));

        // steps: null is the same as leaving it out.
        using var nulled = await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body(hour: 6, steps: "null"));
        Assert.Equal(2, (await StateOf(nulled)).GetProperty("routines")[0].GetProperty("steps").GetArrayLength());
    }

    [Fact]
    public async Task A_PUT_with_an_empty_list_removes_the_steps()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}", Body(steps: "[]"));

        Assert.Empty((await StateOf(response)).GetProperty("routines")[0].GetProperty("steps").EnumerateArray());
    }

    [Fact]
    public async Task A_PUT_with_steps_replaces_them_and_a_kept_id_keeps_its_creation_time()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        var medsCreated = (await _store.RoutinesAsync(CancellationToken.None)).Single().Steps[0].CreatedAt;
        _clock.Now = Noon + Duration.FromHours(2);
        var floss = Guid.NewGuid();

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}",
            Body(steps: $"[{{\"id\":\"{floss}\",\"label\":\"Floss\",\"offsetMinutes\":2}},{{\"id\":\"{Meds}\",\"label\":\"Meds\",\"offsetMinutes\":3}}]"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var steps = (await _store.RoutinesAsync(CancellationToken.None)).Single().Steps;
        Assert.Equal([floss, Meds], steps.Select(step => step.Id));
        Assert.Equal(_clock.Now, steps[0].CreatedAt);
        Assert.Equal(medsCreated, steps[1].CreatedAt);
        Assert.Equal("Meds", steps[1].Label);
    }

    public static IEnumerable<object[]> BadSteps =>
    [
        ["[" + string.Join(",", Enumerable.Range(0, 21).Select(_ => "{\"label\":\"x\",\"offsetMinutes\":1}")) + "]", "steps"],
        ["[{\"label\":\"a\",\"offsetMinutes\":1000},{\"label\":\"b\",\"offsetMinutes\":441}]", "steps"],
        [$"[{{\"id\":\"{Meds}\",\"offsetMinutes\":1}},{{\"id\":\"{Meds}\",\"offsetMinutes\":1}}]", "steps"],
        ["[{\"label\":\"a\",\"offsetMinutes\":1},{\"label\":\"" + new string('b', 61) + "\",\"offsetMinutes\":1}]", "steps[1].label"],
        ["[{\"label\":\"a\\u0007\",\"offsetMinutes\":1}]", "steps[0].label"],
        ["[{\"label\":\"a\",\"offsetMinutes\":-1}]", "steps[0].offsetMinutes"],
        ["[{\"label\":\"a\",\"offsetMinutes\":1441}]", "steps[0].offsetMinutes"],
        ["[{\"label\":\"a\"}]", "steps[0].offsetMinutes"],
    ];

    [Theory]
    [MemberData(nameof(BadSteps))]
    public async Task Each_bound_of_the_steps_is_refused_under_its_field(string steps, string field)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", Body(steps: steps));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await StateOf(response)).GetProperty("errors");
        Assert.Equal([field], errors.EnumerateObject().Select(error => error.Name));
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Twenty_steps_adding_up_to_a_day_are_taken()
    {
        using var owner = Owner();
        var steps = "[" + string.Join(",", Enumerable.Range(0, 20).Select(index => $"{{\"label\":\"s{index}\",\"offsetMinutes\":72}}")) + "]";

        using var response = await Send(owner, "POST", "/api/alerts/routines", Body(steps: steps));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ------------------------------------------------------------------ rings

    [Fact]
    public async Task Each_step_rings_its_cumulative_offset_after_the_routine_and_the_ring_names_its_step()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);

        var rings = (await RingsIn(phone)).Where(ring => ring.GetProperty("alertAt").GetDateTimeOffset() < (Noon + Duration.FromHours(1)).ToDateTimeOffset()).ToList();

        Assert.Equal(
            [(Key(id, "2026-10-28T12:00"), "Wake up", Noon, (Guid?)null),
             (Key(id, "2026-10-28T12:00", Meds), "Take medication", Noon + Duration.FromMinutes(5), Meds),
             (Key(id, "2026-10-28T12:00", Beauty), "Beauty care routine", Noon + Duration.FromMinutes(10), Beauty)],
            rings.Select(ring => (
                ring.GetProperty("key").GetString(),
                ring.GetProperty("label").GetString(),
                Instant.FromDateTimeOffset(ring.GetProperty("alertAt").GetDateTimeOffset()),
                ring.GetProperty("stepId").ValueKind == JsonValueKind.Null ? (Guid?)null : ring.GetProperty("stepId").GetGuid())));
        Assert.Equal(
            ["key", "routineId", "label", "alertAt", "startsAt", "acknowledged", "acknowledgedAt", "acknowledgedVia", "stepId"],
            rings[1].EnumerateObject().Select(field => field.Name));
        Assert.Equal(
            (Noon + Duration.FromMinutes(5) + Duration.FromHours(3)).ToDateTimeOffset(),
            rings[1].GetProperty("startsAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_step_key_carries_the_routines_scheduled_time_in_the_phones_zone()
    {
        using var phone = await PhoneAsync("Asia/Amman");
        _clock.Now = Noon - Duration.FromHours(1);
        // 23:58 in Amman is 20:58 UTC. The step 5 minutes later is past
        // midnight there, and keeps its routine's date.
        using var created = await Send(phone, "POST", "/api/alerts/routines",
            Body(hour: 23, minute: 58, steps: $"[{{\"id\":\"{Meds}\",\"label\":\"Meds\",\"offsetMinutes\":5}}]"));
        var id = (await StateOf(created)).GetProperty("routines")[0].GetProperty("id").GetGuid();
        _clock.Now = Noon;

        var step = (await RingsIn(phone)).First(ring => ring.GetProperty("stepId").ValueKind != JsonValueKind.Null);

        Assert.Equal(Key(id, "2026-10-28T23:58", Meds), step.GetProperty("key").GetString());
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 21, 3).ToDateTimeOffset(), step.GetProperty("alertAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_due_step_goes_to_Pushover_titled_with_its_label()
    {
        using var phone = await PhoneAsync();
        await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(5) + Duration.FromSeconds(5);

        await Worker.TickAsync(CancellationToken.None);

        // The routine's own ring at 12:00 and the step at 12:05. Not the one at 12:10.
        Assert.Equal(["Wake up", "Take medication"], Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task Stop_on_a_step_ring_is_an_acknowledgment_and_not_a_completion()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(5) + Duration.FromSeconds(5);
        await Worker.TickAsync(CancellationToken.None);
        var key = Key(id, "2026-10-28T12:00", Meds);

        using var response = await phone.PostAsJsonAsync("/api/alerts/ack", new { key, via = "phone" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        var ring = state.GetProperty("routineRings").EnumerateArray().Single(item => item.GetProperty("key").GetString() == key);
        Assert.True(ring.GetProperty("acknowledged").GetBoolean());
        Assert.Empty(state.GetProperty("routineCompletions").EnumerateArray());
        Assert.Equal($"/1/receipts/{Receipt}/cancel.json", Assert.Single(Cancels).Url.AbsolutePath);
    }

    // ------------------------------------------------------------------ completions

    [Fact]
    public async Task A_completed_step_is_not_listed_not_sent_to_Pushover_and_is_in_routineCompletions()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{\"note\":\"Took them\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        var keys = state.GetProperty("routineRings").EnumerateArray().Select(ring => ring.GetProperty("key").GetString()).ToList();
        Assert.DoesNotContain(Key(id, "2026-10-28T12:00", Meds), keys);
        Assert.Contains(Key(id, "2026-10-28T12:00", Beauty), keys);
        var completion = Assert.Single(state.GetProperty("routineCompletions").EnumerateArray());
        Assert.Equal(
            ["routineId", "stepId", "cycle", "completedAt", "id", "note", "hasPhoto"],
            completion.EnumerateObject().Select(field => field.Name));
        // The phone shows the note of a step checked off ahead, before its cycle reaches the history.
        Assert.Equal("Took them", completion.GetProperty("note").GetString());
        Assert.False(completion.GetProperty("hasPhoto").GetBoolean());
        Assert.NotEqual(Guid.Empty, completion.GetProperty("id").GetGuid());
        Assert.Equal(id, completion.GetProperty("routineId").GetGuid());
        Assert.Equal(Meds, completion.GetProperty("stepId").GetGuid());
        Assert.Equal("2026-10-28T12:00", completion.GetProperty("cycle").GetString());
        Assert.Equal(Noon.ToDateTimeOffset(), completion.GetProperty("completedAt").GetDateTimeOffset());
        Assert.True(await _devices.PlanVersionAsync(CancellationToken.None) > version);

        _clock.Now = Noon + Duration.FromMinutes(11);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Beauty care routine"], Messages.Select(sent => sent.Form["title"]));

        // The next cycle sets every step again.
        Assert.Contains(await RingsIn(phone), ring => ring.GetProperty("key").GetString() == Key(id, "2026-10-29T12:00", Meds));
    }

    [Fact]
    public async Task Undo_plans_the_step_again()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        var path = CompletionPath(id, Meds, "2026-10-28T12:00");
        await Send(phone, "PUT", path, "{}");

        using var response = await Send(phone, "DELETE", path, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        Assert.Contains(state.GetProperty("routineRings").EnumerateArray(),
            ring => ring.GetProperty("key").GetString() == Key(id, "2026-10-28T12:00", Meds));
        Assert.Empty(state.GetProperty("routineCompletions").EnumerateArray());
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Contains("Take medication", Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task Completing_a_step_while_it_rings_cancels_Pushovers_repeats()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Contains("Take medication", Messages.Select(sent => sent.Form["title"]));

        using var response = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/1/receipts/{Receipt}/cancel.json", Assert.Single(Cancels).Url.AbsolutePath);
    }

    [Fact]
    public async Task The_next_cycle_may_be_checked_off_early()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", CompletionPath(id, Beauty, "2026-10-29T12:00"), "{}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain((await StateOf(response)).GetProperty("routineRings").EnumerateArray(),
            ring => ring.GetProperty("key").GetString() == Key(id, "2026-10-29T12:00", Beauty));
    }

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

        using var response = await Send(phone, "PUT", CompletionPath(id, Meds, cycle), "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None));
    }

    [Fact]
    public async Task A_weekday_routine_has_no_cycle_on_the_weekend()
    {
        using var phone = await PhoneAsync();
        // Monday to Friday. 31 October 2026 is a Saturday, within the two days ahead.
        var id = await CreateAsync(phone, days: "[1,2,3,4,5]");

        using var saturday = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-31T12:00"), "{}");
        using var thursday = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-29T12:00"), "{}");

        Assert.Equal(HttpStatusCode.BadRequest, saturday.StatusCode);
        Assert.Equal(HttpStatusCode.OK, thursday.StatusCode);
    }

    [Fact]
    public async Task An_unknown_routine_or_step_is_404()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var routine = await Send(phone, "PUT", CompletionPath(Guid.NewGuid(), Meds, "2026-10-28T12:00"), "{}");
        using var step = await Send(phone, "PUT", CompletionPath(id, Guid.NewGuid(), "2026-10-28T12:00"), "{}");
        using var undo = await Send(phone, "DELETE", CompletionPath(Guid.NewGuid(), Meds, "2026-10-28T12:00"), null);

        Assert.Equal(HttpStatusCode.NotFound, routine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, step.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, undo.StatusCode);
    }

    public static IEnumerable<object[]> BadCompletions =>
    [
        ["{\"note\":\"" + new string('n', 2001) + "\"}", "note"],
        ["{\"photo\":\"not base64!\"}", "photo"],
        // A PNG's first bytes: not a JPEG, whatever the request says.
        ["{\"photo\":\"" + Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) + "\"}", "photo"],
        ["{\"photo\":\"" + Convert.ToBase64String([0xFF, 0xD8, .. new byte[RoutineCompletions.MaxPhotoBytes - 1]]) + "\"}", "photo"],
        ["{\"completedAt\":\"2026-10-28T12:06:00Z\"}", "completedAt"],
        ["{\"completedAt\":\"2026-10-14T11:59:00Z\"}", "completedAt"],
        ["{\"completedAt\":\"yesterday\"}", "completedAt"],
    ];

    [Theory]
    [MemberData(nameof(BadCompletions))]
    public async Task Each_bound_of_a_completion_is_refused_under_its_field(string body, string field)
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([field], (await StateOf(response)).GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.Empty(await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None));
    }

    [Fact]
    public async Task A_completion_time_a_few_minutes_ahead_of_the_server_is_taken()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"),
            "{\"completedAt\":\"2026-10-28T15:04:59+03:00\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Noon + Duration.FromSeconds(299),
            (await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None)).Single().CompletedAt);
    }

    [Fact]
    public async Task A_JPEG_is_stored_and_served_with_no_store_and_a_null_photo_keeps_it_and_empty_removes_it()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        var path = CompletionPath(id, Meds, "2026-10-28T12:00");

        await Send(phone, "PUT", path, $"{{\"note\":\"Done\",\"photo\":\"{Convert.ToBase64String(Jpeg)}\"}}");
        var completion = (await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None)).Single();
        Assert.True(completion.HasPhoto);

        using var photo = await phone.GetAsync($"/api/alerts/completions/{completion.Id}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await photo.Content.ReadAsByteArrayAsync());
        Assert.True(photo.Headers.CacheControl?.NoStore);

        // A second save with no photo keeps it, and the note is the new one.
        await Send(phone, "PUT", path, "{\"note\":\"Done twice\",\"photo\":null}");
        var kept = (await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None)).Single();
        Assert.Equal(completion.Id, kept.Id);
        Assert.True(kept.HasPhoto);
        Assert.Equal("Done twice", kept.Note);

        await Send(phone, "PUT", path, "{\"photo\":\"\"}");
        Assert.False((await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None)).Single().HasPhoto);
        using var gone = await phone.GetAsync($"/api/alerts/completions/{completion.Id}/photo");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task An_unknown_photo_is_404()
    {
        using var owner = Owner();

        using var response = await owner.GetAsync($"/api/alerts/completions/{Guid.NewGuid()}/photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ history

    [Fact]
    public async Task History_lists_each_cycle_newest_first_with_done_and_missed_steps_in_the_contract_shape()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(3);
        var id = await CreateAsync(phone);
        _clock.Now = Noon - Duration.FromDays(1) + Duration.FromMinutes(7);
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-27T12:00"),
            $"{{\"note\":\"With breakfast\",\"photo\":\"{Convert.ToBase64String(Jpeg)}\"}}");
        await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(id, "2026-10-27T12:00"), via = "phone" });
        _clock.Now = Noon + Duration.FromMinutes(1);

        using var response = await phone.GetAsync("/api/alerts/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await StateOf(response);
        Assert.Equal(["days", "cycles"], history.EnumerateObject().Select(field => field.Name));
        Assert.Equal(14, history.GetProperty("days").GetInt32());
        var cycles = history.GetProperty("cycles").EnumerateArray().ToList();
        // Created 25 October 11:00: the 25th, 26th, 27th and 28th. Not the 29th.
        Assert.Equal(
            ["2026-10-28T12:00", "2026-10-27T12:00", "2026-10-26T12:00", "2026-10-25T12:00"],
            cycles.Select(cycle => cycle.GetProperty("cycle").GetString()));
        var done = cycles[1];
        Assert.Equal(
            ["routineId", "label", "cycle", "at", "acknowledgedAt", "acknowledgedVia", "steps", "outcome"],
            done.EnumerateObject().Select(field => field.Name));
        // Neither Done nor Skipped (AlertRoutineOutcomeRouteTests).
        Assert.Equal(JsonValueKind.Null, done.GetProperty("outcome").ValueKind);
        Assert.Equal(id, done.GetProperty("routineId").GetGuid());
        Assert.Equal("Wake up", done.GetProperty("label").GetString());
        Assert.Equal((Noon - Duration.FromDays(1)).ToDateTimeOffset(), done.GetProperty("at").GetDateTimeOffset());
        Assert.Equal("phone", done.GetProperty("acknowledgedVia").GetString());
        var steps = done.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["stepId", "label", "at", "acknowledgedAt", "completion"], steps[0].EnumerateObject().Select(field => field.Name));
        Assert.Equal([Meds, Beauty], steps.Select(step => step.GetProperty("stepId").GetGuid()));
        Assert.Equal((Noon - Duration.FromDays(1) + Duration.FromMinutes(10)).ToDateTimeOffset(), steps[1].GetProperty("at").GetDateTimeOffset());
        var completion = steps[0].GetProperty("completion");
        Assert.Equal(["id", "completedAt", "note", "hasPhoto"], completion.EnumerateObject().Select(field => field.Name));
        Assert.Equal("With breakfast", completion.GetProperty("note").GetString());
        Assert.True(completion.GetProperty("hasPhoto").GetBoolean());
        // Beauty care was missed.
        Assert.Equal(JsonValueKind.Null, steps[1].GetProperty("completion").ValueKind);
        // The photo is at the id history gives.
        using var photo = await phone.GetAsync($"/api/alerts/completions/{completion.GetProperty("id").GetGuid()}/photo");
        Assert.Equal(Jpeg, await photo.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task History_keeps_a_completion_of_a_deleted_routine_by_the_labels_it_kept()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{\"note\":\"ok\"}");
        await Send(phone, "DELETE", $"/api/alerts/routines/{id}", null);

        var history = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history");

        var cycle = Assert.Single(history.GetProperty("cycles").EnumerateArray());
        Assert.Equal(id, cycle.GetProperty("routineId").GetGuid());
        Assert.Equal("Wake up", cycle.GetProperty("label").GetString());
        Assert.Equal(Noon.ToDateTimeOffset(), cycle.GetProperty("at").GetDateTimeOffset());
        var step = Assert.Single(cycle.GetProperty("steps").EnumerateArray());
        Assert.Equal("Take medication", step.GetProperty("label").GetString());
        Assert.Equal((Noon + Duration.FromMinutes(5)).ToDateTimeOffset(), step.GetProperty("at").GetDateTimeOffset());
        Assert.Equal("ok", step.GetProperty("completion").GetProperty("note").GetString());
    }

    [Fact]
    public async Task History_keeps_a_completion_of_a_deleted_step_beside_the_steps_that_remain()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");
        await Send(phone, "PUT", $"/api/alerts/routines/{id}", Body(steps: $"[{{\"id\":\"{Beauty}\",\"label\":\"Beauty care routine\",\"offsetMinutes\":10}}]"));

        var history = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history");

        var cycle = Assert.Single(history.GetProperty("cycles").EnumerateArray());
        Assert.Equal([Beauty, Meds], cycle.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("stepId").GetGuid()));
    }

    [Fact]
    public async Task History_lists_a_step_only_for_cycles_after_it_was_added()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(2);
        var id = await CreateAsync(phone, steps: $"[{{\"id\":\"{Meds}\",\"label\":\"Take medication\",\"offsetMinutes\":5}}]");
        _clock.Now = Noon - Duration.FromDays(1) + Duration.FromHours(1);
        await Send(phone, "PUT", $"/api/alerts/routines/{id}", Body(steps: TwoSteps));
        _clock.Now = Noon + Duration.FromHours(1);

        var history = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history");

        Assert.Equal(
            [("2026-10-28T12:00", 2), ("2026-10-27T12:00", 1), ("2026-10-26T12:00", 1)],
            history.GetProperty("cycles").EnumerateArray()
                .Select(cycle => (cycle.GetProperty("cycle").GetString(), cycle.GetProperty("steps").GetArrayLength())));
    }

    [Fact]
    public async Task History_has_no_routine_without_steps_and_no_cycle_older_than_14_days()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(30);
        await CreateAsync(phone);
        await CreateAsync(phone, steps: "[]", hour: 7);
        _clock.Now = Noon;

        var cycles = (await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history")).GetProperty("cycles").EnumerateArray().ToList();

        Assert.Equal(15, cycles.Count);
        Assert.Equal("2026-10-28T12:00", cycles[0].GetProperty("cycle").GetString());
        Assert.Equal("2026-10-14T12:00", cycles[^1].GetProperty("cycle").GetString());
        Assert.All(cycles, cycle => Assert.Equal("Wake up", cycle.GetProperty("label").GetString()));
    }

    // ------------------------------------------------------------------ retention

    [Fact]
    public async Task A_pass_deletes_completions_whose_cycle_is_older_than_14_days()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(20);
        var id = await CreateAsync(phone);
        _clock.Now = Noon - Duration.FromDays(13);
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-15T12:00"), $"{{\"photo\":\"{Convert.ToBase64String(Jpeg)}\"}}");
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-14T12:00"), "{}");
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-16T12:00"), "{}");
        Assert.Equal(3, (await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None)).Count);
        _clock.Now = Noon + Duration.FromDays(1) + Duration.FromHours(1);

        await Worker.ReadNowAsync(CancellationToken.None);

        // 15 October 12:00 is 14 days and 1 hour before now: gone with its
        // photo. 16 October is 13 days back and stays.
        Assert.Equal(
            ["2026-10-16T12:00"],
            (await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None)).Select(completion => completion.Cycle));
    }

    // ------------------------------------------------------------------ one-time routines

    [Fact]
    public async Task A_one_time_routine_with_steps_stays_after_its_Stop_and_goes_once_every_step_is_checked_off()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, days: "[]");
        _clock.Now = Noon + Duration.FromSeconds(5);

        using var stopped = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(id, "2026-10-28T12:00"), via = "phone" });
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
        Assert.Contains((await StateOf(stopped)).GetProperty("routineRings").EnumerateArray(),
            ring => ring.GetProperty("key").GetString() == Key(id, "2026-10-28T12:00", Meds));

        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
        _clock.Now = Noon + Duration.FromMinutes(10) + Duration.FromSeconds(5);
        using var last = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(id, "2026-10-28T12:00", Beauty), via = "phone" });

        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_one_time_routine_with_steps_goes_once_its_last_step_has_stopped_ringing()
    {
        using var phone = await PhoneAsync();
        await CreateAsync(phone, days: "[]");

        // 12:10 plus the 180 minutes a ring lasts.
        _clock.Now = Noon + Duration.FromMinutes(10) + Duration.FromHours(3) - Duration.FromSeconds(1);
        await Worker.ReadNowAsync(CancellationToken.None);
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));

        _clock.Now = Noon + Duration.FromMinutes(10) + Duration.FromHours(3);
        var version = await _devices.PlanVersionAsync(CancellationToken.None);
        await Worker.ReadNowAsync(CancellationToken.None);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
        Assert.True(await _devices.PlanVersionAsync(CancellationToken.None) > version);
    }

    [Fact]
    public async Task A_one_time_routine_whose_steps_are_checked_off_before_it_rings_stays()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromHours(2);
        var id = await CreateAsync(phone, days: "[]");
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");
        await Send(phone, "PUT", CompletionPath(id, Beauty, "2026-10-28T12:00"), "{}");

        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ who may call

    public static IEnumerable<object?[]> StepRoutes =>
    [
        ["PUT", $"/api/alerts/routines/{Guid.Empty}/steps/{Meds}/cycles/2026-10-28T12:00/completion", "{}"],
        ["DELETE", $"/api/alerts/routines/{Guid.Empty}/steps/{Meds}/cycles/2026-10-28T12:00/completion", null],
        ["GET", "/api/alerts/history", null],
        ["GET", $"/api/alerts/completions/{Guid.Empty}/photo", null],
    ];

    [Theory]
    [MemberData(nameof(StepRoutes))]
    public async Task No_cookie_and_no_token_is_401_and_the_refusal_is_logged(string method, string path, string? body)
    {
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(_logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.SignInRequired);
        Assert.Empty(await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None));
    }

    [Theory]
    [MemberData(nameof(StepRoutes))]
    public async Task A_strangers_Google_account_is_403(string method, string path, string? body)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, "stranger@example.test");

        using var response = await Send(stranger, method, path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(StepRoutes))]
    public async Task A_wrong_phone_token_is_401(string method, string path, string? body)
    {
        using var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", "aat_" + new string('A', 43));

        using var response = await Send(phone, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(StepRoutes))]
    public async Task A_paired_phone_and_the_owner_reach_every_step_route(string method, string path, string? body)
    {
        using var phone = await PhoneAsync();
        using var owner = Owner();

        using var fromPhone = await Send(phone, method, path, body);
        using var fromOwner = await Send(owner, method, path, body);

        Assert.DoesNotContain(fromPhone.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.DoesNotContain(fromOwner.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    // ------------------------------------------------------------------ body size

    [Fact]
    public async Task Only_the_completion_route_takes_a_body_past_the_global_ceiling_and_it_stops_at_3_MB()
    {
        using var app = App(kestrel: true);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);
        using var created = await Send(owner, "POST", "/api/alerts/routines", Body(steps: TwoSteps));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await StateOf(created)).GetProperty("routines")[0].GetProperty("id").GetGuid();
        var path = CompletionPath(id, Meds, "2026-10-28T12:00");

        // A 1.9 MB JPEG is about 2.5 MB of base64: past the 64 KB ceiling, under 3 MB.
        var photo = Convert.ToBase64String([.. Jpeg, .. new byte[1_900_000]]);
        using var taken = await Send(owner, "PUT", path, $"{{\"photo\":\"{photo}\"}}");
        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);

        // Expect: 100-continue, so the refusal arrives before the body is sent
        // and the connection is not reset under it.
        using var oversized = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StringContent($"{{\"note\":\"{new string('n', 3_200_000)}\"}}", Encoding.UTF8, "application/json"),
            Headers = { ExpectContinue = true }
        };
        using var tooBig = await owner.SendAsync(oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);

        // Every other alerts route keeps the global ceiling.
        using var routine = await Send(owner, "PUT", $"/api/alerts/routines/{id}", Body(label: new string('a', 100_000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, routine.StatusCode);
    }

    [Fact]
    public async Task A_body_that_is_not_JSON_is_refused()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var request = new HttpRequestMessage(HttpMethod.Put, CompletionPath(id, Meds, "2026-10-28T12:00"))
        {
            Content = new StringContent("note=hi", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        using var form = await phone.SendAsync(request);
        using var broken = await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{\"note\":");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, form.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.Empty(await _store.CompletionsAsync(Instant.MinValue, CancellationToken.None));
    }
}
