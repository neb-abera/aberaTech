using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;

namespace aberaTech.Fitness.Api;

/// <summary>A what-if, exactly as the page states it.</summary>
public sealed record ScenarioRequest(
    double DistanceMeters,
    double Months,
    double WeeklyHours,
    double Compliance,
    double? RaceMassKg = null,
    double StrengthHours = 0,
    double? StartHours = null,
    double RampPerWeek = 0,
    bool UseHistory = true);

/// <summary>Solve for one factor, or leave it null to just predict.</summary>
public sealed record SolveRequest(
    ScenarioRequest Scenario,
    string? SolveFor = null,
    double? TargetSeconds = null);

/// <summary>Two factors, swept.</summary>
public sealed record SurfaceRequest(
    ScenarioRequest Scenario,
    string Across,
    string Down,
    double? TargetSeconds = null,
    int Resolution = 28);

public sealed record SpreadDto(
    double Median, double Low, double High, double Impossible, double AlreadyMet);

public sealed record SensitivityDto(
    string Factor,
    string Label,
    double Value,
    double Elasticity,
    double PerUnitSeconds,
    double LowValue,
    double HighValue,
    double LowSeconds,
    double HighSeconds,
    double Swing);

public sealed record ParameterDto(string Name, double Median, double Low, double High, string Unit);

public sealed record ModelDto(
    IReadOnlyList<ParameterDto> Parameters,
    double AcceptanceRate,
    double RHat,
    double EffectiveSampleSize,
    bool Converged,
    int Observations,
    int TimeTrials,
    int Draws,
    IReadOnlyList<StepDto> Steps);

public sealed record SolveDto(
    SpreadDto Predicted,
    string? SolveFor,
    SpreadDto? Solved,
    double? Probability,
    IReadOnlyList<SensitivityDto> Sensitivities,
    IReadOnlyList<BandDto> Fan,
    ModelDto Model,
    IReadOnlyList<StepDto> Steps);

public sealed record SurfaceDto(
    string Across,
    string Down,
    IReadOnlyList<double> AcrossValues,
    IReadOnlyList<double> DownValues,
    IReadOnlyList<IReadOnlyList<double>> Seconds,
    double? TargetSeconds);

public sealed record MeasurementDto(
    double AtMonths,
    string Kind,
    double WidthBeforeSeconds,
    double WidthAfterSeconds,
    double Reduction);

public sealed record MeasurePlanDto(
    IReadOnlyList<MeasurementDto> Options,
    IReadOnlyList<StepDto> Steps);

