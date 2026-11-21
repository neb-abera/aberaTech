using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using aberaTech.Fitness.OneDrive;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Server.App;

/// <summary>One goal, as the app shows it: where it stands and where it is heading.</summary>
public sealed record AppGoalDto(
    string Label,
    string Unit,
    bool LowerIsBetter,
    double? Current,
    string? CurrentOn,
    double? Goal,
    bool? Met,
    double? Projected,
    double? Probability);

public sealed record AppGoalSectionDto(string Name, IReadOnlyList<AppGoalDto> Goals);

public sealed record AppWeighInDto(string Date, double Pounds);

public sealed record AppFitnessDto(
    string? DueOn,
    IReadOnlyList<AppGoalSectionDto> Sections,
    IReadOnlyList<AppWeighInDto> Weight);

/// <summary>
/// Neb's fitness for the app, read-only: the goals from his workbook with
/// today's standing and the forecast at the due date, and his weigh-ins of
/// the last 120 days. The same numbers abera.tech/fitness shows, from the
/// same report, cut to what a reader on a phone needs.
/// </summary>
public static class AppFitness
{
    public static readonly Duration WeightWindow = Duration.FromDays(120);

    public static IEndpointRouteBuilder MapAppFitnessEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/app/fitness", async (
                FitnessDbContext database, IWorkbookSource source, IClock clock, CancellationToken cancellationToken) =>
            {
                var now = clock.GetCurrentInstant();
                var goals = await GoalsReport.BuildAsync(database, source, now, cancellationToken);
                var since = now.InUtc().Date.Minus(Period.FromDays((int)WeightWindow.TotalDays));
                var weights = await database.BodyMetrics.AsNoTracking()
                    .Where(m => m.Date >= since)
                    .OrderBy(m => m.Date)
                    .Select(m => new { m.Date, m.WeightKg })
                    .ToListAsync(cancellationToken);
                return Results.Ok(Shape(goals, weights.Select(w => new AppWeighInDto(AppRequests.Iso(w.Date), Math.Round(w.WeightKg * BodyMass.PoundsPerKg, 1))).ToList()));
            })
            .RequireAuthorization(AppAuth.SignedInPolicy);

        return routes;
    }

    internal static AppFitnessDto Shape(GoalsDto goals, IReadOnlyList<AppWeighInDto> weight) =>
        new(goals.DueOn,
            goals.Sections
                .Select(section => new AppGoalSectionDto(section.Name, section.Rows
                    .Where(row => row.Goal is not null)
                    .Select(row => new AppGoalDto(
                        row.Label, row.Unit, row.LowerIsBetter,
                        row.Current?.Value, row.Current?.On, row.Goal, row.Met,
                        row.Forecast?.Projected, row.Forecast?.Probability))
                    .ToList()))
                .Where(section => section.Goals.Count > 0)
                .ToList(),
            weight);
}
