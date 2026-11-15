using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using aberaTech.Scheduling.Outbox;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Api;

/// <summary>
/// The two changes a booking can take after it is made: canceled, and moved.
/// </summary>
/// <remarks>
/// One engine for each, whoever asks. The cancel route and the calendar sync
/// both cancel through here, so a decline in Google Calendar does exactly
/// what the cancel route does. Neither method saves: the caller commits the
/// booking and its messages together.
/// </remarks>
internal static class BookingChanges
{
    /// <summary>
    /// Cancels a booking, tells the visitor and the host, and stops the reminders.
    /// </summary>
    /// <param name="withdrawInvite">
    /// Whether to delete the Google event. False when the host already
    /// deleted it, which is how the cancellation was learned.
    /// </param>
    public static async Task CancelAsync(
        SchedulingDbContext database,
        Appointment appointment,
        SchedulingOptions options,
        Instant now,
        bool withdrawInvite,
        CancellationToken cancellationToken)
    {
        appointment.Cancelled = true;

        // Anything still waiting to go out about this appointment is now wrong.
        var pending = await database.Outbox
            .Where(message => message.AppointmentId == appointment.Id
                              && (message.State == DeliveryState.Pending || message.State == DeliveryState.Failed))
            .ToListAsync(cancellationToken);

        database.Outbox.RemoveRange(pending);

        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(appointment.BookedZoneId) ?? options.HostZone;

        if (appointment.SmsConsent && !string.IsNullOrEmpty(appointment.PhoneE164))
        {
            database.Outbox.Add(SchedulingEndpoints.NewMessage(
                appointment, NotificationKind.Cancelled, options, zone, now, now));
        }

        if (PhoneNumber.TryParse(options.HostPhoneE164, out var hostPhone))
        {
            var forHost = SchedulingEndpoints.NewMessage(
                appointment, NotificationKind.HostCancelled, options, options.HostZone, now, now);
            forHost.ToPhoneE164 = hostPhone.Value.E164;
            forHost.IdempotencyKey = $"{appointment.Id}:{NotificationKind.HostCancelled}";
            database.Outbox.Add(forHost);
        }

        // Withdrawn by the outbox, so Google being down delays the withdrawal
        // rather than losing it. Queued whether or not the invite went: the
        // event's id is the booking's, so deleting one that was never made
        // is a 404, which counts as done. Half a minute later than now, so an
        // invite already in flight lands before its withdrawal.
        if (withdrawInvite && !string.IsNullOrEmpty(appointment.Email))
        {
            database.Outbox.Add(SchedulingEndpoints.CalendarJob(
                appointment, NotificationKind.CalendarWithdraw, now, now + Duration.FromSeconds(30)));
        }
    }

    /// <summary>
    /// Moves a booking to the time its calendar event now has.
    /// </summary>
    /// <remarks>
    /// Reminders not yet sent are for the old time, so they are removed and
    /// planned again from the new one by <see cref="ReminderPlanner"/>, the
    /// same planner a new booking uses. The visitor is texted the new time if
    /// they agreed to texts. Google has already emailed them the change.
    ///
    /// A booking can move more than once, and the outbox holds each
    /// idempotency key once. So the keys written here carry the second of the
    /// move, where a new booking's carry only the kind.
    /// </remarks>
    public static async Task MoveAsync(
        SchedulingDbContext database,
        Appointment appointment,
        SchedulingOptions options,
        Instant startsAt,
        Instant endsAt,
        Instant now,
        CancellationToken cancellationToken)
    {
        var stale = await database.Outbox
            .Where(message => message.AppointmentId == appointment.Id
                              && (message.Kind == NotificationKind.ReminderDayBefore
                                  || message.Kind == NotificationKind.Reminder)
                              && (message.State == DeliveryState.Pending || message.State == DeliveryState.Failed))
            .ToListAsync(cancellationToken);

        database.Outbox.RemoveRange(stale);

        appointment.StartsAt = startsAt;
        appointment.EndsAt = endsAt;

        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(appointment.BookedZoneId) ?? options.HostZone;

        if (appointment.SmsConsent && !string.IsNullOrEmpty(appointment.PhoneE164))
        {
            database.Outbox.Add(Keyed(
                SchedulingEndpoints.NewMessage(appointment, NotificationKind.Moved, options, zone, now, now), now));

            foreach (var reminder in ReminderPlanner.Plan(
                         now,
                         startsAt,
                         Duration.FromMinutes(options.EarlyReminderLeadMinutes),
                         Duration.FromMinutes(options.ReminderLeadMinutes)))
            {
                database.Outbox.Add(Keyed(
                    SchedulingEndpoints.NewMessage(appointment, reminder.Kind, options, zone, now, reminder.DueAt), now));
            }
        }

        if (PhoneNumber.TryParse(options.HostPhoneE164, out var hostPhone))
        {
            var forHost = Keyed(
                SchedulingEndpoints.NewMessage(appointment, NotificationKind.HostMoved, options, options.HostZone, now, now),
                now);
            forHost.ToPhoneE164 = hostPhone.Value.E164;
            database.Outbox.Add(forHost);
        }
    }

    /// <summary>The message under a key no earlier move used.</summary>
    /// <remarks>32 hex digits, the kind's number and the second: at most 55 of the column's 64 characters.</remarks>
    private static OutboxMessage Keyed(OutboxMessage message, Instant now)
    {
        message.IdempotencyKey = MoveKey(message.AppointmentId!.Value, message.Kind, now);
        return message;
    }

    internal static string MoveKey(Guid appointmentId, NotificationKind kind, Instant now) =>
        $"{appointmentId:N}:{(int)kind}:{now.ToUnixTimeSeconds()}";
}
