using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// The record of what the model said before it knew.
/// </summary>
/// <remarks>
/// Every other number this app produces is a claim about the future that
/// nothing later checks. This is the ledger that makes the model falsifiable:
/// a prediction, the plan it assumed, the interval it was quoted with, and — in
/// time — what actually happened.
///
/// The interval matters as much as the median. A model whose point predictions
/// are often wrong but whose 80% intervals contain the outcome about 80% of the
/// time is working correctly; one whose intervals almost always contain the
/// outcome is overcautious and not saying much. Neither can be told apart from
/// a single number.
/// </remarks>
public static class PredictionLedger
{
    public static IEndpointRouteBuilder MapPredictionLedger(this IEndpointRouteBuilder api)
    {
        api.MapGet("/predictions/locked", async (
            FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var rows = await database.Predictions
                .OrderBy(p => p.TargetDate)
                .ToListAsync(cancellationToken);

            return Results.Ok(rows.Select(Dto).ToArray());
        });

        api.MapPost("/predictions/locked", async (
            LockPredictionRequest request,
            FitnessDbContext database,
            CancellationToken cancellationToken) =>
        {
            if (ParseDate(request.TargetDate) is not { } target) return Fail("A target date is needed.");
            if (request.DistanceMeters is < 400 or > 100_000) return Fail("Distance is 400 m to 100 km.");
            if (request.PredictedSeconds <= 0) return Fail("A predicted time is needed.");
            if (request.PredictedFastSeconds > request.PredictedSlowSeconds)
            {
                return Fail("The interval's fast end must not be slower than its slow end.");
            }

            var today = Today();
            if (target <= today) return Fail("A prediction has to be about the future to be worth keeping.");

            database.Predictions.Add(new LockedPrediction
            {
                Id = Guid.NewGuid(),
                MadeOn = today,
                TargetDate = target,
                DistanceMeters = request.DistanceMeters,
                PredictedSeconds = request.PredictedSeconds,
                PredictedFastSeconds = request.PredictedFastSeconds,
                PredictedSlowSeconds = request.PredictedSlowSeconds,
                WeeklyHours = request.WeeklyHours,
                Compliance = request.Compliance,
                RaceMassKg = request.RaceMassKg,
                Note = request.Note
            });

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/predictions/locked/{id:guid}/actual", async (
            Guid id,
            ScorePredictionRequest request,
            FitnessDbContext database,
            CancellationToken cancellationToken) =>
        {
            if (request.ActualSeconds <= 0) return Fail("An actual time is needed.");

            var row = await database.Predictions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (row is null) return Results.NotFound();

            row.ActualSeconds = request.ActualSeconds;
            if (request.Note is not null) row.Note = request.Note;

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapDelete("/predictions/locked/{id:guid}", async (
            Guid id, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var row = await database.Predictions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (row is null) return Results.NotFound();

            database.Predictions.Remove(row);
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return api;
    }

    /// <summary>
    /// Where a prediction stands: waiting, owed an answer, or answered.
    /// </summary>
    internal static string StatusOf(LockedPrediction row, LocalDate today) =>
        row.ActualSeconds is not null ? "scored"
        : row.TargetDate <= today ? "due"
        : "pending";

    internal static LockedPredictionDto Dto(LockedPrediction row) => Dto(row, Today());

    internal static LockedPredictionDto Dto(LockedPrediction row, LocalDate today) =>
        new(
            row.Id.ToString(),
            row.MadeOn.ToString("yyyy-MM-dd", null),
            row.TargetDate.ToString("yyyy-MM-dd", null),
            row.DistanceMeters,
            row.PredictedSeconds,
            row.PredictedFastSeconds,
            row.PredictedSlowSeconds,
            row.WeeklyHours,
            row.Compliance,
            row.RaceMassKg,
            row.ActualSeconds,
            row.Note,
            StatusOf(row, today),
            row.ActualSeconds is { } actual ? actual - row.PredictedSeconds : null,
            row.ActualSeconds is { } inside
                ? inside >= row.PredictedFastSeconds && inside <= row.PredictedSlowSeconds
                : null);

    private static LocalDate Today() =>
        SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Etc/UTC"]).Date;

    private static LocalDate? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = NodaTime.Text.LocalDatePattern.Iso.Parse(value.Trim());
        return parsed.Success ? parsed.Value : null;
    }

    private static IResult Fail(string message) =>
        Results.Text(message, "text/plain", statusCode: StatusCodes.Status400BadRequest);
}
