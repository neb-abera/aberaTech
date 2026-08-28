using aberaTech.Scheduling.Data;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Sends and withdraws the calendar invitation for a booking.
/// </summary>
/// <remarks>
/// The invite is a Google Calendar event on the host's calendar with the
/// visitor as attendee: Google then emails the invitation, so the site needs no
/// mail server, no sender reputation and no template — and the visitor gets a
/// real invite they can accept, not an attachment to import.
///
/// Best effort by contract. A booking whose invite fails is still a booking;
/// both methods log and return rather than throw, because the calendar is a
/// courtesy and the appointment is the point.
/// </remarks>
public interface ICalendarInvites
{
    /// <summary>
    /// Creates the event and returns its Google id, or null when no invite
    /// could be sent — no calendar connected, the grant lacks the events
    /// scope, or Google refused.
    /// </summary>
    Task<string?> CreateEventAsync(Appointment appointment, CancellationToken cancellationToken);

    /// <summary>Cancels the event, which tells Google to notify the attendee.</summary>
    Task DeleteEventAsync(string googleEventId, CancellationToken cancellationToken);
}

/// <summary>
/// The invites when Google is not configured: none, quietly.
/// </summary>
/// <remarks>
/// The same shape as the logging SMS sender: the booking path stays exercisable
/// in development without a Google project, and the email field simply results
/// in no invite rather than an error.
/// </remarks>
public sealed class NoCalendarInvites(ILogger<NoCalendarInvites> logger) : ICalendarInvites
{
    public Task<string?> CreateEventAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Google is not configured; no calendar invite for appointment {AppointmentId}.", appointment.Id);
        return Task.FromResult<string?>(null);
    }

    public Task DeleteEventAsync(string googleEventId, CancellationToken cancellationToken) => Task.CompletedTask;
}
