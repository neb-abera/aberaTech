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
/// How a routine alarm repeats, from the routes: repeat, every, unit and
/// startsOn in every answer, their defaults on a create, an update that
/// leaves them out keeping them, each refusal, the rings of an interval in
/// the status, and an interval ring's stamp taken by the outcome, delay,
/// completion and history routes. An interval is never deleted as a
/// one-time alarm. Neb asked on 2026-11-25 for a number and a unit: every
/// 97 minutes, every other week.
/// </summary>
public sealed class AlertRoutineRepeatRouteTests : IDisposable
{
    /// <summary>Wednesday 28 October 2026, 12:00 UTC.</summary>
    private static readonly Instant Noon = Instant.FromUtc(2026, 10, 28, 12, 0);

    private static readonly Guid Meds = Guid.Parse("5a1e0000-0000-4000-8000-000000000001");

    private static readonly string OneStep = $"[{{\"id\":\"{Meds}\",\"label\":\"Take medication\",\"offsetMinutes\":5}}]";

    private readonly FakeClock _clock = new(Noon);
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

    public AlertRoutineRepeatRouteTests()
    {
        var settings = AlertsRouteTests.Settings();
        settings["Alerts:TimeZone"] = "UTC";
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
        phone.DefaultRequestHeaders.Add(AlertDeviceZones.Header, "UTC");
        return phone;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> StateOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> RoutineIn(HttpResponseMessage response) =>
        (await StateOf(response)).GetProperty("routines").EnumerateArray().Single();

    private static (string? Repeat, int Every, string? Unit, string? StartsOn) RepeatOf(JsonElement routine) => (
        routine.GetProperty("repeat").GetString(),
        routine.GetProperty("every").GetInt32(),
        routine.GetProperty("unit").GetString(),
        routine.GetProperty("startsOn").GetString());

    /// <summary>The body an older phone build sends: none of the four fields.</summary>
    private static string Old(string days = "[]", int hour = 12, int minute = 30, bool enabled = true) =>
        $"{{\"label\":\"Meds\",\"hour\":{hour},\"minute\":{minute},\"days\":{days},\"enabled\":{(enabled ? "true" : "false")},\"snoozeMinutes\":9}}";

    private static string Interval(
        int every = 97, string unit = "minutes", string startsOn = "2026-10-28", int hour = 12, int minute = 30, string? steps = null)
    {
        var tail = steps is null ? "" : $",\"steps\":{steps}";
        return $"{{\"label\":\"Meds\",\"hour\":{hour},\"minute\":{minute},\"days\":[],\"enabled\":true,\"snoozeMinutes\":9,"
               + $"\"repeat\":\"interval\",\"every\":{every},\"unit\":\"{unit}\",\"startsOn\":\"{startsOn}\"{tail}}}";
    }

    /// <summary>Creates a routine an hour before the clock's time and answers its id.</summary>
    private async Task<Guid> CreateAsync(HttpClient client, string body)
    {
        var saved = _clock.Now;
        _clock.Now = saved - Duration.FromHours(1);
        using var response = await Send(client, "POST", "/api/alerts/routines", body);
        _clock.Now = saved;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await StateOf(response)).GetProperty("routines").EnumerateArray()
            .Select(routine => routine.GetProperty("id").GetGuid())
            .Single(id => _created.Add(id));
    }

    private readonly HashSet<Guid> _created = [];

    private static string Key(Guid routine, string cycle, Guid? step = null) =>
        step is { } id ? $"routine:{routine:D}:{cycle}:step:{id:D}" : $"routine:{routine:D}:{cycle}";

    private static List<string?> Keys(JsonElement state) =>
        [.. state.GetProperty("routineRings").EnumerateArray().Select(ring => ring.GetProperty("key").GetString())];

    // ------------------------------------------------------------------ the fields

