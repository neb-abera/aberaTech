using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Outbox;
using Microsoft.EntityFrameworkCore;
using NodaTime.Text;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Creates and cancels booking invitations on the host's Google calendar.
/// </summary>
/// <remarks>
/// sendUpdates=all on both calls is what makes this an invitation rather than
/// bookkeeping: it tells Google to email the attendee about the event and
/// about its cancellation. Without it the event exists and nobody hears.
///
/// The event's id is chosen here, from the appointment's own id. A retry
/// after a lost reply then asks Google to create an event that already
/// exists, Google answers 409, and that answer counts as done. So a retried
/// invite never becomes two invites.
/// </remarks>
public sealed class GoogleCalendarInvites(
    HttpClient http,
    SchedulingDbContext database,
    GoogleAccessTokens tokens,
    SchedulingOptions options,
    ILogger<GoogleCalendarInvites> logger) : ICalendarInvites
{
    public async Task<SendResult> InviteAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appointment.Email))
        {
            return SendResult.RejectedPermanently("The booking has no email address.");
        }

        var (credential, accessToken, refusal) = await AuthorizeAsync(cancellationToken);
        if (refusal is not null)
        {
            return refusal.Value;
        }

        var eventId = EventId(appointment);
        var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(credential!.CalendarId)}/events?sendUpdates=all";

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
            var result = InterpretCreate(response.StatusCode, eventId);

            if (!result.Accepted)
            {
                logger.LogWarning(
                    "Google event creation returned {StatusCode} for appointment {AppointmentId}.",
                    (int)response.StatusCode,
                    appointment.Id);
            }

            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return SendResult.Rejected($"Google was unreachable: {exception.GetType().Name}.");
        }
    }

    public async Task<SendResult> WithdrawAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var (credential, accessToken, refusal) = await AuthorizeAsync(cancellationToken);
        if (refusal is not null)
        {
            return refusal.Value;
        }

        var eventId = EventId(appointment);
        var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(credential!.CalendarId)}"
                  + $"/events/{Uri.EscapeDataString(eventId)}?sendUpdates=all";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return InterpretDelete(response.StatusCode, eventId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return SendResult.Rejected($"Google was unreachable: {exception.GetType().Name}.");
        }
    }

    /// <summary>
    /// The stored grant and a fresh access token, or the reason there are none.
    /// </summary>
    /// <remarks>
    /// Each reason is retryable. The host can connect or reconnect the calendar
    /// while the retries run, and the invite then goes out on its own.
    /// </remarks>
    private async Task<(HostCalendarCredential? Credential, string? AccessToken, SendResult? Refusal)> AuthorizeAsync(
        CancellationToken cancellationToken)
    {
        var credential = await database.HostCalendarCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (credential is null)
        {
            return (null, null, SendResult.Rejected("No calendar is connected. Connect it on /schedule/admin."));
        }

        if (!credential.GrantedScopes.Contains(CalendarAdminEndpoints.EventsScope, StringComparison.Ordinal))
        {
            return (null, null, SendResult.Rejected(
                "The calendar grant cannot create events. Reconnect the calendar on /schedule/admin."));
        }

        var accessToken = await tokens.GetAccessTokenAsync(credential.ProtectedRefreshToken, cancellationToken);

        return accessToken is null
            ? (null, null, SendResult.Rejected("Google refused the stored calendar grant. Reconnect the calendar on /schedule/admin."))
            : (credential, accessToken, null);
    }

    /// <summary>
    /// The Google event id for a booking.
    /// </summary>
    /// <remarks>
    /// Google accepts an id of 5 to 1024 characters from a-v and 0-9. A Guid
    /// written as 32 lowercase hex digits uses only 0-9 and a-f, so it fits.
    /// A booking made before ids were chosen keeps the id Google gave it.
    /// </remarks>
    internal static string EventId(Appointment appointment) =>
        appointment.GoogleEventId ?? appointment.Id.ToString("N");

    /// <summary>What Google's answer to a create means for the outbox row.</summary>
    internal static SendResult InterpretCreate(HttpStatusCode status, string eventId) => status switch
    {
        _ when (int)status is >= 200 and < 300 => SendResult.Confirmed(eventId),

        // An event with this id exists: an earlier attempt created it and its
        // reply was lost. The invite went with that attempt.
        HttpStatusCode.Conflict => SendResult.Confirmed(eventId),

        // The body is wrong, and the same body will be wrong next time.
        HttpStatusCode.BadRequest => SendResult.RejectedPermanently("Google refused the event as malformed (400)."),

        _ => SendResult.Rejected($"Google answered {(int)status}.")
    };

    /// <summary>What Google's answer to a delete means for the outbox row.</summary>
    internal static SendResult InterpretDelete(HttpStatusCode status, string eventId) => status switch
    {
        // Gone already is the outcome asked for, however it came about: the
        // host deleted it by hand, or the invite was never created.
        _ when (int)status is >= 200 and < 300 => SendResult.Confirmed(WithdrawalId(eventId)),
        HttpStatusCode.NotFound or HttpStatusCode.Gone => SendResult.Confirmed(WithdrawalId(eventId)),
        _ => SendResult.Rejected($"Google answered {(int)status}.")
    };

    /// <summary>
    /// The provider id a withdrawal is recorded under. Distinct from the
    /// invite's, because the outbox holds each provider id once.
    /// </summary>
    internal static string WithdrawalId(string eventId) => $"withdraw-{eventId}";

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
            id = EventId(appointment),
            summary = $"{hostName} and {appointment.DisplayName}",
            description = "Booked at https://abera.tech/schedule.",
            start = new { dateTime = InstantPattern.ExtendedIso.Format(appointment.StartsAt) },
            end = new { dateTime = InstantPattern.ExtendedIso.Format(appointment.EndsAt) },
            attendees = new[] { new { email = appointment.Email } }
        });
}
