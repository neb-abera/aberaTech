using aberaTech.Fitness.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Api;

/// <summary>A race or time trial to enter by hand.</summary>
public sealed record RaceEntry(string Date, string Name, double DistanceMeters, double Seconds, double? AltitudeMeters = null);

/// <summary>A place to add to the place log.</summary>
public sealed record PlaceEntry(string? Since, string Place, double AltitudeMeters);

/// <summary>
/// Races entered by hand, and the place log that sets each test's altitude.
/// Both are kept entry by entry and removed one at a time. The anchor is
/// worked out from them when the log is read.
/// </summary>
public static class EffortEndpoints
{
    public static RouteGroupBuilder MapEffortEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/races", async (RaceEntry entry, FitnessDbContext database, PosteriorCache cache, IClock clock, CancellationToken cancellationToken) =>
        {
            if (Date(entry.Date) is not { } date) return Fail("The race date is yyyy-mm-dd.");
            if (date > clock.GetCurrentInstant().InUtc().Date.PlusDays(1)) return Fail("The race date cannot be in the future.");
            if (entry.DistanceMeters is < 1000 or > 50_000) return Fail("A race is 1 to 50 km.");
            if (entry.Seconds <= 0 || entry.Seconds > 8 * 3600) return Fail("The time is under 8 hours.");
            if (entry.AltitudeMeters is < -500 or > 5000) return Fail("Altitude is -500 to 5,000 m.");
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 128) return Fail("Name the race, in 128 characters at most.");

            database.RaceResults.Add(new RaceResult
            {
                Id = Guid.NewGuid(),
                Date = date,
                Name = entry.Name.Trim(),
                DistanceMeters = entry.DistanceMeters,
                Seconds = entry.Seconds,
                AltitudeMeters = entry.AltitudeMeters,
                RecordedAt = clock.GetCurrentInstant(),
            });
            await database.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
            return Results.NoContent();
        });

        api.MapDelete("/races/{id:guid}", async (Guid id, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            var race = await database.RaceResults.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (race is null) return Results.NotFound();
            database.RaceResults.Remove(race);
            await database.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
            return Results.NoContent();
        });

        api.MapPost("/places", async (PlaceEntry entry, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            LocalDate? since = null;
            if (!string.IsNullOrWhiteSpace(entry.Since))
            {
                if (Date(entry.Since) is not { } parsed) return Fail("The start date is yyyy-mm-dd, or blank for before everything.");
                since = parsed;
            }

            if (entry.AltitudeMeters is < -500 or > 5000) return Fail("Altitude is -500 to 5,000 m.");
            if (string.IsNullOrWhiteSpace(entry.Place) || entry.Place.Length > 128) return Fail("Name the place, in 128 characters at most.");
            if (await database.TrainingPlaces.AnyAsync(p => p.Since == since, cancellationToken))
            {
                return Fail("A place already starts that day. Remove it first if it is wrong.");
            }

            database.TrainingPlaces.Add(new TrainingPlace
            {
                Id = Guid.NewGuid(),
                Since = since,
                Place = entry.Place.Trim(),
                AltitudeMeters = entry.AltitudeMeters,
            });
            await database.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
            return Results.NoContent();
        });

        api.MapDelete("/places/{id:guid}", async (Guid id, FitnessDbContext database, PosteriorCache cache, CancellationToken cancellationToken) =>
        {
            var place = await database.TrainingPlaces.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (place is null) return Results.NotFound();
            database.TrainingPlaces.Remove(place);
            await database.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
            return Results.NoContent();
        });

        return api;
    }

    private static LocalDate? Date(string? value) =>
        !string.IsNullOrWhiteSpace(value) && LocalDatePattern.Iso.Parse(value.Trim()) is { Success: true } parsed
            ? parsed.Value
            : null;

    private static IResult Fail(string message) =>
        Results.Text(message, "text/plain", statusCode: StatusCodes.Status400BadRequest);
}