    [Fact]
    public async Task A_create_without_them_answers_the_defaults_and_every_routine_lists_exactly_the_contract_fields()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", "{\"hour\":6,\"minute\":30,\"days\":[1,3,5]}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var routine = await RoutineIn(response);
        Assert.Equal(
            ["id", "label", "hour", "minute", "days", "repeat", "every", "unit", "startsOn", "enabled", "snoozeMinutes", "updatedAt", "steps"],
            routine.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("weekdays", 1, "weeks", null), RepeatOf(routine));
        // The field is there and null, never left out.
        Assert.Equal(JsonValueKind.Null, routine.GetProperty("startsOn").ValueKind);
    }

    [Fact]
    public async Task A_create_every_97_minutes_stores_the_four_fields_and_the_status_lists_them()
    {
        using var phone = await PhoneAsync();

        using var response = await Send(phone, "POST", "/api/alerts/routines", Interval());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(("interval", 97, "minutes", "2026-10-28"), RepeatOf(await RoutineIn(response)));
        var status = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        Assert.Equal(("interval", 97, "minutes", "2026-10-28"), RepeatOf(status.GetProperty("routines").EnumerateArray().Single()));
        var stored = Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
        Assert.Equal(new LocalDate(2026, 10, 28), stored.StartsOn);
        Assert.Empty(stored.Days);
    }

    [Theory]
    [InlineData("minutes")]
    [InlineData("hours")]
    [InlineData("days")]
    [InlineData("weeks")]
    [InlineData("months")]
    [InlineData("years")]
    public async Task Each_unit_is_taken_at_both_bounds_of_every(string unit)
    {
        using var owner = Owner();

        using var one = await Send(owner, "POST", "/api/alerts/routines", Interval(every: 1, unit: unit));
        using var most = await Send(owner, "POST", "/api/alerts/routines", Interval(every: 999, unit: unit));

        Assert.Equal(HttpStatusCode.Created, one.StatusCode);
        Assert.Equal(HttpStatusCode.Created, most.StatusCode);
    }

    [Fact]
    public async Task Weekdays_every_2_weeks_keep_their_days_and_start_date()
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            "{\"hour\":6,\"minute\":30,\"days\":[5,1,3],\"repeat\":\"weekdays\",\"every\":2,\"unit\":\"weeks\",\"startsOn\":\"2026-10-28\"}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var routine = await RoutineIn(response);
        Assert.Equal(("weekdays", 2, "weeks", "2026-10-28"), RepeatOf(routine));
        Assert.Equal([1, 3, 5], routine.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
    }

