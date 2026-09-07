using System.Text;
using System.Text.Json;
using aberaTech.Fitness.Data;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// The owner's saved documents: what the training guide and the course
/// planner keep between visits.
/// </summary>
/// <remarks>
/// One document per key, the key from a short allowlist, the body an opaque
/// JSON object. The route group these are mapped into carries the owner
/// policy, so an anonymous visitor gets a 401 before any of this runs: they
/// can neither read the owner's progress nor write anything into the
/// database. The size cap is the other half of that: even the owner cannot
/// grow a row past a quarter of a megabyte.
/// </remarks>
public static class ProgressEndpoints
{
    /// <summary>The documents that exist. Anything else is a 404, not a new row.</summary>
    public static readonly IReadOnlyList<string> Keys = ["rf-training", "planner"];

    public const int MaxBytes = 256 * 1024;

    public static bool IsKnownKey(string? key) =>
        key is not null && Keys.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// A document must be a JSON object, not an array, a string or garbage: the
    /// pages read back objects, and a row holding anything else would be a
    /// page that cannot load.
    /// </summary>
    public static bool IsJsonObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder progress)
    {
        progress.MapGet("/{key}", async (string key, FitnessDbContext database, CancellationToken cancellationToken) =>
        {
            if (!IsKnownKey(key)) return Results.NotFound();

            var row = await database.Documents.FindAsync([key], cancellationToken);
            return row is null
                ? Results.NotFound()
                : Results.Content(row.Json, "application/json", Encoding.UTF8);
        });

        progress.MapPut("/{key}", async (
            string key,
            HttpRequest request,
            FitnessDbContext database,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!IsKnownKey(key)) return Results.NotFound();
            if (request.ContentLength is > MaxBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            // Read at most the cap plus one byte: a body that lies about its
            // length, or sends none, still cannot exceed it.
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var buffer = new char[MaxBytes + 1];
            var read = await reader.ReadBlockAsync(buffer, cancellationToken);
            if (read > MaxBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var json = new string(buffer, 0, read);
            if (!IsJsonObject(json)) return Results.BadRequest("A document is a JSON object.");

            var row = await database.Documents.FindAsync([key], cancellationToken);
            if (row is null)
            {
                database.Documents.Add(new OwnerDocument { Key = key, Json = json, UpdatedAt = clock.GetCurrentInstant() });
            }
            else
            {
                row.Json = json;
                row.UpdatedAt = clock.GetCurrentInstant();
            }

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return progress;
    }
}
