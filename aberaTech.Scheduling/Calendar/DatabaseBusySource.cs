using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>Appointments booked here. Always available, always authoritative.</summary>
public sealed class DatabaseBusySource(SchedulingDbContext database) : IBusySource
{
    public async Task<IReadOnlyList<Interval>> GetBusyAsync(Interval range, CancellationToken cancellationToken)
    {
        var windows = await database.Appointments
            .Where(appointment => !appointment.Cancelled
                                  && appointment.EndsAt > range.Start
                                  && appointment.StartsAt < range.End)
            .Select(appointment => new { appointment.StartsAt, appointment.EndsAt })
            .ToListAsync(cancellationToken);

        return windows.Select(window => new Interval(window.StartsAt, window.EndsAt)).ToList();
    }
}
