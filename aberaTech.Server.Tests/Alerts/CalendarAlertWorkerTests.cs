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
/// Pushover message at the alert time, as an alarm for an event marked
/// #critical and as its type otherwise, never a second one for the same
/// occurrence, and none while muted, skipped or set to none. Pushover and
/// the calendar are handlers that record what they were asked.
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
        var harness = new Harness(feed ?? CriticalStandup(Popup("-PT15M")), store ?? new InMemoryAlertStore(), now ?? Eight);
        _harnesses.Add(harness);
        return harness;
    }

    [Fact]
    public async Task One_message_goes_at_the_reminder_time_without_waiting_for_the_next_read()
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
        // Emergency: the phone sounds every 60 seconds until the alert is
        // acknowledged in the Pushover app.
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("60", sent.Form["retry"]);
        Assert.Equal("10800", sent.Form["expire"]);
        Assert.Equal(AppToken, sent.Form["token"]);
        Assert.Equal(UserKey, sent.Form["user"]);
        Assert.Equal("Standup", sent.Form["title"]);
        Assert.Equal("Starts 9:00 AM EDT, Wed 28 Oct\nRoom 1", sent.Form["message"]);
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
            Event("daily@google.com", "Daily #critical", "20261028T090000", "20261028T093000", extra: ["RRULE:FREQ=DAILY;COUNT=2"]));
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
    public async Task The_test_alert_is_one_emergency_message_in_the_calendars_zone_and_ignores_mute()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        await box.Store.SetMutedUntilAsync(Eight + Duration.FromHours(3), Eight, CancellationToken.None);

        var result = await box.Dispatcher.SendTestAsync(AlertTypes.Alarm, CancellationToken.None);

        Assert.True(result.Ok);
        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("Test alert", sent.Form["title"]);
        Assert.Contains("8:00 AM EDT", sent.Form["message"]);
    }

    /// <summary>The configuration's defaults with the owner's changes, as the page saves them.</summary>
    private static AlertSettings Saved(Func<AlertSettings, AlertSettings> change) =>
        change(AlertSettings.Defaults(new AlertsOptions()));

    [Fact]
    public async Task With_nothing_saved_the_message_repeats_every_minute_for_three_hours_with_the_phones_sound()
    {
        var box = New(now: AlertTime);

        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("60", sent.Form["retry"]);
        Assert.Equal("10800", sent.Form["expire"]);
        Assert.False(sent.Form.ContainsKey("sound"));
    }

    [Fact]
    public async Task An_alarm_repeats_until_acknowledged_whatever_the_notification_settings_say()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        await box.Store.SaveSettingsAsync(
            Saved(settings => settings with { NotificationPriority = 0, RepeatSeconds = 30, StopAfterMinutes = 1 }),
            Eight,
            CancellationToken.None);

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("30", sent.Form["retry"]);
        Assert.Equal("60", sent.Form["expire"]);
    }

    [Fact]
    public async Task A_saved_repeat_of_120_seconds_and_stop_of_30_minutes_is_what_pushover_is_asked_for()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        await box.Store.SaveSettingsAsync(
            Saved(settings => settings with { RepeatSeconds = 120, StopAfterMinutes = 30 }), Eight, CancellationToken.None);

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("120", sent.Form["retry"]);
        Assert.Equal("1800", sent.Form["expire"]);
    }

    [Fact]
    public async Task A_saved_sound_is_sent_and_a_blank_one_leaves_the_field_out()
    {
        var siren = New(now: AlertTime);
        await siren.Store.SaveSettingsAsync(Saved(settings => settings with { Sound = "siren" }), Eight, CancellationToken.None);
        await siren.Worker.TickAsync(CancellationToken.None);
        Assert.Equal("siren", Assert.Single(siren.Pushover.Requests).Form["sound"]);

        var blank = New(now: AlertTime);
        await blank.Store.SaveSettingsAsync(Saved(settings => settings with { Sound = "" }), Eight, CancellationToken.None);
        await blank.Worker.TickAsync(CancellationToken.None);
        Assert.False(Assert.Single(blank.Pushover.Requests).Form.ContainsKey("sound"));
    }

    [Fact]
    public async Task A_lead_saved_on_another_replica_moves_the_alert_at_this_ones_next_pass()
    {
        // No reminder on the event: the default lead decides, 10 minutes.
        var box = New(Standup());
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Equal(Start - Duration.FromMinutes(10), Assert.Single(box.Status.Snapshot().Plan).AlertAt);

        await box.Store.SaveSettingsAsync(Saved(settings => settings with { DefaultLeadMinutes = 45 }), Eight, CancellationToken.None);
        box.Clock.Now = Eight + Duration.FromMinutes(1);
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal(Start - Duration.FromMinutes(45), Assert.Single(box.Status.Snapshot().Plan).AlertAt);
        Assert.Equal(2, box.Calendar.Requests.Count);
    }

    [Fact]
    public async Task A_saved_poll_interval_sets_when_the_calendar_is_read_next()
    {
        var box = New(Standup());
        await box.Store.SaveSettingsAsync(Saved(settings => settings with { PollMinutes = 1 }), Eight, CancellationToken.None);

        var wake = await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal(Eight + Duration.FromMinutes(1), wake);
    }

    [Fact]
    public async Task The_test_alert_follows_the_saved_sound_and_always_repeats()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        await box.Store.SaveSettingsAsync(
            Saved(settings => settings with { Sound = "bugle", StopAfterMinutes = 20 }), Eight, CancellationToken.None);

        await box.Dispatcher.SendTestAsync(AlertTypes.Alarm, CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("bugle", sent.Form["sound"]);
        Assert.Equal("60", sent.Form["retry"]);
        Assert.Equal("1200", sent.Form["expire"]);
        Assert.Contains("It rings every minute until you acknowledge it.", sent.Form["message"]);
    }

    [Fact]
    public async Task The_test_alert_says_how_often_it_rings()
    {
        var box = New();
        await box.Store.SaveSettingsAsync(Saved(settings => settings with { RepeatSeconds = 120 }), Eight, CancellationToken.None);

        await box.Dispatcher.SendTestAsync(AlertTypes.Alarm, CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("120", sent.Form["retry"]);
        Assert.Contains("rings every 2 minutes until you acknowledge it", sent.Form["message"]);
    }

    [Fact]
    public async Task A_failed_settings_read_holds_the_send_until_the_database_answers_then_sends_as_saved()
    {
        var store = new SettingsOutageStore();
        await store.SaveSettingsAsync(Saved(settings => settings with { RepeatSeconds = 300 }), Eight, CancellationToken.None);
        var box = New(store: store);
        await box.Worker.TickAsync(CancellationToken.None);

        // Every other read answers. A send with guessed settings would be
        // the wrong message, so none goes and nothing is claimed.
        store.SettingsDown = true;
        box.Clock.Now = AlertTime;
        var wake = await box.Worker.TickAsync(CancellationToken.None);

        Assert.Empty(box.Pushover.Requests);
        Assert.Equal(AlertTime + CalendarAlertWorker.RetryAfter, wake);
        Assert.Contains(box.Logs.Entries, entry => entry.Everything.Contains("Reading the alert settings failed"));

        store.SettingsDown = false;
        box.Clock.Now = wake;
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal("300", Assert.Single(box.Pushover.Requests).Form["retry"]);
    }

    [Fact]
    public async Task An_unmarked_event_sends_nothing_by_default_and_claims_nothing()
    {
        var box = New(Standup(Popup("-PT15M")), now: AlertTime);

        await box.Worker.TickAsync(CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Empty(box.Pushover.Requests);
        Assert.Empty(box.Store.Claims);
        Assert.False(Assert.Single(box.Status.Snapshot().Plan).Critical);
    }

    [Fact]
    public async Task An_unmarked_event_switched_to_notification_after_its_alert_time_still_goes_before_the_start()
    {
        var box = New(Standup(Popup("-PT15M")), now: AlertTime);
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Empty(box.Pushover.Requests);

        await box.Store.SetEventTypeAsync("standup@google.com", AlertTypes.Notification, AlertTime, CancellationToken.None);
        box.Clock.Now = AlertTime + Duration.FromMinutes(2);
        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("0", sent.Form["priority"]);
        Assert.Equal("sent", Assert.Single(box.Store.Claims).Value);
    }

    [Fact]
    public async Task A_default_of_notification_sends_the_notification_priority_and_sound_with_no_retry_or_expiry()
    {
        var box = New(Standup(Popup("-PT15M")), now: AlertTime);
        await box.Store.SaveSettingsAsync(
            Saved(settings => settings with
            {
                DefaultType = AlertTypes.Notification, NotificationPriority = 1, NotificationSound = "bike", Sound = "siren"
            }),
            Eight,
            CancellationToken.None);

        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("1", sent.Form["priority"]);
        Assert.Equal("bike", sent.Form["sound"]);
        Assert.False(sent.Form.ContainsKey("retry"));
        Assert.False(sent.Form.ContainsKey("expire"));
        Assert.Equal("Standup", sent.Form["title"]);
    }

    [Fact]
    public async Task An_event_marked_critical_goes_with_the_alarm_settings_and_without_the_mark()
    {
        var box = New(now: AlertTime);
        await box.Store.SaveSettingsAsync(
            Saved(settings => settings with
            {
                RepeatSeconds = 30, StopAfterMinutes = 20, Sound = "persistent", NotificationPriority = 1, NotificationSound = "bike"
            }),
            Eight,
            CancellationToken.None);

        await box.Worker.TickAsync(CancellationToken.None);

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("2", sent.Form["priority"]);
        Assert.Equal("30", sent.Form["retry"]);
        Assert.Equal("1200", sent.Form["expire"]);
        Assert.Equal("persistent", sent.Form["sound"]);
        Assert.Equal("Standup", sent.Form["title"]);
    }

    [Theory]
    [InlineData("none", null)]
    [InlineData("notification", "0")]
    public async Task A_choice_on_the_page_overrides_the_critical_mark(string type, string? priority)
    {
        var box = New(now: AlertTime);
        await box.Store.SetEventTypeAsync("standup@google.com", type, Eight, CancellationToken.None);

        await box.Worker.TickAsync(CancellationToken.None);

        if (priority is null)
        {
            Assert.Empty(box.Pushover.Requests);
            Assert.Empty(box.Store.Claims);
            return;
        }

        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal(priority, sent.Form["priority"]);
        Assert.False(sent.Form.ContainsKey("retry"));
    }

    [Fact]
    public async Task Alarm_chosen_for_a_repeating_event_holds_for_every_occurrence()
    {
        var feed = Ics(
            Event("daily@google.com", "Daily", "20261028T090000", "20261028T093000", extra: ["RRULE:FREQ=DAILY;COUNT=2"]));
        var box = New(feed);
        await box.Store.SetEventTypeAsync("daily@google.com", AlertTypes.Alarm, Eight, CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);
        var first = box.Status.Snapshot().Plan[0];

        box.Clock.Now = first.AlertAt;
        await box.Worker.TickAsync(CancellationToken.None);
        box.Clock.Now = first.AlertAt + Duration.FromDays(1);
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal(["2", "2"], box.Pushover.Requests.Select(request => request.Form["priority"]));
        Assert.Equal(["60", "60"], box.Pushover.Requests.Select(request => request.Form["retry"]));
    }

    [Fact]
    public async Task The_test_notification_goes_with_the_notification_settings_and_says_so()
    {
        var box = New();
        await box.Store.SaveSettingsAsync(
            Saved(settings => settings with { NotificationPriority = 1, NotificationSound = "bike" }), Eight, CancellationToken.None);

        var result = await box.Dispatcher.SendTestAsync(AlertTypes.Notification, CancellationToken.None);

        Assert.True(result.Ok);
        var sent = Assert.Single(box.Pushover.Requests);
        Assert.Equal("Test notification", sent.Form["title"]);
        Assert.Equal("1", sent.Form["priority"]);
        Assert.Equal("bike", sent.Form["sound"]);
        Assert.False(sent.Form.ContainsKey("retry"));
        Assert.False(sent.Form.ContainsKey("expire"));
        Assert.Contains("as a notification", sent.Form["message"]);
        Assert.Equal("Test notification", box.Status.Snapshot().LastSend!.Title);
    }

    [Fact]
    public async Task A_test_of_type_none_is_refused_rather_than_sent()
    {
        var box = New();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            box.Dispatcher.SendTestAsync(AlertTypes.None, CancellationToken.None));

        Assert.Empty(box.Pushover.Requests);
    }

    [Fact]
    public async Task Each_read_reports_every_event_in_the_feed_so_old_choices_can_be_forgotten()
    {
        var box = New(Ics(
            Event("standup@google.com", "Standup", "20261028T090000"),
            Event("next-month@google.com", "Later", "20261128T090000")));

        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal(["next-month@google.com", "standup@google.com"], Assert.Single(box.Store.Seen).Order(StringComparer.Ordinal));
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

        public Task<AlertSettings?> SettingsAsync(CancellationToken cancellationToken) => Down<AlertSettings?>();

        public Task SaveSettingsAsync(AlertSettings settings, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken) => Down<Instant?>();

        public Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken) => Down<IReadOnlySet<string>>();

        public Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken) => Down<bool>();

        public Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task UnskipAsync(string key, CancellationToken cancellationToken) => Down<bool>();

        public Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task RecordOutcomeAsync(
            string key, string outcome, Instant now, CancellationToken cancellationToken, string? receipt = null) => Down<bool>();

        public Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken) => Down<AlertDelivery?>();

        public Task<IReadOnlyDictionary<string, AlertAcknowledgement>> AcknowledgementsAsync(CancellationToken cancellationToken) =>
            Down<IReadOnlyDictionary<string, AlertAcknowledgement>>();

        public Task<bool> IsAcknowledgedAsync(string key, CancellationToken cancellationToken) => Down<bool>();

        public Task<bool> AcknowledgeAsync(string key, Instant startsAt, string via, Instant now, CancellationToken cancellationToken) =>
            Down<bool>();

        public Task PruneAsync(Instant before, CancellationToken cancellationToken) => Down<bool>();

        public Task<IReadOnlyDictionary<string, string>> EventTypesAsync(CancellationToken cancellationToken) =>
            Down<IReadOnlyDictionary<string, string>>();

        public Task<string?> EventTypeAsync(string eventId, CancellationToken cancellationToken) => Down<string?>();

        public Task SetEventTypeAsync(string eventId, string? type, Instant now, CancellationToken cancellationToken) => Down<bool>();

        public Task SeenEventsAsync(
            IReadOnlyCollection<string> eventIds, Instant now, Instant forgetBefore, CancellationToken cancellationToken) => Down<bool>();
    }

    /// <summary>The in-memory store with a settings read that can be made to fail.</summary>
    private sealed class SettingsOutageStore : IAlertStore
    {
        private readonly InMemoryAlertStore _inner = new();

        public bool SettingsDown { get; set; }

        public Task<AlertSettings?> SettingsAsync(CancellationToken cancellationToken) =>
            SettingsDown ? Task.FromException<AlertSettings?>(new TimeoutException("database down")) : _inner.SettingsAsync(cancellationToken);

        public Task SaveSettingsAsync(AlertSettings settings, Instant now, CancellationToken cancellationToken) =>
            _inner.SaveSettingsAsync(settings, now, cancellationToken);

        public Task<Instant?> MutedUntilAsync(CancellationToken cancellationToken) => _inner.MutedUntilAsync(cancellationToken);

        public Task SetMutedUntilAsync(Instant? until, Instant now, CancellationToken cancellationToken) =>
            _inner.SetMutedUntilAsync(until, now, cancellationToken);

        public Task<IReadOnlySet<string>> SkippedAsync(CancellationToken cancellationToken) => _inner.SkippedAsync(cancellationToken);

        public Task<bool> IsSkippedAsync(string key, CancellationToken cancellationToken) => _inner.IsSkippedAsync(key, cancellationToken);

        public Task SkipAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken) =>
            _inner.SkipAsync(key, startsAt, now, cancellationToken);

        public Task UnskipAsync(string key, CancellationToken cancellationToken) => _inner.UnskipAsync(key, cancellationToken);

        public Task<bool> TryClaimAsync(string key, Instant startsAt, Instant now, CancellationToken cancellationToken) =>
            _inner.TryClaimAsync(key, startsAt, now, cancellationToken);

        public Task RecordOutcomeAsync(
            string key, string outcome, Instant now, CancellationToken cancellationToken, string? receipt = null) =>
            _inner.RecordOutcomeAsync(key, outcome, now, cancellationToken, receipt);

        public Task<AlertDelivery?> DeliveryAsync(string key, CancellationToken cancellationToken) =>
            _inner.DeliveryAsync(key, cancellationToken);

        public Task<IReadOnlyDictionary<string, AlertAcknowledgement>> AcknowledgementsAsync(CancellationToken cancellationToken) =>
            _inner.AcknowledgementsAsync(cancellationToken);

        public Task<bool> IsAcknowledgedAsync(string key, CancellationToken cancellationToken) =>
            _inner.IsAcknowledgedAsync(key, cancellationToken);

        public Task<bool> AcknowledgeAsync(string key, Instant startsAt, string via, Instant now, CancellationToken cancellationToken) =>
            _inner.AcknowledgeAsync(key, startsAt, via, now, cancellationToken);

        public Task PruneAsync(Instant before, CancellationToken cancellationToken) => _inner.PruneAsync(before, cancellationToken);

        public Task<IReadOnlyDictionary<string, string>> EventTypesAsync(CancellationToken cancellationToken) =>
            _inner.EventTypesAsync(cancellationToken);

        public Task<string?> EventTypeAsync(string eventId, CancellationToken cancellationToken) =>
            _inner.EventTypeAsync(eventId, cancellationToken);

        public Task SetEventTypeAsync(string eventId, string? type, Instant now, CancellationToken cancellationToken) =>
            _inner.SetEventTypeAsync(eventId, type, now, cancellationToken);

        public Task SeenEventsAsync(
            IReadOnlyCollection<string> eventIds, Instant now, Instant forgetBefore, CancellationToken cancellationToken) =>
            _inner.SeenEventsAsync(eventIds, now, forgetBefore, cancellationToken);
    }

    [Fact]
    public async Task An_occurrence_acknowledged_before_its_time_is_never_sent()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        await box.Store.AcknowledgeAsync("standup@google.com|20261028T130000Z", Start, "phone", Eight, CancellationToken.None);

        box.Clock.Now = AlertTime;
        var outcome = await box.Dispatcher.DeliverAsync(box.Status.Snapshot().Plan[0], CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Acknowledged, outcome);
        Assert.Empty(box.Pushover.Requests);
        Assert.Empty(box.Store.Claims);
    }

    private static AlertSettings WithBackup(int seconds) =>
        AlertSettings.Defaults(new AlertsOptions()) with { BackupDelaySeconds = seconds };

    [Fact]
    public async Task With_a_backup_delay_an_alarm_goes_that_long_after_its_time_and_the_worker_wakes_for_it()
    {
        var box = New();
        await box.Store.SaveSettingsAsync(WithBackup(300), Eight, CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);

        box.Clock.Now = AlertTime;
        var wake = await box.Worker.TickAsync(CancellationToken.None);
        Assert.Empty(box.Pushover.Requests);
        Assert.Empty(box.Store.Claims);
        Assert.Equal(AlertTime + Duration.FromMinutes(5), wake);

        box.Clock.Now = AlertTime + Duration.FromMinutes(5) - Duration.FromSeconds(1);
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Empty(box.Pushover.Requests);

        box.Clock.Now = AlertTime + Duration.FromMinutes(5);
        await box.Worker.TickAsync(CancellationToken.None);
        Assert.Equal("2", Assert.Single(box.Pushover.Requests).Form["priority"]);
    }

    [Fact]
    public async Task With_a_backup_delay_an_alarm_acknowledged_on_the_phone_in_time_is_never_sent()
    {
        var box = New();
        await box.Store.SaveSettingsAsync(WithBackup(300), Eight, CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);
        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        // The phone rang at 08:45 and was answered at 08:47.
        await box.Store.AcknowledgeAsync(
            "standup@google.com|20261028T130000Z", Start, "phone", AlertTime + Duration.FromMinutes(2), CancellationToken.None);
        box.Clock.Now = AlertTime + Duration.FromMinutes(5);
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Empty(box.Pushover.Requests);
    }

    [Fact]
    public async Task A_backup_delay_never_holds_a_notification()
    {
        var box = New(Standup(Popup("-PT15M")));
        await box.Store.SaveSettingsAsync(WithBackup(300) with { DefaultType = AlertTypes.Notification }, Eight, CancellationToken.None);
        await box.Worker.TickAsync(CancellationToken.None);

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal("0", Assert.Single(box.Pushover.Requests).Form["priority"]);
    }

    [Fact]
    public async Task Acknowledged_while_the_emergency_message_was_on_its_way_its_repeats_are_cancelled()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        box.Pushover.Then(() => RecordingHandler.Text(
            HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\",\"receipt\":\"racereceipt0000000000000000001\"}", "application/json"));
        // The phone answers after the claim and before the receipt is stored:
        // the acknowledgement finds no receipt to cancel.
        box.Store.BeforeRecordOutcome = () => box.Store
            .AcknowledgeAsync("standup@google.com|20261028T130000Z", Start, "phone", AlertTime, CancellationToken.None)
            .GetAwaiter().GetResult();

        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal(
            [PushoverClient.Endpoint, PushoverClient.CancelEndpoint("racereceipt0000000000000000001")],
            box.Pushover.Requests.Select(request => request.Url.ToString()));
    }

    [Fact]
    public async Task An_emergency_send_keeps_its_receipt_and_a_lower_priority_keeps_none()
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        box.Pushover.Then(() => RecordingHandler.Text(
            HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\",\"receipt\":\"keptreceipt0000000000000000001\"}", "application/json"));
        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        var delivery = await box.Store.DeliveryAsync("standup@google.com|20261028T130000Z", CancellationToken.None);
        Assert.Equal("keptreceipt0000000000000000001", delivery?.Receipt);

        var high = New(Standup(Popup("-PT15M")));
        await high.Store.SaveSettingsAsync(
            AlertSettings.Defaults(new AlertsOptions()) with { DefaultType = AlertTypes.Notification }, Eight, CancellationToken.None);
        high.Pushover.Then(() => RecordingHandler.Text(
            HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\",\"receipt\":\"notkept0000000000000000000001\"}", "application/json"));
        high.Clock.Now = AlertTime;
        await high.Worker.TickAsync(CancellationToken.None);
        Assert.Null((await high.Store.DeliveryAsync("standup@google.com|20261028T130000Z", CancellationToken.None))?.Receipt);
    }

    [Theory]
    [InlineData("{\"status\":1,\"receipt\":\"has spaces in it\"}")]
    [InlineData("{\"status\":1,\"receipt\":\"../../messages\"}")]
    [InlineData("{\"status\":1,\"receipt\":42}")]
    [InlineData("not json")]
    [InlineData("[]")]
    public async Task A_receipt_that_is_not_letters_and_digits_is_not_kept(string answer)
    {
        var box = New();
        await box.Worker.TickAsync(CancellationToken.None);
        box.Pushover.Then(() => RecordingHandler.Text(HttpStatusCode.OK, answer, "application/json"));
        box.Clock.Now = AlertTime;
        await box.Worker.TickAsync(CancellationToken.None);

        Assert.Equal("sent", box.Store.Claims.Values.Single());
        Assert.Null((await box.Store.DeliveryAsync("standup@google.com|20261028T130000Z", CancellationToken.None))?.Receipt);
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