/// <summary>
/// The what-if surface of the API: one scenario in, and whichever of predict,
/// solve, differentiate or sweep the caller asked for.
/// </summary>
public static class SolverEndpoints
{
    public static IEndpointRouteBuilder MapSolverEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/model", async (
            FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            var context = await FitnessReports.SolverContextAsync(
                database, cache, DateTime.UtcNow.Year, cancellationToken);
            return Results.Ok(Model(context.Posterior));
        });

        api.MapPost("/solve", async (
            SolveRequest request,
            FitnessDbContext database,
            PosteriorCache cache,
            CancellationToken cancellationToken) =>
        {
            if (Validate(request.Scenario) is { } complaint) return Results.BadRequest(complaint);

            Factor? unknown = null;
            if (request.SolveFor is not null)
            {
                if (!Enum.TryParse<Factor>(request.SolveFor, ignoreCase: true, out var parsed))
                {
                    return Results.BadRequest($"Unknown factor '{request.SolveFor}'.");
                }

                if (request.TargetSeconds is not > 0)
                {
                    return Results.BadRequest("Solving for a factor needs a target time.");
                }

                unknown = parsed;
            }

            var context = await FitnessReports.SolverContextAsync(
                database, cache, DateTime.UtcNow.Year, cancellationToken, request.Scenario.UseHistory);
            var scenario = Of(request.Scenario, context);

            var predicted = Solver.Predict(context, scenario);
            var sensitivities = Solver.Sensitivities(context, scenario);

            Spread? solved = null;
            double? probability = null;
            var steps = new List<CalculationStep>();

            if (unknown is { } factor && request.TargetSeconds is { } target)
            {
                solved = Solver.Solve(context, scenario, factor, target);
                probability = Solver.Probability(context, scenario, target);
                steps.AddRange(Solver.Explain(context, scenario, factor, target, solved));
            }

            // The fan is the same scenario read at a series of horizons, which
            // is what turns one answer into a picture of the whole approach.
            var fan = new List<BandDto>();
            for (var months = 0.0; months <= Math.Max(30, scenario.Months); months += 1.5)
            {
                var spread = Solver.Predict(context, scenario with { Months = months }, draws: 120);
                fan.Add(new BandDto(months, spread.Median, spread.Low, spread.High, 0));
            }

            return Results.Ok(new SolveDto(
                Dto(predicted),
                unknown?.ToString(),
                solved is null ? null : Dto(solved),
                probability,
                sensitivities.Select(Dto).ToArray(),
                fan,
                Model(context.Posterior),
                steps.Select(Step).ToArray()));
        });

        api.MapPost("/surface", async (
            SurfaceRequest request,
            FitnessDbContext database,
            PosteriorCache cache,
            CancellationToken cancellationToken) =>
        {
            if (Validate(request.Scenario) is { } complaint) return Results.BadRequest(complaint);
            if (!Enum.TryParse<Factor>(request.Across, true, out var across)
                || !Enum.TryParse<Factor>(request.Down, true, out var down)
                || across == down)
            {
                return Results.BadRequest("Two different known factors are needed.");
            }

            var resolution = Math.Clamp(request.Resolution, 8, 48);
            var context = await FitnessReports.SolverContextAsync(
                database, cache, DateTime.UtcNow.Year, cancellationToken, request.Scenario.UseHistory);
            var scenario = Of(request.Scenario, context);

            var acrossRange = Solver.Range(context, scenario, across);
            var downRange = Solver.Range(context, scenario, down);
            var grid = Solver.Surface(context, scenario, across, down, acrossRange, downRange, resolution);

            var rows = new List<IReadOnlyList<double>>();
            for (var row = 0; row < resolution; row++)
            {
                var values = new double[resolution];
                for (var column = 0; column < resolution; column++) values[column] = grid[row, column];
                rows.Add(values);
            }

            return Results.Ok(new SurfaceDto(
                across.ToString(),
                down.ToString(),
                Axis(acrossRange, resolution),
                Axis(downRange, resolution),
                rows,
                request.TargetSeconds));
        });

        api.MapPost("/measure", async (
            ScenarioRequest request,
            FitnessDbContext database,
            PosteriorCache cache,
            CancellationToken cancellationToken) =>
        {
            if (Validate(request) is { } complaint) return Results.BadRequest(complaint);

            var context = await FitnessReports.SolverContextAsync(
                database, cache, DateTime.UtcNow.Year, cancellationToken, request.UseHistory);
            var options = Information.Options(context, Of(request, context));

            return Results.Ok(new MeasurePlanDto(
                options
                    .Select(o => new MeasurementDto(
                        o.AtMonths, o.Kind.ToString(), o.WidthBefore, o.WidthAfter, o.Reduction))
                    .ToArray(),
                Information.Explain(options).Select(Step).ToArray()));
        });

        return api;
    }

    private static string? Validate(ScenarioRequest scenario) =>
        scenario switch
        {
            { DistanceMeters: < 400 or > 100_000 } => "Distance is 400 m to 100 km.",
            { Months: < 0 or > 120 } => "Horizon is 0 to 120 months.",
            { WeeklyHours: < 0 or > 40 } => "Weekly hours are 0 to 40.",
            { Compliance: <= 0 or > 1 } => "Compliance is above 0 and at most 1.",
            { StrengthHours: < 0 or > 20 } => "Strength hours are 0 to 20.",
            { RaceMassKg: < 30 or > 250 } => "Race weight is 30 to 250 kg.",
            { StartHours: < 0 or > 40 } => "Starting hours are 0 to 40 a week.",
            { RampPerWeek: < 0 or > 0.5 } => "A ramp is 0 to 50% a week.",
            _ => null
        };

    private static Scenario Of(ScenarioRequest request, SolverContext context) =>
        new(
            request.DistanceMeters,
            request.Months,
            request.WeeklyHours,
            request.Compliance,
            request.RaceMassKg ?? context.CurrentMassKg,
            request.StrengthHours,
            request.StartHours,
            request.RampPerWeek);

    private static IReadOnlyList<double> Axis((double Low, double High) range, int resolution) =>
        Enumerable.Range(0, resolution)
            .Select(i => range.Low + (range.High - range.Low) * i / (resolution - 1.0))
            .ToArray();

    private static SpreadDto Dto(Spread spread) =>
        new(spread.Median, spread.Low, spread.High, spread.Impossible, spread.AlreadyMet);

    private static SensitivityDto Dto(FactorSensitivity sensitivity) =>
        new(
            sensitivity.Factor.ToString(),
            Solver.Name(sensitivity.Factor),
            sensitivity.Value,
            sensitivity.Elasticity,
            sensitivity.PerUnitSeconds,
            sensitivity.LowValue,
            sensitivity.HighValue,
            sensitivity.LowSeconds,
            sensitivity.HighSeconds,
            sensitivity.Swing);

    private static StepDto Step(CalculationStep step) =>
        new(step.Label, step.Expression, step.Value, step.CitationId);

    internal static ModelDto Model(PosteriorSamples posterior)
    {
        ParameterDto Parameter(string name, Func<ParameterDraw, double> pick, string unit)
        {
            var (median, low, high) = posterior.Summary(pick);
            return new ParameterDto(name, median, low, high, unit);
        }

        return new ModelDto(
            [
                Parameter("Starting VDOT", d => d.StartVdot, "VDOT"),
                Parameter("Approach rate", d => d.RatePerMonth, "per month"),
                Parameter("Responsiveness", d => d.Responsiveness, "× reference"),
                Parameter("Pace-proxy scale", d => d.PaceScale, "× VDOT"),
                Parameter("Month-to-month noise", d => d.NoiseSd, "VDOT")
            ],
            posterior.Diagnostics.AcceptanceRate,
            posterior.Diagnostics.RHat,
            posterior.Diagnostics.EffectiveSampleSize,
            posterior.Diagnostics.Converged,
            posterior.Observations,
            posterior.TimeTrials,
            posterior.Draws.Count,
            posterior.Steps.Select(Step).ToArray());
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
