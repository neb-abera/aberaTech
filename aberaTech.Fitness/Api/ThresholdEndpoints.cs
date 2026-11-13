using aberaTech.Fitness.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Api;

/// <summary>One threshold value to log: its kind, the value, and the day of the test.</summary>
public sealed record ThresholdLog(
    string Kind,
    double Value,
    string MeasuredOn,
    string? Source = null,
    Guid? ActivityId = null);

/// <summary>A logged threshold value, and whether it is the one in use.</summary>
public sealed record ThresholdEntryDto(
    Guid Id,
    string Kind,
    double Value,
    string? MeasuredOn,
    string? Source,
    Guid? ActivityId,
    bool Current);

/// <summary>
/// The threshold log. Each value is logged on its own and kept. The one in
/// use for each kind is the latest by test date, and the settings row
/// mirrors it, so every zone and trend reads the same number it always did.
/// </summary>
public static class ThresholdEndpoints
{
    public static RouteGroupBuilder MapThresholdEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/thresholds", async (FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            var entries = await database.Thresholds.AsNoTracking().ToListAsync(cancellationToken);
            var current = CurrentIds(entries);
            return Results.Ok(Ordered(entries).Select(e => new ThresholdEntryDto(
                e.Id,
                e.Kind,
                e.Value,
                e.MeasuredOn?.ToString("yyyy-MM-dd", null),
                e.Source,
                e.ActivityId,
                current.Contains(e.Id))));
        });

        api.MapPost("/thresholds", async (
            ThresholdLog log,
            FitnessDbContext database,
            PosteriorCache cache,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!ThresholdEntry.Kinds.Contains(log.Kind))
            {
                return Fail("Kind is aet-hr, lt-hr, or lt-pace.");
            }

            var parsed = string.IsNullOrWhiteSpace(log.MeasuredOn)
                ? null
                : LocalDatePattern.Iso.Parse(log.MeasuredOn.Trim()) is { Success: true } p ? p.Value : (LocalDate?)null;
            if (parsed is not { } measuredOn) return Fail("The test date is yyyy-mm-dd.");
            if (measuredOn > clock.GetCurrentInstant().InUtc().Date.PlusDays(1))
            {
                return Fail("The test date cannot be in the future.");
            }

            if (log.Kind is ThresholdEntry.AetHr or ThresholdEntry.LtHr
                && (log.Value is < 80 or > 220 || log.Value != Math.Round(log.Value)))
            {
                return Fail("A heart rate is a whole number from 80 to 220 bpm.");
            }

            if (log.Kind is ThresholdEntry.LtPace && log.Value is < 120 or > 900)
            {
                return Fail("LT pace is 2:00 to 15:00 per km.");
            }

            if (log.Source is { Length: > 200 }) return Fail("The source note is 200 characters at most.");

            var entries = await database.Thresholds.ToListAsync(cancellationToken);
            var entry = new ThresholdEntry
            {
                Id = Guid.NewGuid(),
                Kind = log.Kind,
                Value = log.Value,
                MeasuredOn = measuredOn,
                Source = string.IsNullOrWhiteSpace(log.Source) ? "entered by hand" : log.Source.Trim(),
                ActivityId = log.ActivityId,
                RecordedAt = clock.GetCurrentInstant(),
            };

            // The LT heart rate sits above the AeT. Checked against what
            // would be in use once this is logged, so an older test logged
            // for the record is never refused for a newer one's sake.
            var after = entries.Append(entry).ToList();
            var row = await SettingsRow(database, cancellationToken);
            var aet = Latest(after, ThresholdEntry.AetHr)?.Value ?? row.ReferenceHr;
            var lt = Latest(after, ThresholdEntry.LtHr)?.Value ?? row.LtHr;
            if (lt is { } ltHr && ltHr <= aet)
            {
                return Fail(
                    $"The LT heart rate ({ltHr:0}) has to sit above the AeT heart rate ({aet:0}). Log the other one first if it changed too.");
            }

            database.Thresholds.Add(entry);
            Mirror(row, after, entry.Kind);
            await database.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
            return Results.Ok(new { entry.Id, Current = CurrentIds(after).Contains(entry.Id) });
        });

        api.MapDelete("/thresholds/{id:guid}", async (
            Guid id,
            FitnessDbContext database,
            PosteriorCache cache,
            CancellationToken cancellationToken) =>
        {
            var entries = await database.Thresholds.ToListAsync(cancellationToken);
            var entry = entries.SingleOrDefault(e => e.Id == id);
            if (entry is null) return Results.NotFound();

            var after = entries.Where(e => e.Id != id).ToList();
            if (entry.Kind == ThresholdEntry.AetHr && Latest(after, ThresholdEntry.AetHr) is null)
            {
                return Fail("Every zone runs on the AeT heart rate. Log the right value before removing the last one.");
            }

            database.Thresholds.Remove(entry);
            Mirror(await SettingsRow(database, cancellationToken), after, entry.Kind);
            await database.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
            return Results.NoContent();
        });

        return api;
    }

    /// <summary>Newest test first; a carried-over value with no date sorts last.</summary>
    internal static IEnumerable<ThresholdEntry> Ordered(IEnumerable<ThresholdEntry> entries) =>
        entries
            .OrderByDescending(e => e.MeasuredOn.HasValue)
            .ThenByDescending(e => e.MeasuredOn)
            .ThenByDescending(e => e.RecordedAt);

    internal static ThresholdEntry? Latest(IEnumerable<ThresholdEntry> entries, string kind) =>
        Ordered(entries.Where(e => e.Kind == kind)).FirstOrDefault();

    private static HashSet<Guid> CurrentIds(IReadOnlyCollection<ThresholdEntry> entries) =>
        ThresholdEntry.Kinds
            .Select(kind => Latest(entries, kind))
            .OfType<ThresholdEntry>()
            .Select(e => e.Id)
            .ToHashSet();

    private static async Task<AthleteSettings> SettingsRow(FitnessDbContext database, CancellationToken cancellationToken)
    {
        var row = await database.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
        if (row is null)
        {
            row = new AthleteSettings { Id = 1 };
            database.Settings.Add(row);
        }

        return row;
    }

    /// <summary>
    /// Copies the value in use for one kind onto the settings row the zones
    /// read. Only that kind: logging an LT pace never touches either heart rate.
    /// </summary>
    private static void Mirror(AthleteSettings row, IReadOnlyCollection<ThresholdEntry> entries, string kind)
    {
        var latest = Latest(entries, kind)?.Value;
        switch (kind)
        {
            case ThresholdEntry.AetHr when latest is { } aet:
                row.ReferenceHr = (int)Math.Round(aet);
                break;
            case ThresholdEntry.LtHr:
                row.LtHr = latest is { } lt ? (int)Math.Round(lt) : null;
                break;
            case ThresholdEntry.LtPace:
                row.LtSecondsPerKm = latest;
                break;
        }
    }

    private static IResult Fail(string message) =>
        Results.Text(message, "text/plain", statusCode: StatusCodes.Status400BadRequest);
}
