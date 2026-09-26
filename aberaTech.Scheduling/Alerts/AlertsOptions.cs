using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The owner's calendar alerts. The "Alerts" section.
/// </summary>
/// <remarks>
/// Three values are secrets and are set per deployment as container app
/// secret references: the calendar's secret iCal address and the two
/// Pushover keys. Any of them missing and the feature is off: no worker
/// runs, and the owner page lists the missing names. Never the values.
/// </remarks>
public sealed class AlertsOptions
{
    public const string Section = "Alerts";

    /// <summary>Google Calendar, Settings, the calendar, "Secret address in iCal format".</summary>
    public string? CalendarIcsUrl { get; init; }

    /// <summary>The API token of the Pushover application that sends the alerts.</summary>
    public string? PushoverAppToken { get; init; }

    /// <summary>The Pushover user key of the phone that receives them.</summary>
    public string? PushoverUserKey { get; init; }

    /// <summary>How often the calendar is read. Sends do not wait for it: each is timed from the cached list.</summary>
    public int PollMinutes { get; init; } = 5;

    /// <summary>How long before the start an event with no reminder of its own alerts.</summary>
    public int DefaultLeadMinutes { get; init; } = 10;

    /// <summary>How far ahead the calendar is expanded.</summary>
    public int LookaheadHours { get; init; } = 48;

    /// <summary>All-day events are skipped unless this is set.</summary>
    public bool IncludeAllDay { get; init; }

    /// <summary>
    /// The zone for the alert text and for "06:00 tomorrow" when the feed
    /// does not name one. Google's feed always does (X-WR-TIMEZONE, the
    /// calendar's default zone), so this is a fallback. Unset, the fallback
    /// is UTC. Each event's own times come from its own TZID either way.
    /// </summary>
    public string? TimeZone { get; init; }

    /// <summary>
    /// The owner's addresses, for recognising an invitation he declined. The
    /// feed's own name (X-WR-CALNAME, the address for a primary calendar) is
    /// used as well, so this is only needed for a secondary calendar.
    /// </summary>
    public string[] OwnerEmails { get; init; } = [];

    /// <summary>
    /// How long to wait before the one retry of a Pushover send that failed
    /// before Pushover took it: a 5xx or no connection. A timeout is not
    /// retried, because the first attempt may have been delivered.
    /// </summary>
    public int PushoverRetrySeconds { get; init; } = 5;

    /// <summary>
    /// Development only: a calendar and a Pushover in memory, so the compose
    /// app and the browser suite can drive the page without either account.
    /// Ignored outside Development.
    /// </summary>
    public bool Fake { get; init; }

    /// <summary>The environment variable names of the settings that are missing, never their values.</summary>
    public IReadOnlyList<string> Missing(bool hasDatabase)
    {
        var missing = new List<string>();
        if (!hasDatabase) missing.Add("ConnectionStrings__Scheduling");
        if (!IsHttps(CalendarIcsUrl)) missing.Add("Alerts__CalendarIcsUrl");
        if (string.IsNullOrWhiteSpace(PushoverAppToken)) missing.Add("Alerts__PushoverAppToken");
        if (string.IsNullOrWhiteSpace(PushoverUserKey)) missing.Add("Alerts__PushoverUserKey");
        return missing;
    }

    public Duration Poll => Duration.FromMinutes(Math.Clamp(PollMinutes, 1, 60));

    public Duration DefaultLead => Duration.FromMinutes(Math.Clamp(DefaultLeadMinutes, 0, 24 * 60));

    public Duration Lookahead => Duration.FromHours(Math.Clamp(LookaheadHours, 1, 24 * 14));

    /// <summary>The fallback zone. A name the time zone database does not know stops the start.</summary>
    public DateTimeZone FallbackZone() =>
        string.IsNullOrWhiteSpace(TimeZone)
            ? DateTimeZone.Utc
            : DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone)
              ?? throw new InvalidOperationException($"Alerts:TimeZone '{TimeZone}' is not a time zone database name.");

    /// <summary>
    /// Whether a request goes to the calendar's secret address. The secret
    /// is the path, so anything that records a URL (a trace, a log line)
    /// must be told to leave this one out.
    /// </summary>
    public bool IsCalendarRequest(Uri? uri)
    {
        if (uri is null || !Uri.TryCreate(CalendarIcsUrl, UriKind.Absolute, out var feed)) return false;
        return string.Equals(
            uri.GetLeftPart(UriPartial.Path), feed.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
