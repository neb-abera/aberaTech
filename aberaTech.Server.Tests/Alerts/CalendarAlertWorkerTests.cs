using System.Net;
using aberaTech.Scheduling.Alerts;
using aberaTech.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The send path, driven tick by tick on a clock the test moves: one
/// Pushover message at the alert time, priority 1, never a second one for
/// the same occurrence, and none while muted or skipped. Pushover and the
/// calendar are handlers that record what they were asked.
/// </summary>
public sealed class CalendarAlertWorkerTests : IDisposable
{
    private const string FeedUrl = "https://calendar.google.com/calendar/ical/owner%40example.test/private-0123456789abcdef/basic.ics";
    private const string AppToken = "app-token-for-tests-azGvAc";
    private const string UserKey = "user-key-for-tests-uQiRzp";

    /// <summary>08:00 New York time; the standup starts at 09:00 and its reminder is at 08:45.</summary>
    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private static readonly Instant AlertTime = Instant.FromUtc(2026, 10, 28, 12, 45);

    private static readonly Instant Start = Instant.FromUtc(2026, 10, 28, 13, 0);

    private readonly List<Harness> _harnesses = [];

    private Harness New(string? feed = null, IAlertStore? store = null, Instant? now = null)
    {
        var harness = new Harness(feed ?? Standup(Popup("-PT15M")), store ?? new InMemoryAlertStore(), now ?? Eight);
        _harnesses.Add(harness);
        return harness;
    }

    [Fact]
    public async Task One_priority_one_message_goes_at_the_reminder_time_without_waiting_for_the_next_read()
    {
        var box = New();

        var wake = await box.Worker.TickAsync(CancellationToken.None);

        // Nothing yet, and the next wake is the reminder itself, not the next
        // five-minute read: at 08:45 the list is already in memory.
        Assert.Empty(box.Pushover.Requests);
        Assert.Equal(Eight + Duration.FromMinutes(5), wake);

        box.Clock.Now = Eight + Duration.FromMinutes(40);
        await box.Worker.TickAsync(CancellationToken.None);
        box.Clock.Now = AlertTime - Duration.FromSeconds(1);
        wake = await box.Worker.TickAsync(CancellationToken.None);
        Assert.Equal(AlertTime, wake);
        Assert.Empty(box.Pushover.Requests);

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(PushoverClient.Endpoint, sent.Url.ToString());
        Assert.Equal("1", sent.Form["priority"]);
        Assert.Equal(AppToken, sent.Form["token"]);
        Assert.Equal(UserKey, sent.Form["user"]);
        Assert.Equal("Standup", sent.Form["title"]);
        Assert.Equal("Starts 9:00 AM EDT, Wed 28 Oct\nRoom 1", sent.Form["message"]);
        Assert.False(sent.Form.ContainsKey("retry"));
        Assert.False(sent.Form.ContainsKey("expire"));
    }

    [Fact]
    public async Task Ticking_again_restarting_or_a_second_replica_never_sends_twice()
    {
        var store = new InMemoryAlertStore();
        var first = New(store: store);
        await first.Worker.TickAsync(CancellationToken.None);
        first.Clock.Now = AlertTime;
        await first.Worker.TickAsync(CancellationToken.None);
        await first.Worker.TickAsync(CancellationToken.None);

        // A second replica, or this one after a restart, with the same database.
        var second = New(store: store, now: AlertTime + Duration.FromMinutes(1));
        await second.Worker.TickAsync(CancellationToken.None);

        Assert.Single(first.Pushover.Requests);
        Assert.Empty(second.Pushover.Requests);
        Assert.Equal("sent", store.Claims.Values.Single());
    }

    [Fact]
    public async Task Two_replicas_at_the_same_moment_send_one_message_between_them()
    {
        var store = new InMemoryAlertStore();
        var one = New(store: store, now: AlertTime);
        var two = New(store: store, now: AlertTime);

        await Task.WhenAll(one.Worker.TickAsync(CancellationToken.None), two.Worker.TickAsync(CancellationToken.None));

        Assert.Equal(1, one.Pushover.Requests.Count + two.Pushover.Requests.Count);
    }

