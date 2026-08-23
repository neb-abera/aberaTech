using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Admin;

/// <summary>One day's opening hours, as the editor sees them.</summary>
/// <param name="Day">1 is Monday, 7 is Sunday, matching IsoDayOfWeek.</param>
/// <param name="StartsAt">Civil time, "HH:mm".</param>
public sealed record AvailabilityDay(int Day, string StartsAt, string EndsAt, bool Active);

/// <summary>The whole week, plus the zone it is written in.</summary>
/// <remarks>
/// One zone for the set rather than one per day. The underlying rules can each
/// carry their own, but nobody keeps Tuesday in a different time zone from
/// Wednesday, and offering that choice would be seven chances to create a
/// week that is subtly wrong.
/// </remarks>
public sealed record AvailabilityWeek(string ZoneId, IReadOnlyList<AvailabilityDay> Days);

/// <summary>A week that passed validation, ready to store.</summary>
public sealed record ValidatedWeek(string ZoneId, IReadOnlyList<(int Day, LocalTime Start, LocalTime End, bool Active)> Days);

public static class AvailabilityEndpoints
{
    internal static readonly LocalTimePattern TimePattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    /// <summary>
    /// Checks a submitted week, returning either the parsed result or the one
    /// thing wrong with it.
    /// </summary>
    /// <remarks>
    /// Separated from the endpoint so the rules can be asserted without a
    /// signed-in session and a database. They are the part most likely to be
    /// got wrong, and the part whose failure is least visible: a week that is
    /// accepted with a bad day quietly stops offering that day.
    ///
    /// All or nothing. A week with one bad day is refused whole rather than
    /// written in part, so the stored hours are never a mixture of what was
    /// asked for and what was already there.
    /// </remarks>
    internal static (ValidatedWeek? Week, string? Error) Validate(AvailabilityWeek? request)
    {
        if (request is null)
        {
            return (null, "Send the whole week.");
        }

        if (DateTimeZoneProviders.Tzdb.GetZoneOrNull(request.ZoneId ?? string.Empty) is null)
        {
            return (null, "That is not a time zone name.");
        }

        var parsed = new List<(int Day, LocalTime Start, LocalTime End, bool Active)>();
        var seen = new HashSet<int>();

        foreach (var day in request.Days ?? [])
        {
            if (day.Day is < 1 or > 7)
            {
                return (null, "Days run from 1 (Monday) to 7 (Sunday).");
            }

            if (!seen.Add(day.Day))
            {
                // Two rows for one day would make which one wins depend on
                // insertion order, which is no answer at all.
                return (null, "That week has the same day twice.");
            }

            var start = TimePattern.Parse(day.StartsAt ?? string.Empty);
            var end = TimePattern.Parse(day.EndsAt ?? string.Empty);

            if (!start.Success || !end.Success)
            {
                return (null, "Times must look like 07:00.");
            }

            // Only meaningful for a day that is on. An inactive day keeps
            // whatever times it had, so turning it back on restores them.
            if (day.Active && end.Value <= start.Value)
            {
                return (null, "A day has to end after it starts. Overnight hours are not supported yet.");
            }

            parsed.Add((day.Day, start.Value, end.Value, day.Active));
        }

        return parsed.Count == 0
            ? (null, "Send the whole week.")
            : (new ValidatedWeek(request.ZoneId!, parsed), null);
    }

    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/scheduling/admin/availability")
            .RequireAuthorization(AdminAuth.PolicyName);

        group.MapGet("/", GetAsync);
        group.MapPut("/", ReplaceAsync);

        return routes;
    }

    private static async Task<IResult> GetAsync(
        SchedulingDbContext database,
        SchedulingOptions options,
        CancellationToken cancellationToken)
    {
        var rules = await database.AvailabilityRules.ToListAsync(cancellationToken);

        // Always seven days back, whether or not a rule exists for each. An
        // editor that only shows the days already configured gives you no way
        // to add the ones that are not.
        var days = Enumerable.Range(1, 7).Select(day =>
        {
            var rule = rules.FirstOrDefault(candidate => (int)candidate.Day == day);

            return rule is null
                ? new AvailabilityDay(day, "09:00", "17:00", false)
                : new AvailabilityDay(
                    day,
                    TimePattern.Format(rule.StartsAt),
                    TimePattern.Format(rule.EndsAt),
                    rule.Active);
        }).ToList();

        var zone = rules.FirstOrDefault()?.ZoneId ?? options.HostZoneId;

        return Results.Ok(new AvailabilityWeek(zone, days));
    }

    /// <summary>
    /// Replaces the whole week.
    /// </summary>
    /// <remarks>
    /// Replace rather than patch. There are seven rows, the editor always shows
    /// all of them, and sending the whole set removes the class of bug where a
    /// day is left behind because the update addressed only what changed.
    ///
    /// Validated before anything is written, so a week with one bad day is
    /// rejected whole rather than applied in part.
    /// </remarks>
    private static async Task<IResult> ReplaceAsync(
        AvailabilityWeek request,
        SchedulingDbContext database,
        CancellationToken cancellationToken)
    {
        var (week, error) = Validate(request);

        if (week is null)
        {
            return Results.BadRequest(new { error });
        }

        var existing = await database.AvailabilityRules.ToListAsync(cancellationToken);
        database.AvailabilityRules.RemoveRange(existing);

        foreach (var (day, start, end, active) in week.Days)
        {
            database.AvailabilityRules.Add(new AvailabilityRuleRecord
            {
                Id = Guid.NewGuid(),
                Day = (IsoDayOfWeek)day,
                StartsAt = start,
                EndsAt = end,
                ZoneId = week.ZoneId,
                Active = active
            });
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}