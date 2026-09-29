using System.Net;
using aberaTech.Scheduling.Alerts;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;
using static aberaTech.Server.Tests.Alerts.Feeds;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The #critical word written back to Google Calendar, and the calendar
/// check before any write. Google is <see cref="FakeGoogleCalendar"/>: the
/// real client runs on top of it.
/// </summary>
public sealed class CalendarWriteTests : IDisposable
{
    private const string FeedUrl = "https://calendar.google.com/calendar/ical/owner%40example.test/private-0123456789abcdef/basic.ics";

    private static readonly Instant Eight = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly FakeGoogleCalendar _google = new();
    private readonly ServiceProvider _provider;

    public CalendarWriteTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new FakeClock(Eight));
        services.AddSingleton(new AlertsOptions { CalendarIcsUrl = FeedUrl });
        services.AddSingleton<IAlertStore>(new InMemoryAlertStore());
        services.AddSingleton<IAlertCalendarGrant>(_google);
        services.AddCalendarAlerts();
        services.AddHttpClient<GoogleAlertEvents>().ConfigurePrimaryHttpMessageHandler(_google.Handler);
        _provider = services.BuildServiceProvider();
    }

    private GoogleAlertEvents Writer => _provider.GetRequiredService<GoogleAlertEvents>();

    [Theory]
    [InlineData("", "#critical")]
    [InlineData("Bring the slides.", "Bring the slides.\n#critical")]
    [InlineData("Bring the slides.\n\n", "Bring the slides.\n#critical")]
    [InlineData("Agenda\n#Critical\nnotes", "Agenda\n#Critical\nnotes")]
    [InlineData("see #CRITICAL", "see #CRITICAL")]
    public void Adding_the_mark_puts_it_on_its_own_line_once(string before, string after)
    {
        Assert.Equal(after, CriticalText.Add(before));
        Assert.Equal(after, CriticalText.Add(CriticalText.Add(before)));
    }

    [Theory]
    [InlineData("#critical", "")]
    [InlineData("Bring the slides.\n#critical", "Bring the slides.")]
    [InlineData("Bring #Critical docs", "Bring docs")]
    [InlineData("Agenda\n\n#critical\n\nNotes", "Agenda\n\nNotes")]
    [InlineData("#critical\n#CRITICAL\nkeep", "keep")]
    [InlineData("a #critical b #Critical c", "a b c")]
    [InlineData("#criticality and a#critical stay", "#criticality and a#critical stay")]
    [InlineData("Line one\r\n#critical\r\nLine two", "Line one\nLine two")]
    public void Removing_the_mark_removes_every_standalone_one_and_the_line_it_leaves(string before, string after)
    {
        Assert.Equal(after, CriticalText.Remove(before));
        Assert.False(AlertPlanner.IsMarked(CriticalText.Remove(before)));
    }

    [Theory]
    [InlineData(FeedUrl, null, "owner@example.test")]
    [InlineData("https://calendar.google.com/calendar/ical/c_abc123%40group.calendar.google.com/private-x/basic.ics", "Work", "c_abc123@group.calendar.google.com")]
    [InlineData("https://calendar.google.com/calendar/ical/owner%40example.test/private-x/basic.ics", "someone@else.test", "owner@example.test")]
    [InlineData("https://calendar.example.test/basic.ics", "owner@example.test", "owner@example.test")]
    [InlineData("https://calendar.example.test/basic.ics", "Work", null)]
    [InlineData("https://calendar.google.com/elsewhere.ics", "owner@example.test", null)]
    [InlineData(null, null, null)]
    public void The_feed_calendar_is_the_id_in_googles_address_and_otherwise_an_address_as_its_name(
        string? url, string? name, string? identity) =>
        Assert.Equal(identity, AlertFeedIdentity.Of(url, name));

    [Fact]
    public void A_grant_on_the_primary_calendar_is_the_address_it_was_connected_with()
    {
        Assert.Equal("Owner@Example.test", AlertFeedIdentity.Of(new CalendarGrant("primary", "Owner@Example.test", true)));
        Assert.Equal("c_1@group.calendar.google.com", AlertFeedIdentity.Of(new CalendarGrant("c_1@group.calendar.google.com", "o@x.test", true)));
        Assert.True(AlertFeedIdentity.Same("owner@example.test", new CalendarGrant("primary", "Owner@Example.test", true)));
        Assert.False(AlertFeedIdentity.Same(null, new CalendarGrant("primary", "owner@example.test", true)));
        Assert.False(AlertFeedIdentity.Same("other@example.test", new CalendarGrant("primary", "owner@example.test", true)));
    }

    [Fact]
    public async Task Alarm_adds_the_mark_once_and_a_second_alarm_writes_nothing()
    {
        _google.Seed("standup@google.com", "Room 1 dial-in");

        Assert.Null(await Writer.SetMarkAsync("standup@google.com", alarm: true, CancellationToken.None));
        Assert.Null(await Writer.SetMarkAsync("standup@google.com", alarm: true, CancellationToken.None));

        Assert.Equal("Room 1 dial-in\n#critical", _google.Description("standup@google.com"));
        var write = Assert.Single(_google.Writes);
        Assert.Equal("patch", write.Kind);
    }

    [Fact]
    public async Task Any_other_type_removes_every_mark_including_one_mid_text()
    {
        _google.Seed("standup@google.com", "Bring #Critical docs\n\n#critical\n\nDial-in");

        Assert.Null(await Writer.SetMarkAsync("standup@google.com", alarm: false, CancellationToken.None));

        Assert.Equal("Bring docs\n\nDial-in", _google.Description("standup@google.com"));
        Assert.Null(await Writer.SetMarkAsync("standup@google.com", alarm: false, CancellationToken.None));
        Assert.Single(_google.Writes);
    }

    [Fact]
    public async Task A_repeating_event_is_patched_once_on_the_series_and_not_on_a_moved_occurrence()
    {
        _google.Seed("weekly@google.com", "", recurring: true);

        Assert.Null(await Writer.SetMarkAsync("weekly@google.com", alarm: true, CancellationToken.None));

        var write = Assert.Single(_google.Writes);
        Assert.Equal("weekly@google.com", write.EventId);
        Assert.Equal("#critical", _google.Description("weekly@google.com"));
    }

    [Fact]
    public async Task No_connected_calendar_or_no_edit_access_writes_nothing_and_says_so()
    {
        _google.Seed("standup@google.com");
        _google.Grant = null;
        Assert.Equal(CalendarWriteMessages.NotConnected, await Writer.SetMarkAsync("standup@google.com", true, CancellationToken.None));

        _google.Grant = new CalendarGrant("primary", Owner, CanEdit: false);
        Assert.Equal(CalendarWriteMessages.NotConnected, await Writer.SetMarkAsync("standup@google.com", true, CancellationToken.None));

        Assert.Equal(0, _google.Calls);
    }

    [Fact]
    public async Task A_connected_calendar_that_is_not_the_feeds_is_never_written()
    {
        _google.Seed("standup@google.com");
        _google.Grant = new CalendarGrant("primary", "someone@example.test", true);

        Assert.Equal(CalendarWriteMessages.OtherCalendar, await Writer.SetMarkAsync("standup@google.com", true, CancellationToken.None));
        Assert.Equal(0, _google.Calls);
    }

    [Fact]
    public async Task A_refused_sign_in_an_unknown_event_and_a_hashed_uid_each_say_so()
    {
        Assert.Equal(CalendarWriteMessages.NotFound, await Writer.SetMarkAsync("missing@google.com", true, CancellationToken.None));
        Assert.Equal(CalendarWriteMessages.NotFound, await Writer.SetMarkAsync("sha256:" + new string('a', 64), true, CancellationToken.None));
        Assert.Equal(CalendarWriteMessages.NotFound, await Writer.SetMarkAsync("", true, CancellationToken.None));

        _google.TokenRefused = true;
        Assert.Equal(CalendarWriteMessages.SignInRefused, await Writer.SetMarkAsync("missing@google.com", true, CancellationToken.None));
    }

    [Fact]
    public async Task An_event_organised_by_someone_else_is_refused_before_and_by_google()
    {
        _google.Seed("invite@google.com", organisedHere: false);
        Assert.Equal(CalendarWriteMessages.OrganisedElsewhere, await Writer.SetMarkAsync("invite@google.com", true, CancellationToken.None));
        Assert.Empty(_google.Writes);

        // Google's own refusal of the patch names the same reason.
        Assert.Equal("forbiddenForNonOrganizer", GoogleAlertEvents.Reason(
            "{\"error\":{\"code\":403,\"errors\":[{\"reason\":\"forbiddenForNonOrganizer\"}]}}"));
        Assert.Null(GoogleAlertEvents.Reason("not json"));
        Assert.Null(GoogleAlertEvents.Reason("{\"error\":{}}"));
    }

    [Fact]
    public async Task Google_refusing_or_not_answering_is_a_short_message_with_no_url_or_token()
    {
        _google.Seed("standup@google.com");
        _google.Refusing = (HttpStatusCode.Forbidden, "insufficientPermissions");
        var refused = await Writer.SetMarkAsync("standup@google.com", true, CancellationToken.None);
        Assert.Equal("Google refused the change (HTTP 403).", refused);

        _google.Refusing = null;
        _google.Unreachable = true;
        var unreachable = await Writer.SetMarkAsync("standup@google.com", true, CancellationToken.None);
        Assert.Equal(CalendarWriteMessages.Unreachable, unreachable);

        foreach (var message in new[] { refused, unreachable })
        {
            Assert.DoesNotContain("http", message!.Replace("HTTP", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("feedsecret", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_new_event_goes_with_one_popup_at_the_lead_and_the_mark_only_for_an_alarm()
    {
        var start = Eight + Duration.FromHours(4);
        var alarm = await Writer.CreateAsync(
            new NewAlertEvent("Dentist", "Main St", start, start + Duration.FromMinutes(30), 20, AlertTypes.Alarm), CancellationToken.None);
        var plain = await Writer.CreateAsync(
            new NewAlertEvent("Lunch", null, start, start + Duration.FromMinutes(30), 5, AlertTypes.Notification), CancellationToken.None);

        Assert.NotNull(alarm.Event);
        Assert.True(alarm.Event.Critical);
        Assert.False(plain.Event!.Critical);
        Assert.Equal(["insert", "insert"], _google.Writes.Select(write => write.Kind));
        Assert.Equal("#critical", _google.Writes[0].Description);
        Assert.Equal(20, _google.Writes[0].PopupMinutes);
        Assert.Null(_google.Writes[1].Description);
        Assert.Equal(5, _google.Writes[1].PopupMinutes);
        Assert.Equal(_google.Inserted, [alarm.Event.EventId, plain.Event.EventId]);
    }

    [Fact]
    public async Task A_new_event_with_no_calendar_or_the_wrong_one_is_a_conflict_and_googles_refusal_is_not()
    {
        var start = Eight + Duration.FromHours(4);
        var request = new NewAlertEvent("Dentist", null, start, start + Duration.FromMinutes(30), 20, AlertTypes.None);

        _google.Grant = null;
        var none = await Writer.CreateAsync(request, CancellationToken.None);
        Assert.True(none.Conflict);
        Assert.Equal(CalendarWriteMessages.NotConnected, none.Problem);

        _google.Grant = new CalendarGrant("primary", "someone@example.test", true);
        var other = await Writer.CreateAsync(request, CancellationToken.None);
        Assert.True(other.Conflict);
        Assert.Equal(CalendarWriteMessages.OtherCalendar, other.Problem);

        _google.Grant = new CalendarGrant("primary", Owner, true);
        _google.Refusing = (HttpStatusCode.BadRequest, null);
        var refused = await Writer.CreateAsync(request, CancellationToken.None);
        Assert.False(refused.Conflict);
        Assert.Equal("Google refused the change (HTTP 400).", refused.Problem);

        _google.Refusing = null;
        _google.Unreachable = true;
        var unreachable = await Writer.CreateAsync(request, CancellationToken.None);
        Assert.Equal(CalendarWriteMessages.Unreachable, unreachable.Problem);
        Assert.Null(unreachable.Event);
    }

    [Fact]
    public async Task A_connection_that_cannot_be_read_or_a_token_refresh_that_times_out_says_so()
    {
        var start = Eight + Duration.FromHours(4);
        var request = new NewAlertEvent("Dentist", null, start, start + Duration.FromMinutes(30), 20, AlertTypes.None);

        var down = WriterWith(new Failing(_google, grantFails: true));
        Assert.Equal(CalendarWriteMessages.Unchecked, await down.SetMarkAsync("standup@google.com", true, CancellationToken.None));
        var noRow = await down.CreateAsync(request, CancellationToken.None);
        Assert.Equal(CalendarWriteMessages.Unchecked, noRow.Problem);
        Assert.False(noRow.Conflict);

        var slow = WriterWith(new Failing(_google, grantFails: false));
        Assert.Equal(CalendarWriteMessages.Unreachable, await slow.SetMarkAsync("standup@google.com", true, CancellationToken.None));
        Assert.Equal(0, _google.Calls);
    }

    [Theory]
    [InlineData("{\"id\":\"x\"}")]
    [InlineData("{\"id\":\"x\",\"iCalUID\":\"\"}")]
    [InlineData("not json")]
    public async Task A_created_event_without_a_usable_uid_is_not_kept(string answer)
    {
        var start = Eight + Duration.FromHours(4);
        var writer = new GoogleAlertEvents(
            new HttpClient(new RecordingHandler(() => RecordingHandler.Text(HttpStatusCode.OK, answer, "application/json"))),
            _google,
            new AlertsOptions { CalendarIcsUrl = FeedUrl },
            new AlertsStatus(new AlertsOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleAlertEvents>.Instance);

        var outcome = await writer.CreateAsync(
            new NewAlertEvent("Dentist", null, start, start + Duration.FromMinutes(30), 20, AlertTypes.None), CancellationToken.None);

        Assert.Null(outcome.Event);
        Assert.NotNull(outcome.Problem);
    }

    [Fact]
    public void A_list_answer_with_no_usable_series_is_not_found()
    {
        Assert.Null(GoogleAlertEvents.Series("{}"));
        Assert.Null(GoogleAlertEvents.Series("{\"items\":{}}"));
        Assert.Null(GoogleAlertEvents.Series("{\"items\":[{\"id\":\"a\",\"status\":\"cancelled\"},{\"description\":\"no id\"}]}"));
        Assert.Equal(("b", "", true), GoogleAlertEvents.Series("{\"items\":[{\"id\":\"b\"}]}"));
    }

    private GoogleAlertEvents WriterWith(IAlertCalendarGrant grant) => new(
        new HttpClient(_google.Handler()),
        grant,
        new AlertsOptions { CalendarIcsUrl = FeedUrl },
        new AlertsStatus(new AlertsOptions()),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleAlertEvents>.Instance);

    /// <summary>A connection whose row cannot be read, or whose token refresh times out.</summary>
    private sealed class Failing(FakeGoogleCalendar inner, bool grantFails) : IAlertCalendarGrant
    {
        public Task<CalendarGrant?> CurrentAsync(CancellationToken cancellationToken) =>
            grantFails ? Task.FromException<CalendarGrant?>(new TimeoutException("database down")) : inner.CurrentAsync(cancellationToken);

        public Task<string?> AccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromException<string?>(new TaskCanceledException("token refresh timed out"));
    }

    public void Dispose() => _provider.Dispose();
}
