using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Outbox;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Sends and withdraws the calendar invitation for a booking.
/// </summary>
/// <remarks>
/// The invite is a Google Calendar event on the host's calendar with the
/// visitor as attendee. Google then emails the invitation, so the site needs
/// no mail server, and the visitor gets a real invite they can accept.
///
/// The outbox dispatcher calls these, never a request handler. Each answer is
/// a <see cref="SendResult"/>, so an invite is retried, dead lettered and shown
/// to the host the same way a text is. Google sends no delivery receipt, so a
/// confirmed result means Google created the event, and nothing more.
/// </remarks>
public interface ICalendarInvites
{
    /// <summary>Creates the event, or confirms an earlier attempt already did.</summary>
    Task<SendResult> InviteAsync(Appointment appointment, CancellationToken cancellationToken);

    /// <summary>Deletes the event, which tells Google to notify the attendee.</summary>
    Task<SendResult> WithdrawAsync(Appointment appointment, CancellationToken cancellationToken);
}

/// <summary>The invites when Google is not configured: none, and the outbox says so.</summary>
/// <remarks>
/// Refused permanently rather than retried, because no retry can configure a
/// Google project. The row dead letters at once and the host sees why.
/// </remarks>
public sealed class NoCalendarInvites(ILogger<NoCalendarInvites> logger) : ICalendarInvites
{
    public const string Reason = "Google Calendar is not configured on this deployment.";

    public Task<SendResult> InviteAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        logger.LogInformation("No calendar invite for appointment {AppointmentId}: {Reason}", appointment.Id, Reason);
        return Task.FromResult(SendResult.RejectedPermanently(Reason));
    }

    public Task<SendResult> WithdrawAsync(Appointment appointment, CancellationToken cancellationToken) =>
        Task.FromResult(SendResult.RejectedPermanently(Reason));
}