    [Theory]
    [InlineData("[1,3,5]")]
    [InlineData("[]")]
    public async Task Weekdays_every_week_ignore_a_start_date_and_store_none(string days)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines",
            $"{{\"hour\":6,\"minute\":30,\"days\":{days},\"repeat\":\"weekdays\",\"every\":1,\"unit\":\"weeks\",\"startsOn\":\"2026-10-28\"}}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(("weekdays", 1, "weeks", null), RepeatOf(await RoutineIn(response)));
        Assert.Null(Assert.Single(await _store.RoutinesAsync(CancellationToken.None)).StartsOn);
    }

    [Theory]
    [InlineData("{\"hour\":6,\"minute\":30,\"days\":[1,3],\"startsOn\":null}")]
    [InlineData("{\"hour\":6,\"minute\":30,\"days\":[1,3]}")]
    [InlineData("{\"hour\":6,\"minute\":30,\"days\":[],\"repeat\":null,\"every\":null,\"unit\":null,\"startsOn\":null}")]
    public async Task A_field_left_out_and_one_sent_as_null_are_taken_alike(string body)
    {
        using var phone = await PhoneAsync();

        using var response = await Send(phone, "POST", "/api/alerts/routines", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(("weekdays", 1, "weeks", null), RepeatOf(await RoutineIn(response)));
    }

    [Fact]
    public async Task An_update_with_startsOn_null_keeps_the_stored_date_as_one_left_out_does()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval());

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}",
            "{\"label\":\"Meds\",\"hour\":12,\"minute\":30,\"days\":[],\"enabled\":true,\"snoozeMinutes\":9,"
            + "\"repeat\":\"interval\",\"every\":97,\"unit\":\"minutes\",\"startsOn\":null}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("interval", 97, "minutes", "2026-10-28"), RepeatOf(await RoutineIn(response)));
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task An_update_without_them_keeps_the_stored_values_so_an_older_phone_erases_nothing()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval());

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}", Old(hour: 13, enabled: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var routine = await RoutineIn(response);
        Assert.Equal(("interval", 97, "minutes", "2026-10-28"), RepeatOf(routine));
        Assert.Equal(13, routine.GetProperty("hour").GetInt32());
        Assert.False(routine.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task An_update_without_them_keeps_weekdays_every_2_weeks_and_its_start_date()
    {
        using var owner = Owner();
        var id = await CreateAsync(owner,
            "{\"hour\":6,\"minute\":30,\"days\":[1,3],\"repeat\":\"weekdays\",\"every\":2,\"unit\":\"weeks\",\"startsOn\":\"2026-10-28\"}");

        using var response = await Send(owner, "PUT", $"/api/alerts/routines/{id}", Old(days: "[2,4]"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var routine = await RoutineIn(response);
        Assert.Equal(("weekdays", 2, "weeks", "2026-10-28"), RepeatOf(routine));
        Assert.Equal([2, 4], routine.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
    }

    [Fact]
    public async Task An_update_with_them_replaces_them_and_one_field_alone_changes_that_field()
    {
        using var owner = Owner();
        var id = await CreateAsync(owner, Interval());

        using var hours = await Send(owner, "PUT", $"/api/alerts/routines/{id}",
            "{\"label\":\"Meds\",\"hour\":12,\"minute\":30,\"days\":[],\"enabled\":true,\"snoozeMinutes\":9,\"every\":4,\"unit\":\"hours\"}");
        Assert.Equal(("interval", 4, "hours", "2026-10-28"), RepeatOf(await RoutineIn(hours)));

        using var weeks = await Send(owner, "PUT", $"/api/alerts/routines/{id}",
            "{\"label\":\"Meds\",\"hour\":12,\"minute\":30,\"days\":[1,3],\"enabled\":true,\"snoozeMinutes\":9,"
            + "\"repeat\":\"weekdays\",\"every\":2,\"unit\":\"weeks\",\"startsOn\":\"2026-11-02\"}");
        Assert.Equal(HttpStatusCode.OK, weeks.StatusCode);
        Assert.Equal(("weekdays", 2, "weeks", "2026-11-02"), RepeatOf(await RoutineIn(weeks)));

        // Back to ringing once: the start date goes.
        using var once = await Send(owner, "PUT", $"/api/alerts/routines/{id}",
            "{\"label\":\"Meds\",\"hour\":12,\"minute\":30,\"days\":[],\"enabled\":true,\"snoozeMinutes\":9,"
            + "\"repeat\":\"weekdays\",\"every\":1,\"unit\":\"weeks\"}");
        Assert.Equal(HttpStatusCode.OK, once.StatusCode);
        Assert.Equal(("weekdays", 1, "weeks", null), RepeatOf(await RoutineIn(once)));
    }

    [Fact]
    public async Task An_older_phone_that_puts_days_on_an_interval_is_refused_and_nothing_changes()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval());

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}", Old(days: "[1,3]"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await StateOf(response)).GetProperty("errors").TryGetProperty("days", out _));
        var stored = Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
        Assert.Equal((RoutineRepeats.Interval, 0), (stored.Repeat, stored.Days.Count));
    }

    [Fact]
    public async Task A_bad_update_of_an_unknown_id_is_still_400_and_a_good_one_404()
    {
        using var owner = Owner();

        using var bad = await Send(owner, "PUT", $"/api/alerts/routines/{Guid.NewGuid()}", Interval(every: 0));
        using var good = await Send(owner, "PUT", $"/api/alerts/routines/{Guid.NewGuid()}", Interval());

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, good.StatusCode);
    }

    // ------------------------------------------------------------------ refusals

    private const string Base = "\"hour\":6,\"minute\":30";

    /// <summary>Each rule of the contract, and the field its refusal names.</summary>
    public static IEnumerable<object[]> BadRepeats =>
    [
        ["repeat", $"{{{Base},\"days\":[],\"repeat\":\"daily\"}}"],
        ["repeat", $"{{{Base},\"days\":[],\"repeat\":\"\"}}"],
        ["repeat", $"{{{Base},\"days\":[],\"repeat\":\"Interval\",\"startsOn\":\"2026-10-28\"}}"],
        ["unit", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"unit\":\"fortnights\",\"startsOn\":\"2026-10-28\"}}"],
        ["unit", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"unit\":\"minute\",\"startsOn\":\"2026-10-28\"}}"],
        ["every", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":0,\"unit\":\"days\",\"startsOn\":\"2026-10-28\"}}"],
        ["every", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":1000,\"unit\":\"days\",\"startsOn\":\"2026-10-28\"}}"],
        ["every", $"{{{Base},\"days\":[1],\"every\":-1}}"],
        ["every", $"{{{Base},\"days\":[1],\"every\":1000,\"startsOn\":\"2026-10-28\"}}"],
        // Weekdays repeat by weeks.
        ["unit", $"{{{Base},\"days\":[1],\"repeat\":\"weekdays\",\"unit\":\"days\"}}"],
        ["unit", $"{{{Base},\"days\":[],\"unit\":\"minutes\"}}"],
        // Every 2 weeks or more needs a day and a start date.
        ["days", $"{{{Base},\"days\":[],\"repeat\":\"weekdays\",\"every\":2,\"startsOn\":\"2026-10-28\"}}"],
        ["startsOn", $"{{{Base},\"days\":[1,3],\"repeat\":\"weekdays\",\"every\":2}}"],
        ["startsOn", $"{{{Base},\"days\":[1,3],\"every\":2,\"startsOn\":null}}"],
        // An interval takes no days and needs a start date.
        ["days", $"{{{Base},\"days\":[1],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\",\"startsOn\":\"2026-10-28\"}}"],
        ["startsOn", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\"}}"],
        ["startsOn", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\",\"startsOn\":\"\"}}"],
        ["startsOn", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\",\"startsOn\":\"2026-02-30\"}}"],
        ["startsOn", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\",\"startsOn\":\"28/10/2026\"}}"],
        ["startsOn", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\",\"startsOn\":\"2026-2-3\"}}"],
        ["startsOn", $"{{{Base},\"days\":[],\"repeat\":\"interval\",\"every\":3,\"unit\":\"days\",\"startsOn\":\"2026-10-28T06:30\"}}"]
    ];

    [Theory]
    [MemberData(nameof(BadRepeats))]
    public async Task A_repeat_out_of_bounds_is_400_naming_the_field_and_stores_nothing(string field, string body)
    {
        using var owner = Owner();

        using var response = await Send(owner, "POST", "/api/alerts/routines", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await StateOf(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for {field}: {errors}");
        Assert.Empty(await _store.RoutinesAsync(CancellationToken.None));
    }

    // ------------------------------------------------------------------ rings

    [Fact]
    public async Task The_status_lists_an_intervals_rings_each_under_the_stamp_of_its_own_time()
    {
        using var phone = await PhoneAsync();
        // Saved at 11:00. Every 97 minutes from 12:30 today.
        var id = await CreateAsync(phone, Interval());

        var status = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");

        var rings = status.GetProperty("routineRings").EnumerateArray().ToList();
        Assert.Equal(
            [Key(id, "2026-10-28T12:30"), Key(id, "2026-10-28T14:07"), Key(id, "2026-10-28T15:44"), Key(id, "2026-10-28T17:21")],
            rings.Take(4).Select(ring => ring.GetProperty("key").GetString()));
        Assert.Equal(
            (Noon + Duration.FromMinutes(127)).ToDateTimeOffset(),
            rings[1].GetProperty("alertAt").GetDateTimeOffset());
        // 24 hours of rings 97 minutes apart, the last one past midnight.
        Assert.Equal(15, rings.Count);
        Assert.Equal(Key(id, "2026-10-29T11:08"), rings[^1].GetProperty("key").GetString());
    }

    [Fact]
    public async Task An_interval_ring_reaches_Pushover_and_its_acknowledgment_is_taken()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval());
        _clock.Now = Noon + Duration.FromMinutes(127) + Duration.FromSeconds(5);
        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        using var response = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(id, "2026-10-28T14:07"), via = "phone" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(_pushover.Requests, seen => seen.Url.AbsolutePath == "/1/messages.json" && seen.Form["title"] == "Meds");
        var ring = (await StateOf(response)).GetProperty("routineRings").EnumerateArray()
            .Single(ring => ring.GetProperty("key").GetString() == Key(id, "2026-10-28T14:07"));
        Assert.True(ring.GetProperty("acknowledged").GetBoolean());
    }

    // ------------------------------------------------------------------ the cycle routes

    [Fact]
    public async Task Done_takes_the_stamp_of_an_interval_ring_and_refuses_a_time_between_rings()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval());

        using var done = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T14:07/outcome", "{\"status\":\"done\"}");
        using var between = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T14:00/outcome", "{\"status\":\"done\"}");
        using var before = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T10:53/outcome", "{\"status\":\"done\"}");

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var state = await StateOf(done);
        Assert.DoesNotContain(Key(id, "2026-10-28T14:07"), Keys(state));
        Assert.Contains(Key(id, "2026-10-28T15:44"), Keys(state));
        Assert.Equal("2026-10-28T14:07", Assert.Single(state.GetProperty("routineOutcomes").EnumerateArray()).GetProperty("cycle").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, between.StatusCode);
        // 97 minutes before the first ring is no ring: the series starts at 12:30.
        Assert.Equal(HttpStatusCode.BadRequest, before.StatusCode);
        // Marked Done, an interval stays: it is never a one-time alarm.
        Assert.Single(await _store.RoutinesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_delay_takes_the_stamp_of_an_interval_ring_and_moves_that_ring_only()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval());

        using var response = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T14:07/delay", "{\"minutes\":20}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rings = (await StateOf(response)).GetProperty("routineRings").EnumerateArray()
            .ToDictionary(ring => ring.GetProperty("key").GetString()!, ring => ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal((Noon + Duration.FromMinutes(147)).ToDateTimeOffset(), rings[Key(id, "2026-10-28T14:07")]);
        Assert.Equal((Noon + Duration.FromMinutes(224)).ToDateTimeOffset(), rings[Key(id, "2026-10-28T15:44")]);
    }

    [Fact]
    public async Task A_step_is_checked_off_for_the_cycle_of_an_interval_ring()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval(steps: OneStep));

        using var response = await Send(
            phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-28T14:07/completion", "{\"note\":\"Took them\"}");
        using var between = await Send(
            phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-28T14:08/completion", "{}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await StateOf(response);
        Assert.DoesNotContain(Key(id, "2026-10-28T14:07", Meds), Keys(state));
        Assert.Contains(Key(id, "2026-10-28T14:07"), Keys(state));
        Assert.Contains(Key(id, "2026-10-28T15:44", Meds), Keys(state));
        Assert.Equal("2026-10-28T14:07", Assert.Single(state.GetProperty("routineCompletions").EnumerateArray()).GetProperty("cycle").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, between.StatusCode);
    }

    [Fact]
    public async Task An_interval_ring_delayed_7_days_is_listed_then_and_its_cycle_still_takes_a_completion_and_an_outcome()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval(steps: OneStep));

        using var delayed = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T14:07/delay", "{\"minutes\":10080}");
        using var tooLong = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T15:44/delay", "{\"minutes\":10081}");
        Assert.Equal(HttpStatusCode.OK, delayed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.DoesNotContain(Key(id, "2026-10-28T14:07"), Keys(await StateOf(delayed)));

        // Seven days on, ten minutes before the moved ring.
        var moved = Noon + Duration.FromMinutes(127) + Duration.FromDays(7);
        _clock.Now = moved - Duration.FromMinutes(10);
        var status = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        var rings = status.GetProperty("routineRings").EnumerateArray()
            .ToDictionary(ring => ring.GetProperty("key").GetString()!, ring => ring.GetProperty("alertAt").GetDateTimeOffset());
        Assert.Equal(moved.ToDateTimeOffset(), rings[Key(id, "2026-10-28T14:07")]);
        Assert.Equal((moved + Duration.FromMinutes(5)).ToDateTimeOffset(), rings[Key(id, "2026-10-28T14:07", Meds)]);
        // The delay is still listed, so the phone moves the same ring.
        Assert.Contains(status.GetProperty("routineDelays").EnumerateArray(), delay => delay.GetProperty("cycle").GetString() == "2026-10-28T14:07");

        using var checkedOff = await Send(
            phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-28T14:07/completion", "{}");
        using var done = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T14:07/outcome", "{\"status\":\"done\"}");

        Assert.Equal(HttpStatusCode.OK, checkedOff.StatusCode);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var keys = Keys(await StateOf(done));
        Assert.DoesNotContain(Key(id, "2026-10-28T14:07"), keys);
        Assert.DoesNotContain(Key(id, "2026-10-28T14:07", Meds), keys);
    }

    [Fact]
    public async Task A_weekday_ring_delayed_7_days_and_marked_Done_does_not_ring_at_its_moved_time()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, $"{{\"label\":\"Gym\",\"hour\":12,\"minute\":30,\"days\":[1,2,3,4,5,6,7],\"steps\":{OneStep}}}");
        using var delayed = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T12:30/delay", "{\"minutes\":10080}");
        using var done = await Send(phone, "PUT", $"/api/alerts/routines/{id}/cycles/2026-10-28T12:30/outcome", "{\"status\":\"skipped\"}");
        Assert.Equal(HttpStatusCode.OK, delayed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        _clock.Now = Noon + Duration.FromMinutes(20) + Duration.FromDays(7);
        var keys = Keys(await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status"));

        Assert.DoesNotContain(Key(id, "2026-10-28T12:30"), keys);
        Assert.DoesNotContain(Key(id, "2026-10-28T12:30", Meds), keys);
        // That day's own ring is still there.
        Assert.Contains(Key(id, "2026-11-04T12:30"), keys);
    }

    [Fact]
    public async Task A_step_every_2_days_is_checked_off_only_for_an_interval_ring_stamped_on_its_day()
    {
        using var phone = await PhoneAsync();
        // Every 5 hours from 12:30 on the 28th: 12:30, 17:30, 22:30, then 03:30 on the 29th. The step's days are the 29th, the 31st.
        var steps = $"[{{\"id\":\"{Meds}\",\"label\":\"Take medication\",\"offsetMinutes\":5,\"everyDays\":2,\"startsOn\":\"2026-10-29\"}}]";
        var id = await CreateAsync(phone, Interval(every: 5, unit: "hours", steps: steps));

        var status = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/status");
        using var off = await Send(phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-28T22:30/completion", "{}");
        using var on = await Send(phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-29T03:30/completion", "{}");

        var keys = Keys(status);
        Assert.Contains(Key(id, "2026-10-28T22:30"), keys);
        Assert.DoesNotContain(Key(id, "2026-10-28T22:30", Meds), keys);
        Assert.Contains(Key(id, "2026-10-29T03:30", Meds), keys);
        Assert.Contains(Key(id, "2026-10-29T08:30", Meds), keys);
        Assert.Equal(HttpStatusCode.BadRequest, off.StatusCode);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
    }

    [Fact]
    public async Task History_lists_the_cycles_of_an_interval_each_at_its_own_time()
    {
        using var phone = await PhoneAsync();
        // Saved at 05:00 on 27 October. Every 5 hours from 06:00 that day.
        _clock.Now = Instant.FromUtc(2026, 10, 27, 6, 0);
        var id = await CreateAsync(phone, Interval(every: 5, unit: "hours", startsOn: "2026-10-27", hour: 6, minute: 0, steps: OneStep));
        _clock.Now = Instant.FromUtc(2026, 10, 27, 16, 2);
        using var checkedOff = await Send(
            phone, "PUT", $"/api/alerts/routines/{id}/steps/{Meds}/cycles/2026-10-27T16:00/completion", "{}");
        Assert.Equal(HttpStatusCode.OK, checkedOff.StatusCode);
        _clock.Now = Noon + Duration.FromMinutes(1);

        var history = await phone.GetFromJsonAsync<JsonElement>("/api/alerts/history");

        var cycles = history.GetProperty("cycles").EnumerateArray().ToList();
        Assert.Equal(
            [
                "2026-10-28T12:00", "2026-10-28T07:00", "2026-10-28T02:00", "2026-10-27T21:00",
                "2026-10-27T16:00", "2026-10-27T11:00", "2026-10-27T06:00"
            ],
            cycles.Select(cycle => cycle.GetProperty("cycle").GetString()));
        Assert.Equal(Instant.FromUtc(2026, 10, 28, 2, 0).ToDateTimeOffset(), cycles[2].GetProperty("at").GetDateTimeOffset());
        Assert.NotEqual(JsonValueKind.Null, cycles[4].GetProperty("steps")[0].GetProperty("completion").ValueKind);
        Assert.Equal(JsonValueKind.Null, cycles[3].GetProperty("steps")[0].GetProperty("completion").ValueKind);
    }

    // ------------------------------------------------------------------ never a one-time alarm

    [Fact]
    public async Task An_interval_stays_after_its_ring_is_stopped_and_a_one_time_alarm_beside_it_goes()
    {
        using var phone = await PhoneAsync();
        var interval = await CreateAsync(phone, Interval());
        var once = await CreateAsync(phone, "{\"label\":\"Nap\",\"hour\":12,\"minute\":30,\"days\":[]}");
        _clock.Now = Noon + Duration.FromMinutes(30) + Duration.FromSeconds(5);

        using var stopped = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(interval, "2026-10-28T12:30"), via = "phone" });
        using var stoppedOnce = await phone.PostAsJsonAsync("/api/alerts/ack", new { key = Key(once, "2026-10-28T12:30"), via = "phone" });

        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stoppedOnce.StatusCode);
        Assert.Equal([interval], (await _store.RoutinesAsync(CancellationToken.None)).Select(routine => routine.Id));
        // The next ring is still planned.
        Assert.Contains(Key(interval, "2026-10-28T14:07"), Keys(await StateOf(stoppedOnce)));
    }

    [Fact]
    public async Task An_interval_with_steps_stays_after_a_pass_once_its_steps_have_stopped()
    {
        using var phone = await PhoneAsync();
        var id = await CreateAsync(phone, Interval(every: 2, unit: "days", steps: OneStep));
        _clock.Now = Noon + Duration.FromHours(12);

        await _app.Factory.Services.GetRequiredService<CalendarAlertWorker>().TickAsync(CancellationToken.None);

        Assert.Equal([id], (await _store.RoutinesAsync(CancellationToken.None)).Select(routine => routine.Id));
    }
}
