using System.Security.Cryptography;
using System.Text;
using Ical.Net;
using IcalCalendar = Ical.Net.Calendar;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using NodaTime;
using Duration = NodaTime.Duration;
using IcalDuration = Ical.Net.DataTypes.Duration;

namespace aberaTech.Scheduling.Alerts;

/// <summary>Where an alert time came from.</summary>
public enum AlertSource
{
    /// <summary>A popup reminder on the event itself (VALARM).</summary>
    Reminder,

    /// <summary>No reminder on the event: <see cref="AlertsOptions.DefaultLeadMinutes"/> before the start.</summary>
    DefaultLead
}

/// <summary>One occurrence of one event, and when its one alert goes off.</summary>
/// <param name="Key">The event's UID and this occurrence's start. The dedupe and skip key.</param>
public sealed record PlannedAlert(
    string Key,
    string Title,
    string? Location,
    Instant StartsAt,
    Instant AlertAt,
    AlertSource Source);

/// <summary>
/// One read of the calendar: the alerts in the window, and the calendar's
/// own zone, which the alert text and "06:00 tomorrow" are written in.
/// </summary>
public sealed record CalendarPlan(IReadOnlyList<PlannedAlert> Alerts, DateTimeZone Zone);

/// <summary>The feed could not be read as a calendar. The message is safe to show and to log.</summary>
public sealed class CalendarFeedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Reads an iCalendar feed and works out every alert in the look-ahead
/// window. Pure: the feed and the time in, the list out.
/// </summary>
/// <remarks>
/// Ical.Net expands the recurrence rules, applies EXDATE and the
/// RECURRENCE-ID overrides Google writes for a moved or cancelled instance,
/// and resolves TZID through the time zone database, so a daily 09:00 stays
/// at 09:00 local on both sides of a clock change.
///
/// One alert per occurrence. Of the event's popup and sound reminders the
/// earliest is the alert. Mail reminders are ignored: they are for the
/// inbox, not the phone. With no usable reminder the alert is the default
/// lead before the start.
/// </remarks>
public static class AlertPlanner
{
    /// <summary>The longest key stored. A longer one is replaced by its hash.</summary>
    public const int MaxKeyLength = 200;

    /// <summary>How many occurrences one read may expand. A personal calendar has tens in two days.</summary>
    public const int MaxOccurrences = 5000;

    public static CalendarPlan Plan(string ics, Instant now, AlertsOptions options)
    {
        var calendar = Load(ics);
        var zone = FeedZone(calendar) ?? options.FallbackZone();
        var owners = OwnerAddresses(calendar, options);
        var horizon = now + options.Lookahead;

        var alerts = new List<PlannedAlert>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            // A day early, and a day past the horizon, so an all-day date read
            // as UTC midnight by the library is never cut at the window's edge.
            // The window itself is applied below, on real instants.
            var from = new CalDateTime((now - Duration.FromDays(1)).ToDateTimeUtc(), "UTC");
            var until = (horizon + Duration.FromDays(1)).ToDateTimeUtc();
            var evaluation = new EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 };

