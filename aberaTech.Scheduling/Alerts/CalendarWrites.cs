using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The #critical word in an event's description, added and removed as the
/// owner changes the event's type. Pure: text in, text out.
/// </summary>
public static class CriticalText
{
    /// <summary>
    /// The description with the mark on a line of its own at the end. The
    /// same text when it already carries the mark anywhere, so a second
    /// Alarm changes nothing.
    /// </summary>
    public static string Add(string? description)
    {
        var text = description ?? "";
        if (AlertPlanner.IsMarked(text)) return text;
        var kept = text.TrimEnd();
        return kept.Length == 0 ? AlertPlanner.CriticalMark : $"{kept}\n{AlertPlanner.CriticalMark}";
    }

    /// <summary>
    /// The description with every standalone mark removed, in any case. A
    /// line that held only the mark goes, and so does the blank line it
    /// leaves between two paragraphs. The same text when there is no mark.
    /// </summary>
    public static string Remove(string? description)
    {
        var text = description ?? "";
        if (!AlertPlanner.IsMarked(text)) return text;

        var kept = new List<string>();
        var dropped = false;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!AlertPlanner.IsMarked(line))
            {
                // One blank line where a paragraph break was, never two.
                if (dropped && line.Trim().Length == 0 && kept.Count > 0 && kept[^1].Trim().Length == 0) continue;
                kept.Add(line);
                dropped = false;
                continue;
            }

            var rest = string.Join(' ', AlertPlanner.Unmark(line).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (rest.Length > 0)
            {
                kept.Add(rest);
                dropped = false;
            }
            else
            {
                dropped = true;
            }
        }

        return string.Join('\n', kept).Trim('\n', ' ');
    }
}

/// <summary>
/// Which calendar the secret iCal feed reads, so a write goes to the same
/// one. Null when it cannot be told.
/// </summary>
/// <remarks>
/// Google's secret address carries the calendar's id in its path:
/// <c>https://calendar.google.com/calendar/ical/{id}/private-{key}/basic.ics</c>.
/// That id is the account address for a primary calendar, and it cannot be
/// renamed, so it is the comparison. X-WR-CALNAME is the calendar's display
/// name. For a primary calendar Google sets it to the address, and the owner
/// can rename it, so it is used only for a feed address of another shape
/// (the development calendar) and only when it is an address.
/// </remarks>
public static class AlertFeedIdentity
{
    public static string? Of(string? feedUrl, string? calendarName)
    {
        if (Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, "calendar.google.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments is ["calendar", "ical", var id, ..] ? Uri.UnescapeDataString(id).Trim() : null;
        }

        return calendarName is { } name && name.Contains('@', StringComparison.Ordinal) ? name.Trim() : null;
    }

    /// <summary>
    /// The calendar a stored grant writes to. Google's id for the primary
    /// calendar is the account's address, which the grant recorded at
    /// connect time.
    /// </summary>
    public static string Of(CalendarGrant grant) =>
        string.Equals(grant.CalendarId, "primary", StringComparison.Ordinal) ? grant.ConnectedEmail : grant.CalendarId;

    public static bool Same(string? feed, CalendarGrant grant) =>
        feed is not null && string.Equals(feed, Of(grant), StringComparison.OrdinalIgnoreCase);
}

/// <summary>The stored Google Calendar connection, without its token.</summary>
/// <param name="CanEdit">The grant includes the events scope.</param>
public sealed record CalendarGrant(string CalendarId, string ConnectedEmail, bool CanEdit);

/// <summary>The stored Google Calendar connection and a way to use it.</summary>
public interface IAlertCalendarGrant
{
    /// <summary>Null when no calendar is connected.</summary>
    Task<CalendarGrant?> CurrentAsync(CancellationToken cancellationToken);

    /// <summary>A usable access token. Null when Google refused the stored one.</summary>
    Task<string?> AccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>The connection /schedule/admin stores (HostCalendarCredentials).</summary>
public sealed class DatabaseAlertCalendarGrant(SchedulingDbContext database, GoogleAccessTokens tokens) : IAlertCalendarGrant
{
    public async Task<CalendarGrant?> CurrentAsync(CancellationToken cancellationToken)
    {
        var credential = await database.HostCalendarCredentials.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return credential is null
            ? null
            : new CalendarGrant(
                credential.CalendarId,
                credential.ConnectedEmail,
                credential.GrantedScopes.Contains(CalendarAdminEndpoints.EventsScope, StringComparison.Ordinal));
    }

