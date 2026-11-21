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
/// Done and Skipped for one ring of a routine alarm, from every entry point:
/// the outcome PUT and DELETE from the phone and the page, the status's
/// rings and outcomes, the worker's sends to Pushover and their repeats,
/// the history, the one-time routine marked ahead, and the 14 days kept.
/// Neb asked for this on 2026-11-21: he woke at 06:40, did his routine
/// before the 07:00 alarm, and had to switch the alarm off and on again.
/// </summary>
public sealed class AlertRoutineOutcomeRouteTests : IDisposable
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
        $"{{\"status\":1,\"request\":\"r\",\"receipt\":\"rcptoutcomes{Interlocked.Increment(ref _receipts):D16}\"}}",
        "application/json"));

    private readonly RecordingHandler _calendar = new(() => RecordingHandler.Text(
        HttpStatusCode.OK,
        Ics(Event("standup@google.com", "Standup #critical", "20261028T090000", "20261028T093000", "Room 1", alarms: [Popup("-PT15M")])),
        "text/calendar"));

    private readonly TestApp _app;

    public AlertRoutineOutcomeRouteTests()
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

    // ------------------------------------------------------------------ Done

    [Fact]
    public async Task Done_ahead_drops_the_routines_ring_keeps_its_steps_and_is_in_routineOutcomes_in_the_contract_shape()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\",\"note\":\" Up at 11:10 \"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        var keys = Keys(state);
        Assert.DoesNotContain(Key(id, "2026-10-28T12:00"), keys);
        Assert.Contains(Key(id, "2026-10-28T12:00", Meds), keys);
        Assert.Contains(Key(id, "2026-10-28T12:00", Beauty), keys);
        var outcome = Assert.Single(state.GetProperty("routineOutcomes").EnumerateArray());
        Assert.Equal(["routineId", "cycle", "status", "at", "note"], outcome.EnumerateObject().Select(field => field.Name));
        Assert.Equal(id, outcome.GetProperty("routineId").GetGuid());
        Assert.Equal("2026-10-28T12:00", outcome.GetProperty("cycle").GetString());
        Assert.Equal("done", outcome.GetProperty("status").GetString());
        Assert.Equal((Noon - Duration.FromMinutes(30)).ToDateTimeOffset(), outcome.GetProperty("at").GetDateTimeOffset());
        Assert.Equal("Up at 11:10", outcome.GetProperty("note").GetString());
        Assert.True(await _devices.PlanVersionAsync(CancellationToken.None) > version);

        _clock.Now = Noon + Duration.FromMinutes(11);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Take medication", "Beauty care routine"], Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task Done_on_a_ring_ringing_now_cancels_its_Pushover_repeats_and_not_its_steps()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Take medication"], Messages.Select(sent => sent.Form["title"]));

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Cancels);
        var keys = await KeysIn(response);
        Assert.DoesNotContain(Key(id, "2026-10-28T12:00"), keys);
        Assert.Contains(Key(id, "2026-10-28T12:00", Meds), keys);
    }

    // ------------------------------------------------------------------ Skipped

    [Fact]
    public async Task Skipped_drops_the_routines_ring_and_every_step_ring_of_that_cycle_only()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"skipped\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        Assert.Empty(Keys(state));
        Assert.Equal("skipped", Assert.Single(state.GetProperty("routineOutcomes").EnumerateArray()).GetProperty("status").GetString());

        _clock.Now = Noon + Duration.FromMinutes(11);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Empty(Messages);

        // The next cycle rings, steps and all.
        _clock.Now = Noon + Duration.FromHours(13);
        var next = (await RingsIn(phone)).Select(ring => ring.GetProperty("key").GetString()).ToList();
        Assert.Equal([Key(id, "2026-10-29T12:00"), Key(id, "2026-10-29T12:00", Meds), Key(id, "2026-10-29T12:00", Beauty)], next);
    }

    [Fact]
    public async Task Skipped_while_a_step_rings_cancels_the_repeats_of_the_ring_and_the_step()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"skipped\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, Cancels.Count);
        Assert.DoesNotContain(await KeysIn(response), key => key!.StartsWith(Key(id, "2026-10-28T12:00"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_second_outcome_replaces_the_first()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\",\"note\":\"first\"}");

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"),
            "{\"status\":\"skipped\",\"at\":\"2026-10-28T14:00:00+03:00\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = Assert.Single(await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None));
        Assert.Equal(("skipped", (string?)null, Noon - Duration.FromHours(1)), (outcome.Status, outcome.Note, outcome.At));
        Assert.DoesNotContain(Key(id, "2026-10-28T12:00", Meds), await KeysIn(response));
    }

    // ------------------------------------------------------------------ undo

    [Theory]
    [InlineData("done")]
    [InlineData("skipped")]
    public async Task Undo_plans_the_ring_and_its_steps_again(string status)
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromMinutes(30);
        var id = await CreateAsync(phone);
        var path = OutcomePath(id, "2026-10-28T12:00");
        await Send(phone, "PUT", path, $"{{\"status\":\"{status}\"}}");
        var version = await _devices.PlanVersionAsync(CancellationToken.None);

        using var response = await Send(phone, "DELETE", path, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        var keys = Keys(state);
        Assert.Contains(Key(id, "2026-10-28T12:00"), keys);
        Assert.Contains(Key(id, "2026-10-28T12:00", Meds), keys);
        Assert.Empty(state.GetProperty("routineOutcomes").EnumerateArray());
        Assert.True(await _devices.PlanVersionAsync(CancellationToken.None) > version);
        _clock.Now = Noon + Duration.FromMinutes(6);
        await Worker.TickAsync(CancellationToken.None);
        Assert.Equal(["Wake up", "Take medication"], Messages.Select(sent => sent.Form["title"]));
    }

    [Fact]
    public async Task Undo_of_a_deleted_routines_outcome_works_and_an_unknown_routine_is_404()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);
        await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\"}");
        await Send(phone, "DELETE", $"/api/alerts/routines/{id}", null);

        using var undo = await Send(phone, "DELETE", OutcomePath(id, "2026-10-28T12:00"), null);
        using var again = await Send(phone, "DELETE", OutcomePath(id, "2026-10-28T12:00"), null);

        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        Assert.Empty(await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
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

        using var response = await Send(phone, "PUT", OutcomePath(id, cycle), "{\"status\":\"done\"}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None));
    }

    [Fact]
    public async Task The_next_cycle_may_be_marked_and_an_unknown_routine_is_404()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, days: "[1,2,3,4,5]");

        using var thursday = await Send(phone, "PUT", OutcomePath(id, "2026-10-29T12:00"), "{\"status\":\"done\"}");
        using var saturday = await Send(phone, "PUT", OutcomePath(id, "2026-10-31T12:00"), "{\"status\":\"done\"}");
        using var unknown = await Send(phone, "PUT", OutcomePath(Guid.NewGuid(), "2026-10-28T12:00"), "{\"status\":\"done\"}");

        Assert.Equal(HttpStatusCode.OK, thursday.StatusCode);
        Assert.DoesNotContain(Key(id, "2026-10-29T12:00"), await KeysIn(thursday));
        Assert.Equal(HttpStatusCode.BadRequest, saturday.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    public static IEnumerable<object[]> BadOutcomes =>
    [
        ["{}", "status"],
        ["{\"status\":\"Done\"}", "status"],
        ["{\"status\":\"missed\"}", "status"],
        ["{\"status\":\"done\",\"note\":\"" + new string('n', 2001) + "\"}", "note"],
        ["{\"status\":\"done\",\"at\":\"2026-10-28T12:06:00Z\"}", "at"],
        ["{\"status\":\"done\",\"at\":\"2026-10-14T11:59:00Z\"}", "at"],
        ["{\"status\":\"done\",\"at\":\"yesterday\"}", "at"],
    ];

    [Theory]
    [MemberData(nameof(BadOutcomes))]
    public async Task Each_bound_of_an_outcome_is_refused_under_its_field(string body, string field)
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([field], (await StateOf(response)).GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.Empty(await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None));
    }

    [Fact]
    public async Task A_body_that_is_not_JSON_is_refused()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone);

        using var request = new HttpRequestMessage(HttpMethod.Put, OutcomePath(id, "2026-10-28T12:00"))
        {
            Content = new StringContent("status=done", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        using var form = await phone.SendAsync(request);
        using var broken = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, form.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.Empty(await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None));
    }

    // ------------------------------------------------------------------ history

    [Fact]
    public async Task History_shows_each_cycles_outcome_and_a_later_cycles_once_its_ring_time_comes()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(3);
        var id = await CreateAsync(phone);
        _clock.Now = Noon - Duration.FromDays(1) - Duration.FromMinutes(20);
        await Send(phone, "PUT", OutcomePath(id, "2026-10-27T12:00"), "{\"status\":\"done\",\"note\":\"Up early\"}");
        await Send(phone, "PUT", OutcomePath(id, "2026-10-26T12:00"), "{\"status\":\"skipped\"}");
        _clock.Now = Noon + Duration.FromMinutes(1);
        await Send(phone, "PUT", OutcomePath(id, "2026-10-29T12:00"), "{\"status\":\"done\"}");

        var history = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history");

        var cycles = history.GetProperty("cycles").EnumerateArray().ToList();
        Assert.Equal(
            ["2026-10-28T12:00", "2026-10-27T12:00", "2026-10-26T12:00", "2026-10-25T12:00"],
            cycles.Select(cycle => cycle.GetProperty("cycle").GetString()));
        Assert.Equal(
            ["routineId", "label", "cycle", "at", "acknowledgedAt", "acknowledgedVia", "steps", "outcome"],
            cycles[1].EnumerateObject().Select(field => field.Name));
        var done = cycles[1].GetProperty("outcome");
        Assert.Equal(["status", "at", "note"], done.EnumerateObject().Select(field => field.Name));
        Assert.Equal("done", done.GetProperty("status").GetString());
        Assert.Equal((Noon - Duration.FromDays(1) - Duration.FromMinutes(20)).ToDateTimeOffset(), done.GetProperty("at").GetDateTimeOffset());
        Assert.Equal("Up early", done.GetProperty("note").GetString());
        Assert.Equal("skipped", cycles[2].GetProperty("outcome").GetProperty("status").GetString());
        // The steps of a skipped cycle keep no completion: the phone shows them skipped.
        Assert.All(cycles[2].GetProperty("steps").EnumerateArray(), step => Assert.Equal(JsonValueKind.Null, step.GetProperty("completion").ValueKind));
        Assert.Equal(JsonValueKind.Null, cycles[0].GetProperty("outcome").ValueKind);

        _clock.Now = Noon + Duration.FromDays(1) + Duration.FromMinutes(1);
        var later = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history");
        var first = later.GetProperty("cycles")[0];
        Assert.Equal("2026-10-29T12:00", first.GetProperty("cycle").GetString());
        Assert.Equal("done", first.GetProperty("outcome").GetProperty("status").GetString());
    }

    [Fact]
    public async Task History_lists_an_outcome_of_a_routine_without_steps_and_of_a_deleted_routine_by_its_label()
    {
        using var phone = await PhoneAsync();
        var plain = await CreateAsync(phone, steps: "[]", hour: 7);
        var gone = await CreateAsync(phone);
        _clock.Now = Noon + Duration.FromMinutes(1);
        await Send(phone, "PUT", OutcomePath(plain, "2026-10-28T07:00"), "{\"status\":\"done\"}");
        await Send(phone, "PUT", OutcomePath(gone, "2026-10-28T12:00"), "{\"status\":\"skipped\"}");
        await Send(phone, "DELETE", $"/api/alerts/routines/{gone}", null);

        var cycles = (await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history")).GetProperty("cycles").EnumerateArray().ToList();

        Assert.Equal(
            [(gone, "2026-10-28T12:00", "skipped", 0), (plain, "2026-10-28T07:00", "done", 0)],
            cycles.Select(cycle => (
                cycle.GetProperty("routineId").GetGuid(),
                cycle.GetProperty("cycle").GetString(),
                cycle.GetProperty("outcome").GetProperty("status").GetString(),
                cycle.GetProperty("steps").GetArrayLength())));
        Assert.All(cycles, cycle => Assert.Equal("Wake up", cycle.GetProperty("label").GetString()));
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 7, 0).ToDateTimeOffset(), cycles[1].GetProperty("at").GetDateTimeOffset());
    }

    // ------------------------------------------------------------------ one-time routines

    [Fact]
    public async Task A_one_time_routine_marked_Done_ahead_goes_once_its_steps_are_checked_off()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromHours(2);
        var id = await CreateAsync(phone, days: "[]");

        await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\"}");
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
        await Send(phone, "PUT", CompletionPath(id, Meds, "2026-10-28T12:00"), "{}");
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
        using var last = await Send(phone, "PUT", CompletionPath(id, Beauty, "2026-10-28T12:00"), "{}");

        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
        Assert.Empty((await StateOf(last)).GetProperty("routineRings").EnumerateArray());
    }

    [Fact]
    public async Task A_one_time_routine_marked_Skipped_ahead_goes_at_once()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromHours(2);
        var id = await CreateAsync(phone, days: "[]");

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"skipped\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
        Assert.Empty((await StateOf(response)).GetProperty("routines").EnumerateArray());
    }

    [Fact]
    public async Task A_one_time_routine_without_steps_marked_Done_ahead_goes_at_once()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromHours(2);
        var id = await CreateAsync(phone, days: "[]", steps: "[]");

        using var response = await Send(phone, "PUT", OutcomePath(id, "2026-10-28T12:00"), "{\"status\":\"done\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ retention

    [Fact]
    public async Task A_pass_deletes_outcomes_whose_cycle_is_older_than_14_days()
    {
        using var phone = await PhoneAsync();
        _clock.Now = Noon - Duration.FromDays(20);
        var id = await CreateAsync(phone);
        _clock.Now = Noon - Duration.FromDays(13);
        await Send(phone, "PUT", OutcomePath(id, "2026-10-14T12:00"), "{\"status\":\"done\"}");
        await Send(phone, "PUT", OutcomePath(id, "2026-10-15T12:00"), "{\"status\":\"skipped\"}");
        await Send(phone, "PUT", OutcomePath(id, "2026-10-16T12:00"), "{\"status\":\"done\"}");
        Assert.Equal(3, (await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None)).Count);
        _clock.Now = Noon + Duration.FromDays(1) + Duration.FromHours(1);

        await Worker.ReadNowAsync(CancellationToken.None);

        Assert.Equal(
            ["2026-10-16T12:00"],
            (await _store.OutcomesAsync(Instant.MinValue, CancellationToken.None)).Select(outcome => outcome.Cycle));
    }

    // ------------------------------------------------------------------ who may call

    public static IEnumerable<object?[]> OutcomeRoutes =>
    [
        ["PUT", $"/api/alerts/routines/{Guid.Empty}/cycles/2026-10-28T12:00/outcome", "{\"status\":\"done\"}"],
        ["DELETE", $"/api/alerts/routines/{Guid.Empty}/cycles/2026-10-28T12:00/outcome", null],
    ];

    [Theory]
    [MemberData(nameof(OutcomeRoutes))]
    public async Task No_cookie_and_no_token_is_401_and_the_refusal_is_logged(string method, string path, string? body)
    {
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(_logs.InCategory(SecurityEvents.Category), entry => entry.EventId.Id == SecurityEvents.SignInRequired);
    }

    [Theory]
    [MemberData(nameof(OutcomeRoutes))]
    public async Task A_strangers_Google_account_is_403(string method, string path, string? body)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, "stranger@example.test");

        using var response = await Send(stranger, method, path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OutcomeRoutes))]
    public async Task A_wrong_phone_token_is_401(string method, string path, string? body)
    {
        using var phone = _app.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new("Bearer", "aat_" + new string('A', 43));

        using var response = await Send(phone, method, path, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OutcomeRoutes))]
    public async Task A_paired_phone_and_the_owner_reach_both_outcome_routes(string method, string path, string? body)
    {
        using var phone = await PhoneAsync();
        using var owner = Owner();

        using var fromPhone = await Send(phone, method, path, body);
        using var fromOwner = await Send(owner, method, path, body);

        Assert.Equal(HttpStatusCode.NotFound, fromPhone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fromOwner.StatusCode);
    }
}
