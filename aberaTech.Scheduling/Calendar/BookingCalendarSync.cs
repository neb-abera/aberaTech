using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Keeps each upcoming booking in step with its Google Calendar event.
/// </summary>
/// <remarks>
/// A visitor cancels by declining the invite and asks for another time by
/// proposing one, both in their own calendar. The host accepts a proposal by
/// moving the event, and cancels by deleting it. This worker reads each
/// upcoming booking's event and applies what it finds, so the free times on
/// the page and the reminder texts follow the calendar.
///
/// It polls. A change shows on the site within one interval. Each change is
/// saved in its own scope, so one that cannot be saved does not hold up the
/// rest.
/// </remarks>
public sealed class BookingCalendarSync(
    IServiceScopeFactory scopeFactory,
    SchedulingOptions options,
    IClock clock,
    ILogger<BookingCalendarSync> logger) : BackgroundService
{
    /// <summary>Bookings read per pass, soonest first.</summary>
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.CalendarSyncSeconds));
        logger.LogInformation("Booking calendar sync started, reading every {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // One bad pass must not end the loop, as in the dispatcher.
                logger.LogError(exception, "Booking calendar sync pass failed. Continuing.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One pass over the upcoming bookings whose invite Google holds.</summary>
    /// <remarks>
    /// The events are read through one scope, so a pass asks Google for one
    /// access token however many bookings it reads. A pass with no upcoming
    /// booking makes no call to Google at all. Each change is then saved in a
    /// scope of its own.
    /// </remarks>
    internal async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();

        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        // GoogleEventId is set when Google confirms the invite. Before that
        // there is no event to read.
        var upcoming = await database.Appointments
            .AsNoTracking()
            .Where(appointment => !appointment.Cancelled
                                  && appointment.GoogleEventId != null
                                  && appointment.EndsAt > now)
            .OrderBy(appointment => appointment.StartsAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (upcoming.Count == 0)
        {
            return;
        }

        var invites = scope.ServiceProvider.GetRequiredService<ICalendarInvites>();

        foreach (var appointment in upcoming)
        {
            var google = await invites.ReadAsync(appointment, cancellationToken);

            if (google.State == BookingEventState.Unknown)
            {
                logger.LogDebug(
                    "Booking {AppointmentId} was not read from Google: {Reason}", appointment.Id, google.Reason);
                continue;
            }

            if (BookingSync.Decide(appointment.StartsAt, appointment.EndsAt, google).Action == BookingSyncAction.Keep)
            {
                continue;
            }

            try
            {
                await ApplyAsync(appointment.Id, google, cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                // Two causes, both left for the next pass. The event was
                // moved onto another booking's time, which the overlap
                // constraint refuses. Or another replica saved the same
                // change first, and its messages hold the idempotency keys.
                logger.LogWarning(
                    exception,
                    "Booking {AppointmentId} could not follow its calendar event{Overlap}.",
                    appointment.Id,
                    exception.InnerException is PostgresException { SqlState: "23P01" }
                        ? ": the new time overlaps another booking"
                        : string.Empty);
            }
        }
    }

    /// <summary>Applies what Google holds to the booking as it is now, and saves.</summary>
    /// <remarks>
    /// Decided again from the row just loaded. The cancel route or another
    /// replica may have changed the booking since the pass read it.
    /// </remarks>
    private async Task ApplyAsync(Guid id, BookingEvent google, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        var appointment = await database.Appointments
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (appointment is null || appointment.Cancelled)
        {
            return;
        }

        var decision = BookingSync.Decide(appointment.StartsAt, appointment.EndsAt, google);
        var now = clock.GetCurrentInstant();

        switch (decision.Action)
        {
            case BookingSyncAction.CancelDeclined:
                await BookingChanges.CancelAsync(database, appointment, options, now, withdrawInvite: true, cancellationToken);
                break;

            case BookingSyncAction.CancelDeleted:
                await BookingChanges.CancelAsync(database, appointment, options, now, withdrawInvite: false, cancellationToken);
                break;

            case BookingSyncAction.Move:
                await BookingChanges.MoveAsync(
                    database, appointment, options, decision.StartsAt, decision.EndsAt, now, cancellationToken);
                break;

            default:
                return;
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Booking {AppointmentId} followed its calendar event: {Action}.", id, decision.Action);
    }
}