    public async Task<string?> AccessTokenAsync(CancellationToken cancellationToken)
    {
        var credential = await database.HostCalendarCredentials.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return credential is null ? null : await tokens.GetAccessTokenAsync(credential.ProtectedRefreshToken, cancellationToken);
    }
}

/// <summary>An event the owner or a paired phone asked for, before Google has it.</summary>
public sealed record NewAlertEvent(
    string Title,
    string? Location,
    Instant StartsAt,
    Instant EndsAt,
    int LeadMinutes,
    string Type);

/// <summary>How a create went: the stored event, or a message and whether it is a conflict (409) or Google's refusal (502).</summary>
public sealed record CreateOutcome(CreatedAlertEvent? Event, string? Problem, bool Conflict);

/// <summary>An edit of one listed occurrence, or of its whole series, as the route checked it.</summary>
/// <param name="LocationSet">The body carried a location. False leaves the event's own.</param>
/// <param name="Location">The new location. Null clears it when <paramref name="LocationSet"/> is true.</param>
/// <param name="StartsAt">The picked occurrence's new start.</param>
/// <param name="DurationMinutes">The new length. Null keeps the event's own.</param>
/// <param name="LeadMinutes">One popup reminder at this lead. Null leaves the reminders alone.</param>
public sealed record AlertEventEdit(
    string Title,
    bool LocationSet,
    string? Location,
    Instant StartsAt,
    int? DurationMinutes,
    int? LeadMinutes);

/// <summary>
/// How an edit or a deletion went. Problem is null when Google took it.
/// Series says whether the whole series was changed: false for an event
/// that does not repeat, whatever was asked. TimeZone is the series' zone.
/// </summary>
public sealed record EventWriteOutcome(string? Problem, bool Conflict, bool Series = false, string? TimeZone = null)
{
    public static EventWriteOutcome Failed(string problem) =>
        new(problem, problem is CalendarWriteMessages.NotConnected or CalendarWriteMessages.OtherCalendar or CalendarWriteMessages.OnlyOrganizer);
}

/// <summary>An event as events.list or events.instances answers it: the parts an edit reads.</summary>
/// <param name="Master">Not an instance of a series: no recurringEventId.</param>
internal sealed record GoogleEvent(
    string Id,
    bool OrganisedHere,
    bool Recurring,
    Instant? Start,
    Instant? End,
    string? TimeZone,
    bool Master);

/// <summary>
/// The messages the page and the phone show when a write to Google does not
/// happen. Fixed text: never a token, a URL or Google's own answer.
/// </summary>
public static class CalendarWriteMessages
{
    public const string NotConnected = "Google Calendar is not connected with edit access.";

    public const string OtherCalendar = "The connected calendar is not the one alerts read.";

    public const string SignInRefused = "Google refused the stored calendar sign-in. Connect the calendar again.";

    public const string NotFound = "Google Calendar has no such event on the connected calendar.";

    public const string OrganisedElsewhere = "Google refused the change: this event is organised by someone else.";

    /// <summary>An edit or a deletion of an invitation: Google lets only its organiser change it.</summary>
    public const string OnlyOrganizer = "Only the organizer can change this event.";

    public const string Unreachable = "Google Calendar did not answer.";

    public const string Unchecked = "The calendar connection could not be read.";

