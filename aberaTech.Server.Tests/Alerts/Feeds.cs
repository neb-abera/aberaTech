namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// Calendar feeds shaped the way Google's secret iCal address writes them:
/// the X-WR headers, a VTIMEZONE block, TZID on local times, a VALARM per
/// popup reminder and an EMAIL one per mail reminder.
/// </summary>
internal static class Feeds
{
    public const string Owner = "owner@example.test";

    public static string Ics(params string[] events) => IcsIn("America/New_York", events);

    /// <summary>A feed whose calendar default zone (X-WR-TIMEZONE) is <paramref name="calendarZone"/>.</summary>
    public static string IcsIn(string calendarZone, params string[] events) =>
        string.Join("\r\n",
        [
            "BEGIN:VCALENDAR",
            "PRODID:-//Google Inc//Google Calendar 70.9054//EN",
            "VERSION:2.0",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            $"X-WR-CALNAME:{Owner}",
            $"X-WR-TIMEZONE:{calendarZone}",
            "BEGIN:VTIMEZONE",
            "TZID:America/New_York",
            "X-LIC-LOCATION:America/New_York",
            "BEGIN:DAYLIGHT",
            "TZOFFSETFROM:-0500",
            "TZOFFSETTO:-0400",
            "TZNAME:EDT",
            "DTSTART:19700308T020000",
            "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU",
            "END:DAYLIGHT",
            "BEGIN:STANDARD",
            "TZOFFSETFROM:-0400",
            "TZOFFSETTO:-0500",
            "TZNAME:EST",
            "DTSTART:19701101T020000",
            "RRULE:FREQ=YEARLY;BYMONTH=11;BYDAY=1SU",
            "END:STANDARD",
            "END:VTIMEZONE",
            .. events,
            "END:VCALENDAR",
            ""
        ]);

    /// <summary>One VEVENT. <paramref name="start"/> is local New York time, yyyyMMddTHHmmss.</summary>
    public static string Event(
        string uid,
        string summary,
        string start,
        string? end = null,
        string? location = null,
        string[]? extra = null,
        string[]? alarms = null) =>
        string.Join("\r\n",
        [
            "BEGIN:VEVENT",
            $"DTSTART;TZID=America/New_York:{start}",
            $"DTEND;TZID=America/New_York:{end ?? start}",
            $"UID:{uid}",
            $"SUMMARY:{summary}",
            .. location is null ? Array.Empty<string>() : [$"LOCATION:{location}"],
            .. extra?.Any(line => line.StartsWith("STATUS:", StringComparison.Ordinal)) == true
                ? Array.Empty<string>()
                : ["STATUS:CONFIRMED"],
            .. extra ?? [],
            .. alarms ?? [],
            "END:VEVENT"
        ]);

    /// <summary>A popup reminder, as Google writes one.</summary>
    public static string Popup(string trigger) =>
        $"BEGIN:VALARM\r\nACTION:DISPLAY\r\nDESCRIPTION:This is an event reminder\r\nTRIGGER:{trigger}\r\nEND:VALARM";

    /// <summary>A mail reminder, as Google writes one. Not something a phone alert should follow.</summary>
    public static string Mail(string trigger) =>
        "BEGIN:VALARM\r\nACTION:EMAIL\r\nDESCRIPTION:This is an event reminder\r\nSUMMARY:Alarm notification\r\n"
        + $"ATTENDEE:mailto:{Owner}\r\nTRIGGER:{trigger}\r\nEND:VALARM";

    /// <summary>A plain event on one morning: 09:00 to 09:30 New York time on Wednesday 28 October 2026.</summary>
    public static string Standup(params string[] alarms) =>
        Ics(Event("standup@google.com", "Standup", "20261028T090000", "20261028T093000", "Room 1", alarms: alarms));
}
