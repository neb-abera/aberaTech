using System.Net;
using aberaTech.Scheduling.Alerts;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The clock that starts the owner's cloud routines: each one at its local
/// time in the zone his phone reports, once, wherever the phone goes.
/// </summary>
public sealed class CloudRoutineClockTests
{
    private const string Token = "sk-ant-oat01-token-for-tests";

    // 26 Nov 2026 is a Thursday. Chicago is UTC-6 and New York UTC-5.
    private static readonly Instant HalfPastFiveInChicago = Instant.FromUtc(2026, 11, 26, 11, 30);

    private static readonly Instant HalfPastFiveInNewYork = Instant.FromUtc(2026, 11, 26, 10, 30);

    private static string Clock(LocalTime time) => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static CloudRoutinesOptions Options(string at = "05:30", string? days = null, string? token = Token) => new()
    {
        Routines = [new CloudRoutineOptions { Name = "morning-brief", Id = "trig_01Brief", Token = token, At = at, Days = days }],
    };

    [Fact]
    public void A_range_with_a_step_names_each_time_and_a_single_time_names_one()
    {
        Assert.Equal(
            "06:15 08:15 10:15 12:15 14:15 16:15 18:15 20:15",
            string.Join(' ', CloudRoutineSchedule.Times("06:15-20:15/120")!.Select(Clock)));
        Assert.Equal(17, CloudRoutineSchedule.Times("06:07-22:07/60")!.Count);
        Assert.Equal("05:30 11:00", string.Join(' ', CloudRoutineSchedule.Times("11:00, 05:30")!.Select(Clock)));
        Assert.True(CloudRoutineSchedule.Days("sun,Wed")!.SetEquals([IsoDayOfWeek.Sunday, IsoDayOfWeek.Wednesday]));
        Assert.Empty(CloudRoutineSchedule.Days("")!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("5:30")]
    [InlineData("25:00")]
    [InlineData("06:15-20:15")]
    [InlineData("06:15/60")]
    [InlineData("20:15-06:15/60")]
    [InlineData("06:15-20:15/1")]
    [InlineData("06:15-20:15/x")]
    public void A_bad_time_is_refused(string at) => Assert.Null(CloudRoutineSchedule.Times(at));

    [Fact]
    public void A_bad_day_is_refused() => Assert.Null(CloudRoutineSchedule.Days("Sun,Funday"));

    [Fact]
    public void A_bad_setting_stops_the_start_and_names_the_setting_never_the_token()
    {
        var badTime = Assert.Throws<InvalidOperationException>(() => Options(at: "half past five").Parsed());
        Assert.Contains("CloudRoutines__Routines__0__At", badTime.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, badTime.Message, StringComparison.Ordinal);

        Assert.Contains("Routines__0__Token", Assert.Throws<InvalidOperationException>(() => Options(token: " ").Parsed()).Message, StringComparison.Ordinal);
        Assert.Contains("Routines__0__Days", Assert.Throws<InvalidOperationException>(() => Options(days: "Someday").Parsed()).Message, StringComparison.Ordinal);
        Assert.Contains(
            "Routines__0__Id",
            Assert.Throws<InvalidOperationException>(() => new CloudRoutinesOptions
            {
                Routines = [new CloudRoutineOptions { Name = "x", Id = "../fire", Token = Token, At = "05:30" }],
            }.Parsed()).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "FireUrl",
            Assert.Throws<InvalidOperationException>(() => new CloudRoutinesOptions
            {
                FireUrl = "http://api.example.test/{0}/fire",
                Routines = Options().Routines,
            }.Parsed()).Message,
            StringComparison.Ordinal);
        Assert.Empty(new CloudRoutinesOptions().Parsed());
    }

    [Fact]
    public void A_run_is_due_from_its_minute_until_the_grace_ends()
    {
        var routines = Options().Parsed();
        var chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
        var grace = Duration.FromMinutes(20);

        Assert.Empty(CloudRoutineSchedule.Due(routines, HalfPastFiveInChicago - Duration.FromSeconds(1), chicago, grace));
        var due = Assert.Single(CloudRoutineSchedule.Due(routines, HalfPastFiveInChicago, chicago, grace));
        Assert.Equal("cloud:morning-brief:2026-11-26T05:30", due.Key);
        Assert.Single(CloudRoutineSchedule.Due(routines, HalfPastFiveInChicago + Duration.FromMinutes(19), chicago, grace));
        Assert.Empty(CloudRoutineSchedule.Due(routines, HalfPastFiveInChicago + Duration.FromMinutes(20), chicago, grace));
    }

    [Fact]
    public void A_run_just_before_midnight_is_still_due_just_after_it_under_yesterdays_key()
    {
        var routines = Options(at: "23:55").Parsed();
        var chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

        // 00:05 on Friday 27 Nov in Chicago.
        var due = Assert.Single(CloudRoutineSchedule.Due(routines, Instant.FromUtc(2026, 11, 27, 6, 5), chicago, Duration.FromMinutes(20)));
        Assert.Equal("cloud:morning-brief:2026-11-26T23:55", due.Key);
    }

    [Fact]
    public void A_routine_for_Sunday_is_not_due_on_Thursday_and_its_next_run_is_Sunday()
    {
        var routine = Assert.Single(Options(at: "17:00", days: "Sun").Parsed());
        var chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

        // 17:00 on Thursday 26 Nov in Chicago.
        var thursday = Instant.FromUtc(2026, 11, 26, 23, 0);
        Assert.Empty(CloudRoutineSchedule.Due([routine], thursday, chicago, Duration.FromMinutes(20)));
        Assert.Equal(new LocalDateTime(2026, 11, 29, 17, 0), CloudRoutineSchedule.Next(routine, thursday, chicago));
        Assert.Single(CloudRoutineSchedule.Due([routine], Instant.FromUtc(2026, 11, 29, 23, 0), chicago, Duration.FromMinutes(20)));
    }

    [Fact]
    public async Task The_run_starts_at_the_local_time_in_the_phones_zone_and_once()
    {
        using var box = new Harness(Options(), HalfPastFiveInNewYork);
        await box.PhoneIsInAsync("America/Chicago");

        // 05:30 in New York is 04:30 on the phone.
        Assert.Equal(0, await box.Clock.PassAsync(CancellationToken.None));
        Assert.Empty(box.Anthropic.Seen);

        box.Time.Now = HalfPastFiveInChicago;
        Assert.Equal(1, await box.Clock.PassAsync(CancellationToken.None));
        var request = Assert.Single(box.Anthropic.Seen);
        Assert.Equal("https://api.anthropic.com/v1/claude_code/routines/trig_01Brief/fire", request.Url.ToString());
        Assert.Equal($"Bearer {Token}", request.Authorization);
        Assert.Equal("2023-06-01", request.Version);
        Assert.Contains("Neb's iPhone reports the time zone America/Chicago", request.Body, StringComparison.Ordinal);
        Assert.Contains("Thu 26 Nov 2026 at 05:30", request.Body, StringComparison.Ordinal);

        box.Time.Now += Duration.FromSeconds(30);
        Assert.Equal(0, await box.Clock.PassAsync(CancellationToken.None));
        Assert.Single(box.Anthropic.Seen);

        var fire = Assert.Single(await box.Fires.RecentAsync(10, CancellationToken.None));
        Assert.Equal(("cloud:morning-brief:2026-11-26T05:30", "America/Chicago", "fired"), (fire.Key, fire.Zone, fire.Outcome));
        Assert.DoesNotContain(box.Logs.Entries, entry => entry.Message.Contains(Token, StringComparison.Ordinal));
        Assert.Contains(box.Logs.Entries, entry => entry.EventId.Id == CloudRoutineLog.FiredId);
    }

    [Fact]
    public async Task With_no_phone_zone_the_run_starts_in_the_fallback_zone()
    {
        using var box = new Harness(Options(), HalfPastFiveInNewYork);

        Assert.Equal(1, await box.Clock.PassAsync(CancellationToken.None));
        Assert.Contains("America/New_York", Assert.Single(box.Anthropic.Seen).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_phone_that_moves_west_does_not_start_the_same_local_minute_twice()
    {
        using var box = new Harness(Options(), HalfPastFiveInNewYork);
        await box.PhoneIsInAsync("America/New_York");
        Assert.Equal(1, await box.Clock.PassAsync(CancellationToken.None));

        // An hour later it is 05:30 again, in Chicago.
        await box.PhoneIsInAsync("America/Chicago");
        box.Time.Now = HalfPastFiveInChicago;
        Assert.Equal(0, await box.Clock.PassAsync(CancellationToken.None));
        Assert.Single(box.Anthropic.Seen);

        // The next morning it starts at 05:30 in Chicago.
        box.Time.Now = HalfPastFiveInChicago + Duration.FromDays(1);
        Assert.Equal(1, await box.Clock.PassAsync(CancellationToken.None));
        Assert.Contains("America/Chicago", box.Anthropic.Seen[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_phone_that_moves_east_starts_the_next_run_at_the_new_local_time()
    {
        using var box = new Harness(Options(), HalfPastFiveInChicago);
        await box.PhoneIsInAsync("America/Chicago");
        Assert.Equal(1, await box.Clock.PassAsync(CancellationToken.None));

        await box.PhoneIsInAsync("America/New_York");
        box.Time.Now = HalfPastFiveInNewYork + Duration.FromDays(1);
        Assert.Equal(1, await box.Clock.PassAsync(CancellationToken.None));
        Assert.Equal(2, box.Anthropic.Seen.Count);
        Assert.Contains("America/New_York", box.Anthropic.Seen[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_passes_at_the_same_moment_start_one_run_between_them()
    {
        using var box = new Harness(Options(), HalfPastFiveInNewYork);

        var claimed = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => box.Clock.PassAsync(CancellationToken.None)));

        Assert.Equal(1, claimed.Sum());
        Assert.Single(box.Anthropic.Seen);
    }

    [Fact]
    public async Task A_busy_answer_is_tried_once_more_and_a_refusal_is_not()
    {
        using var busy = new Harness(Options(), HalfPastFiveInNewYork);
        busy.Anthropic.Then(HttpStatusCode.ServiceUnavailable, "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"secret words\"}}");
        await busy.Clock.PassAsync(CancellationToken.None);
        Assert.Equal(2, busy.Anthropic.Seen.Count);
        Assert.Equal(CloudRoutineClock.RetryAfter.ToTimeSpan(), Assert.Single(busy.Delay.Waits));
        Assert.Equal("fired", Assert.Single(await busy.Fires.RecentAsync(10, CancellationToken.None)).Outcome);
        Assert.Contains(busy.Logs.Entries, entry => entry.EventId.Id == CloudRoutineLog.RetryingId);
        Assert.DoesNotContain(busy.Logs.Entries, entry => entry.Message.Contains("secret words", StringComparison.Ordinal));

        using var refused = new Harness(Options(), HalfPastFiveInNewYork);
        refused.Anthropic.Then(HttpStatusCode.Unauthorized, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"m\"}}");
        await refused.Clock.PassAsync(CancellationToken.None);
        Assert.Single(refused.Anthropic.Seen);
        Assert.Equal("failed: 401 authentication_error", Assert.Single(await refused.Fires.RecentAsync(10, CancellationToken.None)).Outcome);
        Assert.Contains(refused.Logs.Entries, entry => entry.EventId.Id == CloudRoutineLog.FailedId);

        // A failed run is not started again a pass later: the minute is claimed.
        refused.Time.Now += Duration.FromSeconds(30);
        Assert.Equal(0, await refused.Clock.PassAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_timeout_is_not_tried_again_because_the_run_may_have_started()
    {
        using var box = new Harness(Options(), HalfPastFiveInNewYork);
        box.Anthropic.ThenThrow(new TaskCanceledException());

        await box.Clock.PassAsync(CancellationToken.None);

        Assert.Single(box.Anthropic.Seen);
        Assert.Empty(box.Delay.Waits);
        Assert.Equal("failed: 0 Timeout", Assert.Single(await box.Fires.RecentAsync(10, CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_or_names_no_type_gives_the_status_alone()
    {
        using var box = new Harness(Options(), HalfPastFiveInNewYork);
        box.Anthropic.Then(HttpStatusCode.BadRequest, "<html>no</html>");

        await box.Clock.PassAsync(CancellationToken.None);

        Assert.Equal("failed: 400", Assert.Single(await box.Fires.RecentAsync(10, CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task With_no_routine_configured_the_clock_is_off_and_asks_nothing()
    {
        using var box = new Harness(new CloudRoutinesOptions(), HalfPastFiveInNewYork);

        Assert.False(box.Clock.On);
        Assert.Equal(0, await box.Clock.PassAsync(CancellationToken.None));
        await box.Clock.StartAsync(CancellationToken.None);
        await box.Clock.StopAsync(CancellationToken.None);
        Assert.Empty(box.Anthropic.Seen);
    }

    [Fact]
    public async Task The_status_names_the_zone_where_it_came_from_and_each_next_run_without_id_or_token()
    {
        using var box = new Harness(Options(), HalfPastFiveInChicago);
        var before = await box.StatusAsync();
        Assert.Equal(("America/New_York", "fallback"), (before.Zone, before.ZoneFrom));

        await box.PhoneIsInAsync("America/Chicago");
        await box.Clock.PassAsync(CancellationToken.None);
        var status = await box.StatusAsync();

        Assert.True(status.On);
        Assert.Equal(("America/Chicago", "phone"), (status.Zone, status.ZoneFrom));
        var line = Assert.Single(status.Routines);
        Assert.Equal(("morning-brief", "05:30", "", "Fri 27 Nov 2026 05:30"), (line.Name, line.At, line.Days, line.Next));
        Assert.Equal(HalfPastFiveInChicago + Duration.FromDays(1), line.NextAt);
        Assert.Single(status.Recent);
        var json = System.Text.Json.JsonSerializer.Serialize(status);
        Assert.DoesNotContain(Token, json, StringComparison.Ordinal);
        Assert.DoesNotContain("trig_", json, StringComparison.Ordinal);
    }

    private sealed class Harness : IDisposable
    {
        public Harness(CloudRoutinesOptions options, Instant now)
        {
            Time = new FakeClock(now);
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
            services.AddSingleton<IClock>(Time);
            services.AddSingleton(new AlertsOptions());
            services.AddSingleton(options);
            services.AddSingleton<IAlertStore>(new InMemoryAlertStore());
            services.AddSingleton<IAlertDeviceStore>(Devices);
            services.AddSingleton<ICloudRoutineFireStore>(Fires);
            services.AddCloudRoutineClock();
            services.AddSingleton<CloudRoutineDelay>(Delay);
            services.AddHttpClient<CloudRoutineClient>().ConfigurePrimaryHttpMessageHandler(() => Anthropic);
            Provider = services.BuildServiceProvider();
        }

        public FakeClock Time { get; }

        public InMemoryAlertDeviceStore Devices { get; } = new();

        public InMemoryFires Fires { get; } = new();

        public FireHandler Anthropic { get; } = new();

        public InstantDelay Delay { get; } = new();

        public CapturedLogs Logs { get; } = new();

        public ServiceProvider Provider { get; }

        public CloudRoutineClock Clock => Provider.GetRequiredService<CloudRoutineClock>();

        private Guid? _phone;

        public async Task PhoneIsInAsync(string zone)
        {
            if (_phone is null)
            {
                _phone = Guid.NewGuid();
                await Devices.CreateAsync(_phone.Value, "iphone", [1, 2, 3], Time.Now, CancellationToken.None);
            }

            await Devices.SetTimeZoneAsync(_phone.Value, zone, Time.Now, CancellationToken.None);
        }

        public Task<CloudRoutineStatus> StatusAsync() => CloudRoutineStatus.ReadAsync(
            Clock, Provider.GetRequiredService<IAlertStore>(), Devices, Fires, new AlertsOptions(), Time.Now, CancellationToken.None);

        public void Dispose() => Provider.Dispose();
    }

    private sealed class InstantDelay : CloudRoutineDelay
    {
        public List<TimeSpan> Waits { get; } = [];

        public override Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Waits.Add(delay);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryFires : ICloudRoutineFireStore
    {
        private readonly Lock _gate = new();

        private readonly Dictionary<string, CloudRoutineFire> _rows = [];

        public Task<bool> TryClaimAsync(string key, string zone, Instant now, CancellationToken cancellationToken)
        {
            lock (_gate) return Task.FromResult(_rows.TryAdd(key, new CloudRoutineFire(key, zone, now, "claimed", null)));
        }

        public Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken)
        {
            lock (_gate) _rows[key] = _rows[key] with { Outcome = outcome, CompletedAt = now };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CloudRoutineFire>> RecentAsync(int limit, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<CloudRoutineFire>>(
                    [.. _rows.Values.OrderByDescending(fire => fire.ClaimedAt).ThenBy(fire => fire.Key).Take(limit)]);
            }
        }

        public Task PruneAsync(Instant before, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                foreach (var key in _rows.Values.Where(fire => fire.ClaimedAt < before).Select(fire => fire.Key).ToList()) _rows.Remove(key);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>The API trigger in a handler: every request with its headers and body, answered 200 unless told otherwise.</summary>
    internal sealed class FireHandler : HttpMessageHandler
    {
        public sealed record Request(Uri Url, string? Authorization, string? Version, string Body);

        private readonly Lock _gate = new();

        private readonly Queue<Func<HttpResponseMessage>> _answers = new();

        private readonly List<Request> _seen = [];

        public IReadOnlyList<Request> Seen
        {
            get
            {
                lock (_gate) return [.. _seen];
            }
        }

        public void Then(HttpStatusCode status, string body)
        {
            lock (_gate) _answers.Enqueue(() => RecordingHandler.Text(status, body, "application/json"));
        }

        public void ThenThrow(Exception exception)
        {
            lock (_gate) _answers.Enqueue(() => throw exception);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The body is {"text": "..."}; keep the sentence itself, unescaped.
            var json = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var body = document.RootElement.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
            Func<HttpResponseMessage>? answer;
            lock (_gate)
            {
                _seen.Add(new Request(
                    request.RequestUri!,
                    request.Headers.TryGetValues("authorization", out var authorization) ? string.Join(",", authorization) : null,
                    request.Headers.TryGetValues("anthropic-version", out var version) ? string.Join(",", version) : null,
                    body));
                _answers.TryDequeue(out answer);
            }

            return answer is null
                ? RecordingHandler.Text(HttpStatusCode.OK, "{\"type\":\"routine_fire\"}", "application/json")
                : answer();
        }
    }
}

/// <summary>The claim in Postgres: the key's row lets one caller start a run.</summary>
public sealed class DatabaseCloudRoutineFireStoreTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 26, 11, 30);

    private readonly TestDatabase? _database;

    public DatabaseCloudRoutineFireStoreTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("cloudroutines");
        using var context = Context();
        context.Database.Migrate();
    }

    private SchedulingDbContext Context() =>
        new(new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(_database!.ConnectionString, npgsql => npgsql.UseNodaTime())
            .Options);

    [PostgresFact]
    public async Task Ten_replicas_claiming_one_run_at_once_get_one_claim()
    {
        const string key = "cloud:morning-brief:2026-11-26T05:30";
        var contexts = Enumerable.Range(0, 10).Select(_ => Context()).ToList();
        try
        {
            var claims = await Task.WhenAll(contexts.Select(context =>
                new DatabaseCloudRoutineFireStore(context).TryClaimAsync(key, "America/Chicago", Now, CancellationToken.None)));

            Assert.Equal(1, claims.Count(claimed => claimed));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }

        await using var check = Context();
        var store = new DatabaseCloudRoutineFireStore(check);
        Assert.False(await store.TryClaimAsync(key, "America/New_York", Now + Duration.FromHours(1), CancellationToken.None));
        var claimed = Assert.Single(await store.RecentAsync(10, CancellationToken.None));
        Assert.Equal((key, "America/Chicago", "claimed", (Instant?)null), (claimed.Key, claimed.Zone, claimed.Outcome, claimed.CompletedAt));
    }

    [PostgresFact]
    public async Task The_outcome_is_kept_short_the_newest_claim_is_first_and_old_rows_are_pruned()
    {
        await using var context = Context();
        var store = new DatabaseCloudRoutineFireStore(context);
        await store.TryClaimAsync("cloud:a:2026-09-01T05:30", "America/New_York", Now - Duration.FromDays(80), CancellationToken.None);
        await store.TryClaimAsync("cloud:a:2026-11-26T05:30", "America/Chicago", Now, CancellationToken.None);
        await store.RecordOutcomeAsync("cloud:a:2026-11-26T05:30", "failed: 500 " + new string('x', 80), Now + Duration.FromSeconds(2), CancellationToken.None);

        var recent = await store.RecentAsync(10, CancellationToken.None);
        Assert.Equal("cloud:a:2026-11-26T05:30 cloud:a:2026-09-01T05:30", string.Join(' ', recent.Select(fire => fire.Key)));
        Assert.Equal(64, recent[0].Outcome.Length);
        Assert.Equal(Now + Duration.FromSeconds(2), recent[0].CompletedAt);

        await store.PruneAsync(Now - CloudRoutineClock.Kept, CancellationToken.None);
        Assert.Equal("cloud:a:2026-11-26T05:30", Assert.Single(await store.RecentAsync(10, CancellationToken.None)).Key);
    }

    public void Dispose() => _database?.Dispose();
}
