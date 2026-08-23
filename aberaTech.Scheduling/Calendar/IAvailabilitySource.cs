using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>Where the host's open time comes from.</summary>
/// <remarks>
/// Two implementations, because there are two honest answers. Rules stored here
/// need no Google account and work offline. A calendar the host already keeps is
/// the one they actually maintain — and maintaining hours in two places means
/// the two disagree, usually at the worst moment.
/// </remarks>
public interface IAvailabilitySource
{
    Task<IReadOnlyList<Interval>> GetOpenWindowsAsync(
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken);
}

/// <summary>Open time from the availability rules in this database.</summary>
public sealed class RuleAvailabilitySource(SchedulingDbContext database) : IAvailabilitySource
{
    public async Task<IReadOnlyList<Interval>> GetOpenWindowsAsync(
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        var rules = await database.AvailabilityRules
            .Where(rule => rule.Active)
            .ToListAsync(cancellationToken);

        var windows = new List<Interval>();

        for (var date = from; date <= to; date = date.PlusDays(1))
        {
            foreach (var rule in rules)
            {
                if (rule.ToDomain().Materialise(date) is { } window)
                {
                    windows.Add(window);
                }
            }
        }

        return windows;
    }
}
