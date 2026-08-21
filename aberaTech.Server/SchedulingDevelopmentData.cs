using aberaTech.Scheduling;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Server;

/// <summary>
/// Enough availability for the booking page to show something on a fresh
/// development database.
/// </summary>
/// <remarks>
/// Development only, and only when the table is empty, so it never overwrites
/// real rules and never runs against the deployed database. The real rules are
/// the host's to set; this exists so that `make db` followed by a run produces a
/// page with slots on it rather than an empty state that looks like a bug.
/// </remarks>
public static class SchedulingDevelopmentData
{
    public static async Task SeedAsync(SchedulingDbContext database, SchedulingOptions options)
    {
        if (await database.AvailabilityRules.AnyAsync())
        {
            return;
        }

        IsoDayOfWeek[] weekdays =
        [
            IsoDayOfWeek.Monday,
            IsoDayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday,
            IsoDayOfWeek.Friday
        ];

        foreach (var day in weekdays)
        {
            database.AvailabilityRules.Add(new AvailabilityRuleRecord
            {
                Id = Guid.NewGuid(),
                Day = day,
                StartsAt = new LocalTime(9, 0),
                EndsAt = new LocalTime(17, 0),
                ZoneId = options.HostZoneId,
                Active = true
            });
        }

        await database.SaveChangesAsync();
    }
}