    public static string Refused(HttpStatusCode status) => $"Google refused the change (HTTP {(int)status}).";
}

/// <summary>
/// Writes to the owner's Google Calendar for /alerts: the #critical word in
/// an event's description, and a new event with its popup reminder. Raw
/// HTTP like <see cref="GoogleCalendarInvites"/>, through the same stored
/// grant.
/// </summary>
/// <remarks>
/// Every write first checks that the grant has the events scope and that its
/// calendar is the one the iCal feed reads (<see cref="AlertFeedIdentity"/>).
/// A write to another calendar would never show in the alerts.
///
/// The client is registered with its loggers removed. Nothing here logs a
/// URL, a token, a title or a description: a status code at most.
/// </remarks>
public sealed class GoogleAlertEvents(
    HttpClient http,
    IAlertCalendarGrant grants,
    AlertsOptions options,
    AlertsStatus status,
    ILogger<GoogleAlertEvents> logger)
{
    public const string Api = "https://www.googleapis.com/calendar/v3/calendars/";

    /// <summary>
    /// Adds the mark for an alarm, removes it for anything else. Null when
    /// the description now says what the type says, or already did. A
    /// repeating event is one series in Google with one UID, so the series
    /// is patched once and every occurrence follows.
    /// </summary>
    public async Task<string?> SetMarkAsync(string eventId, bool alarm, CancellationToken cancellationToken)
    {
        // A UID too long for the key column is kept as its hash, which Google cannot look up.
        if (eventId.Length == 0 || eventId.StartsWith("sha256:", StringComparison.Ordinal)) return CalendarWriteMessages.NotFound;

        var (calendar, token, problem) = await ReadyAsync(cancellationToken);
        if (problem is not null) return problem;

        try
        {
            using var find = Request(HttpMethod.Get, $"{Api}{Escape(calendar!)}/events?iCalUID={Escape(eventId)}&maxResults=50", token!);
            using var found = await http.SendAsync(find, cancellationToken);
            if (!found.IsSuccessStatusCode)
            {
                logger.LogWarning("Google answered {StatusCode} looking up an event for /alerts.", (int)found.StatusCode);
                return CalendarWriteMessages.Refused(found.StatusCode);
            }

            var series = Series(await found.Content.ReadAsStringAsync(cancellationToken));
            if (series is null) return CalendarWriteMessages.NotFound;
            if (!series.Value.OrganisedHere) return CalendarWriteMessages.OrganisedElsewhere;

            var description = series.Value.Description;
            var next = alarm ? CriticalText.Add(description) : CriticalText.Remove(description);
            if (next == description) return null;

            using var patch = Request(
                HttpMethod.Patch, $"{Api}{Escape(calendar!)}/events/{Escape(series.Value.Id)}?sendUpdates=none", token!);
            patch.Content = new StringContent(JsonSerializer.Serialize(new { description = next }), Encoding.UTF8, "application/json");
            using var patched = await http.SendAsync(patch, cancellationToken);
            if (patched.IsSuccessStatusCode) return null;

            logger.LogWarning("Google answered {StatusCode} to a description change for /alerts.", (int)patched.StatusCode);
            return Reason(await patched.Content.ReadAsStringAsync(cancellationToken)) == "forbiddenForNonOrganizer"
                ? CalendarWriteMessages.OrganisedElsewhere
                : CalendarWriteMessages.Refused(patched.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google Calendar did not answer a write for /alerts ({Failure}).", exception.GetType().Name);
            return CalendarWriteMessages.Unreachable;
        }
    }

    /// <summary>
    /// Creates the event with one popup reminder at the lead, and #critical
    /// in the description for an alarm. Google's own default reminders are
    /// off, so the alert time is the lead the caller chose.
    /// </summary>
    public async Task<CreateOutcome> CreateAsync(NewAlertEvent request, CancellationToken cancellationToken)
    {
        var (calendar, token, problem) = await ReadyAsync(cancellationToken);
        if (problem is not null)
        {
            return new CreateOutcome(
                null, problem, problem is CalendarWriteMessages.NotConnected or CalendarWriteMessages.OtherCalendar);
        }

        var critical = request.Type == AlertTypes.Alarm;
        var body = JsonSerializer.Serialize(new
        {
            summary = request.Title,
            location = request.Location,
            description = critical ? AlertPlanner.CriticalMark : null,
            start = new { dateTime = InstantPattern.ExtendedIso.Format(request.StartsAt) },
            end = new { dateTime = InstantPattern.ExtendedIso.Format(request.EndsAt) },
            reminders = new { useDefault = false, overrides = new[] { new { method = "popup", minutes = request.LeadMinutes } } }
        }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        try
        {
            using var insert = Request(HttpMethod.Post, $"{Api}{Escape(calendar!)}/events?sendUpdates=none", token!);
            insert.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(insert, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google answered {StatusCode} to a new event from /alerts.", (int)response.StatusCode);
                return new CreateOutcome(null, CalendarWriteMessages.Refused(response.StatusCode), false);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var uid = document.RootElement.TryGetProperty("iCalUID", out var value) ? value.GetString() : null;
            if (string.IsNullOrWhiteSpace(uid) || uid.Length > AlertPlanner.MaxKeyLength - 17)
            {
                logger.LogWarning("Google created an event for /alerts and answered without a usable UID.");
                return new CreateOutcome(null, CalendarWriteMessages.Refused(response.StatusCode), false);
            }

            return new CreateOutcome(
                new CreatedAlertEvent(
                    uid, request.Title, request.Location, request.StartsAt, request.EndsAt, request.LeadMinutes, critical),
                null,
                false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google Calendar did not answer a new event from /alerts ({Failure}).", exception.GetType().Name);
            return new CreateOutcome(null, CalendarWriteMessages.Unreachable, false);
        }
    }

    /// <summary>
    /// Changes one occurrence, or its series, and answers why not when
    /// Google did not take it. The event is found by its UID
    /// (events.list?iCalUID=). One occurrence of a series is the instance
    /// Google lists at that start (events.instances with timeMin and
    /// timeMax around it), patched on its own id. A series moves its master
    /// by the picked occurrence's change in date and wall-clock time, in the
    /// series' zone, and keeps its RRULE. The description, and so #critical,
    /// is never sent.
    /// </summary>
    /// <param name="zone">The zone to write a time in when Google names none.</param>
    public async Task<EventWriteOutcome> EditAsync(
        PlannedAlert occurrence, bool series, AlertEventEdit edit, DateTimeZone zone, CancellationToken cancellationToken)
    {
        var found = await FindAsync(occurrence, series, cancellationToken);
        if (found.Problem is not null) return EventWriteOutcome.Failed(found.Problem);
        var (calendar, token, master, instance) = (found.Calendar!, found.Token!, found.Master!, found.Instance);

        try
        {
            string target;
            Instant start;
            Instant? end;
            string? timeZone;
            if (master.Recurring && series)
            {
                // Google names the zone a series repeats in. Its time of day
                // is kept on that clock.
                timeZone = master.TimeZone ?? zone.Id;
                var seriesZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone) ?? zone;
                var from = master.Start ?? occurrence.StartsAt;
                start = EventChanges.Shift(from, occurrence.StartsAt, edit.StartsAt, seriesZone);
                end = Ends(start, edit.DurationMinutes, master.Start, master.End);
                target = master.Id;
                zone = seriesZone;
            }
            else
            {
                var picked = instance ?? master;
                timeZone = picked.TimeZone;
                start = edit.StartsAt;
                end = Ends(start, edit.DurationMinutes, picked.Start ?? occurrence.StartsAt, picked.End ?? occurrence.EndsAt);
                target = picked.Id;
                if (timeZone is not null && DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone) is { } own) zone = own;
            }

            var body = new Dictionary<string, object?>
            {
                ["summary"] = edit.Title,
                ["start"] = Time(start, timeZone, zone),
                ["end"] = Time(end ?? start, timeZone, zone)
            };
            // Google's patch leaves out what the body leaves out. An empty
            // location clears it.
            if (edit.LocationSet) body["location"] = edit.Location ?? "";
            if (edit.LeadMinutes is { } lead)
            {
                body["reminders"] = new { useDefault = false, overrides = new[] { new { method = "popup", minutes = lead } } };
            }

            using var patch = Request(HttpMethod.Patch, $"{Api}{Escape(calendar)}/events/{Escape(target)}?sendUpdates=none", token);
            patch.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var patched = await http.SendAsync(patch, cancellationToken);
            if (patched.IsSuccessStatusCode) return new EventWriteOutcome(null, false, master.Recurring && series, timeZone);

            logger.LogWarning("Google answered {StatusCode} to an event edit from /alerts.", (int)patched.StatusCode);
            return EventWriteOutcome.Failed(Refusal(patched.StatusCode, await patched.Content.ReadAsStringAsync(cancellationToken)));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google Calendar did not answer an event edit from /alerts ({Failure}).", exception.GetType().Name);
            return EventWriteOutcome.Failed(CalendarWriteMessages.Unreachable);
        }
    }

    /// <summary>
    /// Deletes one occurrence, or the whole event. One occurrence of a series
    /// is its instance id, deleted: Google keeps the series and cancels that
    /// one. An event that does not repeat is deleted whatever was asked.
    /// Gone already (410) is what was asked for.
    /// </summary>
    public async Task<EventWriteOutcome> DeleteAsync(PlannedAlert occurrence, bool series, CancellationToken cancellationToken)
    {
        var found = await FindAsync(occurrence, series, cancellationToken);
        if (found.Problem is not null) return EventWriteOutcome.Failed(found.Problem);
        var target = (found.Instance ?? found.Master!).Id;

        try
        {
            using var delete = Request(
                HttpMethod.Delete, $"{Api}{Escape(found.Calendar!)}/events/{Escape(target)}?sendUpdates=none", found.Token!);
            using var deleted = await http.SendAsync(delete, cancellationToken);
            if (deleted.IsSuccessStatusCode || deleted.StatusCode == HttpStatusCode.Gone)
            {
                return new EventWriteOutcome(null, false, found.Master!.Recurring && series, found.Master.TimeZone);
            }

            logger.LogWarning("Google answered {StatusCode} to an event deletion from /alerts.", (int)deleted.StatusCode);
            return EventWriteOutcome.Failed(Refusal(deleted.StatusCode, await deleted.Content.ReadAsStringAsync(cancellationToken)));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google Calendar did not answer an event deletion from /alerts ({Failure}).", exception.GetType().Name);
            return EventWriteOutcome.Failed(CalendarWriteMessages.Unreachable);
        }
    }

    /// <summary>
    /// The master event for an occurrence, and for one occurrence of a
    /// series, the instance at its start. Or why there is none.
    /// </summary>
    private async Task<(string? Calendar, string? Token, GoogleEvent? Master, GoogleEvent? Instance, string? Problem)> FindAsync(
        PlannedAlert occurrence, bool series, CancellationToken cancellationToken)
    {
        // A UID too long for the key column is kept as its hash, which Google cannot look up.
        var uid = occurrence.EventId;
        if (uid.Length == 0 || uid.StartsWith("sha256:", StringComparison.Ordinal))
        {
            return (null, null, null, null, CalendarWriteMessages.NotFound);
        }

        var (calendar, token, problem) = await ReadyAsync(cancellationToken);
        if (problem is not null) return (null, null, null, null, problem);

        try
        {
            using var find = Request(HttpMethod.Get, $"{Api}{Escape(calendar!)}/events?iCalUID={Escape(uid)}&maxResults=50", token!);
            using var listed = await http.SendAsync(find, cancellationToken);
            if (!listed.IsSuccessStatusCode)
            {
                logger.LogWarning("Google answered {StatusCode} looking up an event for /alerts.", (int)listed.StatusCode);
                return (null, null, null, null, CalendarWriteMessages.Refused(listed.StatusCode));
            }

            var master = Master(await listed.Content.ReadAsStringAsync(cancellationToken));
            if (master is null) return (null, null, null, null, CalendarWriteMessages.NotFound);
            if (!master.OrganisedHere) return (null, null, null, null, CalendarWriteMessages.OnlyOrganizer);
            if (!master.Recurring || series) return (calendar, token, master, null, null);

            // developers.google.com/calendar/api/v3/reference/events/instances:
            // timeMin bounds an instance's end, timeMax its start, so this
            // window holds the instances that start at the occurrence's start
            // or run through it. The one whose start matches is the pick.
            var from = InstantPattern.ExtendedIso.Format(occurrence.StartsAt);
            var until = InstantPattern.ExtendedIso.Format(occurrence.StartsAt + Duration.FromSeconds(1));
            using var instances = Request(
                HttpMethod.Get,
                $"{Api}{Escape(calendar!)}/events/{Escape(master.Id)}/instances?timeMin={Escape(from)}&timeMax={Escape(until)}&maxResults=25",
                token!);
            using var answered = await http.SendAsync(instances, cancellationToken);
            if (!answered.IsSuccessStatusCode)
            {
                logger.LogWarning("Google answered {StatusCode} listing the instances of an event for /alerts.", (int)answered.StatusCode);
                return (null, null, null, null, CalendarWriteMessages.Refused(answered.StatusCode));
            }

            var instance = Items(await answered.Content.ReadAsStringAsync(cancellationToken))
                .FirstOrDefault(item => item.Start == occurrence.StartsAt);
            return instance is null
                ? (null, null, null, null, CalendarWriteMessages.NotFound)
                : (calendar, token, master, instance, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google Calendar did not answer a lookup for /alerts ({Failure}).", exception.GetType().Name);
            return (null, null, null, null, CalendarWriteMessages.Unreachable);
        }
    }

    /// <summary>The new end: the start plus the new length, or plus the event's own length when none was given.</summary>
    private static Instant? Ends(Instant start, int? minutes, Instant? oldStart, Instant? oldEnd) =>
        minutes is { } length
            ? start + Duration.FromMinutes(length)
            : oldStart is { } from && oldEnd is { } to ? start + (to - from) : null;

    /// <summary>
    /// A start or end for events.patch: the wall-clock time and its offset,
    /// and the zone when Google named one. A series needs the zone: it is the
    /// clock its recurrence is expanded on.
    /// </summary>
    private static Dictionary<string, string> Time(Instant at, string? timeZone, DateTimeZone zone)
    {
        var time = new Dictionary<string, string>
        {
            ["dateTime"] = OffsetDateTimePattern.Rfc3339.Format(at.InZone(zone).ToOffsetDateTime())
        };
        if (timeZone is not null) time["timeZone"] = timeZone;
        return time;
    }

    private static string Refusal(HttpStatusCode status, string body) =>
        Reason(body) == "forbiddenForNonOrganizer" ? CalendarWriteMessages.OnlyOrganizer : CalendarWriteMessages.Refused(status);

    /// <summary>The calendar to write to and a token, or the reason there is none.</summary>
    private async Task<(string? Calendar, string? Token, string? Problem)> ReadyAsync(CancellationToken cancellationToken)
    {
        CalendarGrant? grant;
        try
        {
            grant = await grants.CurrentAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Reading the calendar connection for /alerts failed ({Failure}).", exception.GetType().Name);
            return (null, null, CalendarWriteMessages.Unchecked);
        }

        if (grant is not { CanEdit: true }) return (null, null, CalendarWriteMessages.NotConnected);

        var feed = AlertFeedIdentity.Of(options.CalendarIcsUrl, status.Snapshot().CalendarName);
        if (!AlertFeedIdentity.Same(feed, grant)) return (null, null, CalendarWriteMessages.OtherCalendar);

        string? token;
        try
        {
            token = await grants.AccessTokenAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google did not answer a token refresh for /alerts ({Failure}).", exception.GetType().Name);
            return (null, null, CalendarWriteMessages.Unreachable);
        }

        return token is null ? (null, null, CalendarWriteMessages.SignInRefused) : (grant.CalendarId, token, null);
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    /// <summary>
    /// The series or single event among what events.list returned for a UID.
    /// A moved occurrence of a series comes back as its own item carrying
    /// recurringEventId. The series itself does not.
    /// </summary>
    internal static (string Id, string Description, bool OrganisedHere)? Series(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("recurringEventId", out _)) continue;
            if (item.TryGetProperty("status", out var state) && state.GetString() == "cancelled") continue;
            if (!item.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } eventId) continue;

            var description = item.TryGetProperty("description", out var text) ? text.GetString() ?? "" : "";
            // An event this calendar organises says so. One it was invited
            // to names another organiser and cannot be edited here.
            var here = !item.TryGetProperty("organizer", out var organizer)
                       || (organizer.TryGetProperty("self", out var self) && self.ValueKind == JsonValueKind.True);
            return (eventId, description, here);
        }

        return null;
    }

    /// <summary>The master event among what events.list returned for a UID, as <see cref="Series"/> picks it.</summary>
    internal static GoogleEvent? Master(string json) => Items(json).FirstOrDefault(item => item.Master);

    /// <summary>Every event item in an events.list or events.instances answer, cancelled ones left out.</summary>
    internal static IReadOnlyList<GoogleEvent> Items(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return [];

        var found = new List<GoogleEvent>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("status", out var state) && state.GetString() == "cancelled") continue;
            if (!item.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } eventId) continue;

            var here = !item.TryGetProperty("organizer", out var organizer)
                       || (organizer.TryGetProperty("self", out var self) && self.ValueKind == JsonValueKind.True);
            var recurring = item.TryGetProperty("recurrence", out var rules)
                            && rules.ValueKind == JsonValueKind.Array
                            && rules.GetArrayLength() > 0;
            var (start, zone) = When(item, "start");
            var (end, _) = When(item, "end");
            found.Add(new GoogleEvent(
                eventId, here, recurring, start, end, zone, !item.TryGetProperty("recurringEventId", out _)));
        }

        return found;
    }

    /// <summary>A start or end: dateTime as an instant, and the zone Google named.</summary>
    private static (Instant? At, string? Zone) When(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var time) || time.ValueKind != JsonValueKind.Object) return (null, null);
        var zone = time.TryGetProperty("timeZone", out var named) ? named.GetString() : null;
        return time.TryGetProperty("dateTime", out var value)
               && DateTimeOffset.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out var parsed)
            ? (Instant.FromDateTimeOffset(parsed), zone)
            : (null, zone);
    }

    /// <summary>Google's reason word for a refusal, from its error body. Never shown: only compared.</summary>
    internal static string? Reason(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("error", out var error)
                   && error.TryGetProperty("errors", out var errors)
                   && errors.ValueKind == JsonValueKind.Array
                   && errors.GetArrayLength() > 0
                   && errors[0].TryGetProperty("reason", out var reason)
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