            var seen = 0;
            foreach (var occurrence in calendar.GetOccurrences<CalendarEvent>(from, evaluation))
            {
                if (occurrence.Period.StartTime.AsUtc > until || ++seen > MaxOccurrences) break;
                if (occurrence.Source is not CalendarEvent calendarEvent) continue;

                var alert = PlanOne(calendarEvent, occurrence, now, horizon, zone, owners, options);
                if (alert is not null && keys.Add(alert.Key)) alerts.Add(alert);
            }
        }
        catch (Exception exception) when (exception is not CalendarFeedException)
        {
            throw new CalendarFeedException("The feed has an event this server cannot expand.", exception);
        }

        return new CalendarPlan(
            [.. alerts.OrderBy(alert => alert.AlertAt).ThenBy(alert => alert.StartsAt).ThenBy(alert => alert.Key, StringComparer.Ordinal)],
            zone);
    }

    /// <summary>
    /// The calendar's default zone, as Google names it in X-WR-TIMEZONE.
    /// Null when the feed names none or a name the database does not know.
    /// </summary>
    private static DateTimeZone? FeedZone(IcalCalendar calendar)
    {
        var name = Property(calendar, "X-WR-TIMEZONE");
        return string.IsNullOrWhiteSpace(name) ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(name.Trim());
    }

    private static string? Property(IcalCalendar calendar, string name) =>
        calendar.Properties.FirstOrDefault(property => property.Name == name)?.Value?.ToString();

    private static IcalCalendar Load(string ics)
    {
        IcalCalendar? calendar;
        try
        {
            calendar = IcalCalendar.Load(ics);
        }
        catch (Exception exception)
        {
            throw new CalendarFeedException("The feed is not a calendar.", exception);
        }

        return calendar ?? throw new CalendarFeedException("The feed is not a calendar.");
    }

    private static PlannedAlert? PlanOne(
        CalendarEvent calendarEvent,
        Occurrence occurrence,
        Instant now,
        Instant horizon,
        DateTimeZone zone,
        IReadOnlySet<string> owners,
        AlertsOptions options)
    {
        if (string.Equals(calendarEvent.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) return null;
        if (calendarEvent.IsAllDay && !options.IncludeAllDay) return null;
        if (Declined(calendarEvent, owners)) return null;

        var startTime = occurrence.Period.StartTime;
        var start = ToInstant(startTime, zone);
        if (start <= now || start > horizon) return null;

        var end = occurrence.Period.EffectiveEndTime is { } endTime ? ToInstant(endTime, zone) : start;
        // A reminder "one day before" is a day on the event's own clock.
        var eventZone = ZoneOf(startTime) ?? zone;
        var reminder = EarliestReminder(calendarEvent, start, end, eventZone);

        return new PlannedAlert(
            Key(calendarEvent.Uid, start),
            string.IsNullOrWhiteSpace(calendarEvent.Summary) ? "(no title)" : calendarEvent.Summary.Trim(),
            string.IsNullOrWhiteSpace(calendarEvent.Location) ? null : calendarEvent.Location.Trim(),
            start,
            reminder ?? start - options.DefaultLead,
            reminder is null ? AlertSource.DefaultLead : AlertSource.Reminder);
    }

    private static Instant? EarliestReminder(CalendarEvent calendarEvent, Instant start, Instant end, DateTimeZone zone)
    {
        Instant? earliest = null;
        foreach (var alarm in calendarEvent.Alarms)
        {
            if (!string.Equals(alarm.Action, "DISPLAY", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(alarm.Action, "AUDIO", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Instant? at = alarm.Trigger switch
            {
                { IsRelative: true, Duration: { } offset } trigger => Shift(
                    string.Equals(trigger.Related, "END", StringComparison.OrdinalIgnoreCase) ? end : start, offset, zone),
                { DateTime: { } fixedTime } => ToInstant(fixedTime, zone),
                _ => null
            };

            if (at is { } value && (earliest is null || value < earliest)) earliest = value;
        }

        return earliest;
    }

    /// <summary>
    /// A trigger offset from a start or end. Weeks and days are calendar days
    /// in the zone (RFC 5545 calls them nominal): "one day before" 09:00 is
    /// 09:00 the day before, 23 or 25 hours earlier across a clock change.
    /// Hours, minutes and seconds are exact.
    /// </summary>
    private static Instant Shift(Instant from, IcalDuration offset, DateTimeZone zone)
    {
        var days = (offset.Weeks ?? 0) * 7 + (offset.Days ?? 0);
        var shifted = days == 0
            ? from
            : zone.AtLeniently(from.InZone(zone).LocalDateTime.PlusDays(days)).ToInstant();

        return shifted
               + Duration.FromHours(offset.Hours ?? 0)
               + Duration.FromMinutes(offset.Minutes ?? 0)
               + Duration.FromSeconds(offset.Seconds ?? 0);
    }

    private static DateTimeZone? ZoneOf(CalDateTime value) =>
        value.IsFloating || string.IsNullOrWhiteSpace(value.TzId) ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(value.TzId);

    /// <summary>A date, or a time with no zone, is local time in the calendar's zone.</summary>
    private static Instant ToInstant(CalDateTime value, DateTimeZone zone)
    {
        if (!value.HasTime || value.IsFloating)
        {
            var local = new LocalDateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second);
            return zone.AtLeniently(local).ToInstant();
        }

        return Instant.FromDateTimeUtc(DateTime.SpecifyKind(value.AsUtc, DateTimeKind.Utc));
    }

    private static bool Declined(CalendarEvent calendarEvent, IReadOnlySet<string> owners) =>
        calendarEvent.Attendees.Any(attendee =>
            string.Equals(attendee.ParticipationStatus, "DECLINED", StringComparison.OrdinalIgnoreCase)
            && Address(attendee.Value) is { } address
            && owners.Contains(address));

    private static IReadOnlySet<string> OwnerAddresses(IcalCalendar calendar, AlertsOptions options)
    {
        var owners = new HashSet<string>(options.OwnerEmails.Select(email => email.Trim()), StringComparer.OrdinalIgnoreCase);
        var name = Property(calendar, "X-WR-CALNAME");
        if (name is not null && name.Contains('@')) owners.Add(name.Trim());
        return owners;
    }

    private static string? Address(Uri? value)
    {
        if (value is null) return null;
        var text = value.OriginalString;
        return text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? text["mailto:".Length..] : text;
    }

    /// <summary>
    /// The event's UID and the occurrence's start in UTC. A moved occurrence
    /// gets a new key, so it alerts at its new time. A UID long enough to
    /// push the key past the column is hashed.
    /// </summary>
    private static string Key(string? uid, Instant start)
    {
        var key = $"{uid}|{start.ToDateTimeUtc():yyyyMMdd'T'HHmmss'Z'}";
        if (key.Length <= MaxKeyLength) return key;

        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
