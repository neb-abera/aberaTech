using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Calendar;

/// <summary>Reads the host's Google calendar so booked-elsewhere time is not offered here.</summary>
public sealed class GoogleCalendarBusySource(
    HttpClient http,
    SchedulingDbContext database,
    GoogleAccessTokens tokens,
    GoogleCalendarOptions options,
    IClock clock,
    ILogger<GoogleCalendarBusySource> logger) : IBusySource
{
    // Static so the cache is shared by every request, which is the point: a link
    // sent to twenty-eight people at once should cost one call to Google, not
    // twenty-eight.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IReadOnlyList<Interval> cached = [];
    private static Instant cachedUntil = Instant.MinValue;
    private static Interval cachedRange;

    public async Task<IReadOnlyList<Interval>> GetBusyAsync(Interval range, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();

        if (now < cachedUntil && Covers(cachedRange, range))
        {
            return cached;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the gate: several requests can arrive together on
            // a cold cache, and only the first should call Google.
            if (now < cachedUntil && Covers(cachedRange, range))
            {
                return cached;
            }

            var credential = await database.Set<HostCalendarCredential>()
                .FirstOrDefaultAsync(cancellationToken);

            if (credential is null)
            {
                return [];
            }

            var accessToken = await tokens.GetAccessTokenAsync(credential.ProtectedRefreshToken, cancellationToken);

            if (accessToken is null)
            {
                return [];
            }

            var busy = await QueryFreeBusyAsync(accessToken, credential.CalendarId, range, cancellationToken);

            cached = busy;
            cachedRange = range;
            cachedUntil = now + Duration.FromSeconds(options.CacheSeconds);

            return busy;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<IReadOnlyList<Interval>> QueryFreeBusyAsync(
        string accessToken,
        string calendarId,
        Interval range,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            timeMin = InstantPattern.ExtendedIso.Format(range.Start),
            timeMax = InstantPattern.ExtendedIso.Format(range.End),
            items = new[] { new { id = calendarId } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/freeBusy")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google free/busy returned {StatusCode}.", (int)response.StatusCode);
            return [];
        }

        return Parse(await response.Content.ReadAsStringAsync(cancellationToken), calendarId);
    }

    /// <summary>
    /// Pulls the busy blocks out of a free/busy response.
    /// </summary>
    /// <remarks>
    /// Internal so it can be tested against a captured response without a
    /// network: the parsing is where a change in Google's shape would break
    /// this, and that is worth an assertion rather than a hope.
    /// </remarks>
    internal static IReadOnlyList<Interval> Parse(string json, string calendarId)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("calendars", out var calendars)
            || !calendars.TryGetProperty(calendarId, out var calendar)
            || !calendar.TryGetProperty("busy", out var busy))
        {
            return [];
        }

        var intervals = new List<Interval>();

        foreach (var period in busy.EnumerateArray())
        {
            var start = period.TryGetProperty("start", out var from) ? from.GetString() : null;
            var end = period.TryGetProperty("end", out var to) ? to.GetString() : null;

            if (start is null || end is null)
            {
                continue;
            }

            if (TryParseRfc3339(start) is { } busyStart
                && TryParseRfc3339(end) is { } busyEnd
                && busyEnd > busyStart)
            {
                intervals.Add(new Interval(busyStart, busyEnd));
            }
        }

        return BusyMerge.Coalesce(intervals);
    }

    /// <summary>
    /// Parses an RFC 3339 timestamp, with or without a numeric offset.
    /// </summary>
    /// <remarks>
    /// Both forms are necessary. NodaTime's ExtendedIso pattern requires a "Z"
    /// and rejects "2027-06-01T09:00:00-04:00" outright — and a rejected period
    /// is a period silently dropped, which would offer a slot in the middle of
    /// something the host is already doing. The offset form is the dangerous one
    /// precisely because it fails quietly rather than loudly.
    /// </remarks>
    private static Instant? TryParseRfc3339(string value)
    {
        var utc = InstantPattern.ExtendedIso.Parse(value);
        if (utc.Success)
        {
            return utc.Value;
        }

        var offset = OffsetDateTimePattern.Rfc3339.Parse(value);
        return offset.Success ? offset.Value.ToInstant() : null;
    }

    private static bool Covers(Interval cachedFor, Interval wanted) =>
        cachedFor.Start <= wanted.Start && cachedFor.End >= wanted.End;
}
