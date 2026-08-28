using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Creates and cancels booking invitations on the host's Google calendar.
/// </summary>
/// <remarks>
/// sendUpdates=all on both calls is what makes this an invitation rather than
/// bookkeeping: it tells Google to email the attendee about the event and
/// about its cancellation. Without it the event exists and nobody hears.
/// </remarks>
public sealed class GoogleCalendarInvites(
    HttpClient http,
    SchedulingDbContext database,
    GoogleAccessTokens tokens,
    SchedulingOptions options,
    ILogger<GoogleCalendarInvites> logger) : ICalendarInvites
{
    public async Task<string?> CreateEventAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appointment.Email))
        {
            return null;
        }

        var credential = await database.HostCalendarCredentials.FirstOrDefaultAsync(cancellationToken);

        if (credential is null
            || !credential.GrantedScopes.Contains(CalendarAdminEndpoints.EventsScope, StringComparison.Ordinal))
        {
            // Checked from the stored grant rather than by trying and reading a
            // 403, because the 403 would repeat on every booking while the
            // remedy — reconnect the calendar — belongs to the host, once.
            logger.LogInformation(
                "No calendar invite for appointment {AppointmentId}: {Reason}.",
                appointment.Id,
                credential is null ? "no calendar connected" : "the grant lacks the events scope");
            return null;
        }

        var accessToken = await tokens.GetAccessTokenAsync(credential.ProtectedRefreshToken, cancellationToken);

        if (accessToken is null)
        {
            return null;
        }

        var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(credential.CalendarId)}/events?sendUpdates=all";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                EventBody(options.HostName, appointment),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Google event creation returned {StatusCode} for appointment {AppointmentId}.",
                    (int)response.StatusCode,
                    appointment.Id);
                return null;
            }

            return ParseEventId(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception,
                "Google was unreachable creating the invite for appointment {AppointmentId}.", appointment.Id);
            return null;
        }
    }

    public async Task DeleteEventAsync(string googleEventId, CancellationToken cancellationToken)
    {
        var credential = await database.HostCalendarCredentials.FirstOrDefaultAsync(cancellationToken);

        if (credential is null)
        {
            return;
        }

        var accessToken = await tokens.GetAccessTokenAsync(credential.ProtectedRefreshToken, cancellationToken);

        if (accessToken is null)
        {
            return;
        }

        var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(credential.CalendarId)}"
                  + $"/events/{Uri.EscapeDataString(googleEventId)}?sendUpdates=all";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);

            // Gone already is the outcome asked for, however it came about: the
            // host deleted it by hand, or the cancel link was pressed twice.
            if (!response.IsSuccessStatusCode
                && response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))
            {
                logger.LogWarning(
                    "Google event deletion returned {StatusCode} for event {EventId}.",
                    (int)response.StatusCode,
                    googleEventId);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Google was unreachable cancelling event {EventId}.", googleEventId);
        }
    }

    /// <summary>
    /// The event as Google's API wants it.
    /// </summary>
    /// <remarks>
    /// Internal and pure so the shape can be asserted without a network. The
    /// instants go over as UTC and Google renders them in each viewer's own
    /// calendar zone, which is the same promise the texts make: nobody is ever
    /// shown a time in somebody else's zone.
    /// </remarks>
    internal static string EventBody(string hostName, Appointment appointment) =>
        JsonSerializer.Serialize(new
        {
            summary = $"{hostName} and {appointment.DisplayName}",
            description = "Booked at https://abera.tech/schedule.",
            start = new { dateTime = InstantPattern.ExtendedIso.Format(appointment.StartsAt) },
            end = new { dateTime = InstantPattern.ExtendedIso.Format(appointment.EndsAt) },
            attendees = new[] { new { email = appointment.Email } }
        });

    /// <summary>Pulls the event id out of Google's creation response.</summary>
    internal static string? ParseEventId(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }
}