    [Fact]
    public async Task A_server_that_starts_after_the_reminder_but_before_the_start_sends_at_once()
    {
        var box = New(now: AlertTime + Duration.FromMinutes(5));

        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Single(box.Pushover.Requests);
    }

    [Fact]
    public async Task Once_the_event_has_started_its_alert_is_never_sent()
    {
        var box = New(now: Start + Duration.FromMinutes(1));

        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Empty(box.Pushover.Requests);
    }

    [Fact]
    public async Task Mute_is_read_just_before_the_send_so_a_mute_after_the_read_still_holds()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);

        // Muted after the calendar was read, before the reminder.
        await box.Store.SetMutedUntilAsync(AlertTime + Duration.FromMinutes(30), Eight, CancellationToken.None);
        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Empty(box.Pushover.Requests);
        Assert.Empty(box.Store.Claims);
    }

    [Fact]
    public async Task A_mute_that_has_ended_sends_as_usual()
    {
        var box = New();
        await box.Store.SetMutedUntilAsync(AlertTime - Duration.FromMinutes(1), Eight, CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Single(box.Pushover.Requests);
    }

    [Fact]
    public async Task A_skipped_occurrence_is_not_sent_and_the_others_are()
    {
        var feed = Ics(
            Event("daily@google.com", "Daily", "20261028T090000", "20261028T093000", extra: ["RRULE:FREQ=DAILY;COUNT=2"]));
        var box = New(feed);
        await box.Worker.TickAsync(CancellationToken.None);
        var first = box.Status.Snapshot().Plan[0];
        await box.Store.SkipAsync(first.Key, first.StartsAt, Eight, CancellationToken.None);

        box.Clock.Now = first.AlertAt;
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Empty(box.Pushover.Requests);

        box.Clock.Now = first.AlertAt + Duration.FromDays(1);
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Single(box.Pushover.Requests);
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_list_says_why_and_the_alert_still_goes()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);

        box.Calendar.Then(() => RecordingHandler.Text(HttpStatusCode.NotFound, "gone"));
        box.Clock.Now = Eight + Duration.FromMinutes(5);
        await box.Worker.TickAsync(CancellationToken.None);

        var status = box.Status.Snapshot();
        Assert.Equal("HTTP 404", status.LastFetchError);
        Assert.Equal(Eight + Duration.FromMinutes(5), status.LastFetchAt);
        Assert.Equal(Eight, status.LastSuccessAt);
        Assert.Single(status.Plan);

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Single(box.Pushover.Requests);
    }

    [Fact]
    public async Task A_page_that_is_not_a_calendar_is_reported_as_such()
    {
        var box = New("<!DOCTYPE html><html>Sign in</html>");

        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal("The feed is not a calendar.", box.Status.Snapshot().LastFetchError);
    }

    [Fact]
    public async Task A_pushover_outage_is_tried_once_more_and_a_refusal_is_not()
    {
        var outage = New(now: AlertTime);
        outage.Pushover.Then(() => RecordingHandler.Text(HttpStatusCode.ServiceUnavailable, "{}"));
        await outage.Worker.TickAsync(CancellationToken.None);
        Assert.Equal(2, outage.Pushover.Requests.Count);
        Assert.Equal("sent", outage.Store.Claims.Values.Single());

        var refused = New(now: AlertTime);
        refused.Pushover.Then(() => RecordingHandler.Text(HttpStatusCode.BadRequest, "{\"status\":0}"));
        await refused.Worker.TickAsync(CancellationToken.None);
        await refused.Worker.TickAsync(CancellationToken.None);
        Assert.Single(refused.Pushover.Requests);
        Assert.Equal("failed: HTTP 400", refused.Store.Claims.Values.Single());
        Assert.Equal("failed: HTTP 400", refused.Status.Snapshot().LastSend!.Outcome);
    }

    [Fact]
    public async Task A_database_that_is_down_at_send_time_is_tried_again_half_a_minute_later()
    {
        var box = New(store: new BrokenStore(), now: AlertTime);

        var wake = await box.Worker.TickAsync(CancellationToken.None);

        Assert.Empty(box.Pushover.Requests);
        Assert.Equal(AlertTime + Duration.FromSeconds(30), wake);
    }

    [Fact]
    public async Task Neither_the_calendar_address_nor_the_pushover_keys_reach_the_log()
    {
        var box = New(now: AlertTime);
        box.Pushover.Then(() => RecordingHandler.Text(HttpStatusCode.BadRequest, "{\"status\":0}"));
        await box.Worker.TickAsync(CancellationToken.None);
        box.Calendar.Then(() => throw new HttpRequestException("failed to reach " + FeedUrl));
        box.Clock.Now = AlertTime + Duration.FromMinutes(6);
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.NotEmpty(box.Logs.Entries);
        Assert.All(box.Logs.Entries, entry =>
        {
            Assert.DoesNotContain("private-0123456789abcdef", entry.Everything);
            Assert.DoesNotContain(AppToken, entry.Everything);
            Assert.DoesNotContain(UserKey, entry.Everything);
            Assert.DoesNotContain("Standup", entry.Everything);
        });
        Assert.Equal(nameof(HttpRequestException), box.Status.Snapshot().LastFetchError);
    }

    [Fact]
    public async Task The_test_alert_is_one_priority_one_message_in_the_calendars_zone_and_ignores_mute()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        await box.Store.SetMutedUntilAsync(Eight + Duration.FromHours(3), Eight, CancellationToken.None);

        var result = await box.Dispatcher.SendTestAsync(CancellationToken.None);

        Assert.True(result.Ok);
        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("1", sent.Form["priority"]);
        Assert.Equal("Test alert", sent.Form["title"]);
        Assert.Contains("8:00 AM EDT", sent.Form["message"]);
    }

    [Fact]
    public void The_hosted_service_is_registered_once()
    {
        var box = New();

        Assert.Single(box.Provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>());
    }

    public void Dispose()
    {
        foreach (var harness in _harnesses) harness.Dispose();
    }

    private sealed class BrokenStore : IAlertStore
    {
        private static Task<T> Down<T>() => Task.FromException<T>(new TimeoutException("database down"));

        public Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken) => Down<Instant?>();

        public Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken) => Down<IReadOnlySet<string>>();

        public Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken) => Down<bool>();

        public Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task UnskipAsync(string key, CancellationToken cancellationToken) => Down<bool>();

        public Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task PruneAsync(Instant before, CancellationToken cancellationToken) => Down<bool>();
    }

    private sealed class Harness : IDisposable
    {
        public Harness(string feed, IAlertStore store, Instant now)
        {
            Clock = new FakeClock(now);
            Store = store as InMemoryAlertStore ?? new InMemoryAlertStore();
            Calendar = new RecordingHandler(() => RecordingHandler.Text(HttpStatusCode.OK, feed, "text/calendar"));
            Pushover = new RecordingHandler(() => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));

            var options = new AlertsOptions
            {
                CalendarIcsUrl = FeedUrl,
                PushoverAppToken = AppToken,
                PushoverUserKey = UserKey,
                PushoverRetrySeconds = 0
            };

            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton(options);
            services.AddSingleton(store);
            services.AddCalendarAlerts();
            services.AddHttpClient<CalendarFeed>().ConfigurePrimaryHttpMessageHandler(() => Calendar);
            services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => Pushover);
            Provider = services.BuildServiceProvider();
        }

        public FakeClock Clock { get; }

        public InMemoryAlertStore Store { get; }

        public RecordingHandler Calendar { get; }

        public RecordingHandler Pushover { get; }

        public CapturedLogs Logs { get; } = new();

        public ServiceProvider Provider { get; }

        public CalendarAlertWorker Worker => Provider.GetRequiredService<CalendarAlertWorker>();

        public AlertDispatcher Dispatcher => Provider.GetRequiredService<AlertDispatcher>();

        public AlertsStatus Status => Provider.GetRequiredService<AlertsStatus>();

        public void Dispose() => Provider.Dispose();
    }
}
