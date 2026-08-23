using System.Net.Http.Headers;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Open time read from the events on a calendar the host keeps in Google.
/// </summary>
/// <remarks>
/// Worth being precise about why it works this way, because the obvious idea
/// does not work: Google does not expose the availability behind an appointment
/// schedule. The Calendar API has eight resources — Acl, CalendarList,
/// Calendars, Channels, Colors, Events, Freebusy and Settings — and none of them
/// describes a booking page's hours or a user's working hours. There is nothing
/// to read.
///
/// What can be read is events. So the host keeps a calendar whose events *are*
/// the open windows — a recurring "Open for bookings" block, moved and edited in
/// Google like anything else — and this reads that calendar. Hours stay in one
/// place, which was the point.
///
/// Recurrence is expanded by Google rather than here (singleEvents), so a
/// weekly block that skips a week because the host deleted one occurrence skips
/// it here too, and daylight saving is applied by the same engine that shows the
/// host their own calendar.
/// </remarks>
public sealed class GoogleEventsAvailabilitySource(
    HttpClient http,
    SchedulingDbContext database,
    GoogleAccessTokens tokens,
    GoogleCalendarOptions options,
    ILogger<GoogleEventsAvailabilitySource> logger) : IAvailabilitySource
{
    public async Task<IReadOnlyList<Interval>> GetOpenWindowsAsync(
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        var credential = await database.HostCalendarCredentials.FirstOrDefaultAsync(cancellationToken);

        if (credential is null || string.IsNullOrWhiteSpace(options.AvailabilityCalendarId))
        {
            return [];
        }

        var accessToken = await tokens.GetAccessTokenAsync(credential.ProtectedRefreshToken, cancellationToken);

        if (accessToken is null)
        {
            return [];
        }

        var timeMin = from.AtMidnight().InUtc().ToInstant();
        var timeMax = to.PlusDays(1).AtMidnight().InUtc().ToInstant();

        var url = "https://www.googleapis.com/calendar/v3/calendars/"
                  + Uri.EscapeDataString(options.AvailabilityCalendarId)
                  + "/events"
                  + $"?timeMin={Uri.EscapeDataString(InstantPattern.ExtendedIso.Format(timeMin))}"
                  + $"&timeMax={Uri.EscapeDataString(InstantPattern.ExtendedIso.Format(timeMax))}"
                  // Expands recurring blocks into their occurrences, and applies
                  // any the host has moved or cancelled individually.
                  + "&singleEvents=true&orderBy=startTime&maxResults=2500";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Returning nothing rather than throwing: no open windows means the
            // page says there is nothing free, which is wrong but harmless.
            // Throwing would take the booking page down over a calendar rename.
            logger.LogWarning(
                "Reading the availability calendar returned {StatusCode}. No windows will be offered.",
                (int)response.StatusCode);
            return [];
        }

        var windows = ParseWindows(await response.Content.ReadAsStringAsync(cancellationToken));

        if (windows.Count == 0)
        {
            logger.LogInformation(
                "The availability calendar has no events between {From} and {To}.",
                from,
                to);
        }

        return windows;
    }

    /// <summary>Turns an events listing into the periods it declares open.</summary>
    /// <remarks>
    /// All-day entries are skipped deliberately. An all-day "Open" would claim
    /// midnight to midnight, so one carelessly created entry would offer the
    /// host's entire night; a booking window is a time of day and has to be
    /// stated as one.
    ///
    /// Cancelled occurrences are skipped too: Google returns them in the listing
    /// with status "cancelled" so that callers syncing state can notice the
    /// deletion, and treating one as open would resurrect a block the host
    /// removed.
    /// </remarks>
    internal static IReadOnlyList<Interval> ParseWindows(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("items", out var items))
        {
            return [];
        }

        var windows = new List<Interval>();

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("status", out var status)
                && string.Equals(status.GetString(), "cancelled", StringComparison.Ordinal))
            {
                continue;
            }

            if (!item.TryGetProperty("start", out var start) || !item.TryGetProperty("end", out var end))
            {
                continue;
            }

            // "dateTime" is a timed event; "date" alone is all-day.
            if (!start.TryGetProperty("dateTime", out var startsAt)
                || !end.TryGetProperty("dateTime", out var endsAt))
            {
                continue;
            }

            if (TryParse(startsAt.GetString()) is { } opensAt
                && TryParse(endsAt.GetString()) is { } closesAt
                && closesAt > opensAt)
            {
                windows.Add(new Interval(opensAt, closesAt));
            }
        }

        return BusyMerge.Coalesce(windows);
    }

    /// <summary>
    /// Parses an RFC 3339 timestamp with or without a numeric offset.
    /// </summary>
    /// <remarks>
    /// Google returns event times with the calendar's offset, not "Z", so the
    /// offset form is the normal case here rather than the exception.
    /// </remarks>
    private static Instant? TryParse(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var utc = InstantPattern.ExtendedIso.Parse(value);
        if (utc.Success)
        {
            return utc.Value;
        }

        var offset = OffsetDateTimePattern.Rfc3339.Parse(value);
        return offset.Success ? offset.Value.ToInstant() : null;
    }
}
