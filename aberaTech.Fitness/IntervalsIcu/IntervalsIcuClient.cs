using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace aberaTech.Fitness.IntervalsIcu;

/// <summary>An activity as intervals.icu lists it. Only the fields this library reads.</summary>
public sealed record IcuActivity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("start_date")] string? StartDate,
    [property: JsonPropertyName("start_date_local")] string? StartDateLocal,
    [property: JsonPropertyName("distance")] double? DistanceMeters,
    [property: JsonPropertyName("moving_time")] double? MovingTimeSeconds,
    [property: JsonPropertyName("elapsed_time")] double? ElapsedTimeSeconds,
    [property: JsonPropertyName("average_heartrate")] double? AverageHeartRate,
    [property: JsonPropertyName("max_heartrate")] double? MaxHeartRate,
    [property: JsonPropertyName("trainer")] bool? Trainer);

/// <summary>
/// intervals.icu's API, as far as this library uses it: one listing and one
/// file download. Raw HTTP, for the same reason the Hevy and Google calls
/// are: two requests do not need a client library and its dependency graph.
/// </summary>
/// <remarks>
/// Authentication is basic auth with the literal username API_KEY and the
/// key as the password, set on the client at registration. The personal
/// limit is 5,000 requests a day and 2,500 per fifteen minutes, which an
/// hourly listing plus one download per new activity never approaches.
/// </remarks>
public sealed class IntervalsIcuClient(HttpClient http, IntervalsIcuOptions options)
{
    public const string BaseAddress = "https://intervals.icu/api/v1/";

    /// <summary>The basic-auth username intervals.icu expects with an API key.</summary>
    public const string ApiKeyUser = "API_KEY";

    /// <summary>Activities that started between two dates, newest first.</summary>
    public async Task<IReadOnlyList<IcuActivity>> ActivitiesAsync(LocalDate oldest, LocalDate newest, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"athlete/{Uri.EscapeDataString(options.AthleteId)}/activities?oldest={oldest:yyyy-MM-dd}&newest={newest:yyyy-MM-dd}");

        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<IcuActivity>>(cancellationToken) ?? [];
    }

    /// <summary>
    /// The activity's original file as the device recorded it, or null when
    /// intervals.icu has none for it — an activity that arrived via Strava, or
    /// one typed in by hand.
    /// </summary>
    public Task<byte[]?> OriginalFileAsync(string activityId, CancellationToken cancellationToken) =>
        FileAsync($"activity/{Uri.EscapeDataString(activityId)}/file", cancellationToken);

    /// <summary>The FIT file intervals.icu generates from its own streams, or null when it cannot.</summary>
    public Task<byte[]?> GeneratedFitAsync(string activityId, CancellationToken cancellationToken) =>
        FileAsync($"activity/{Uri.EscapeDataString(activityId)}/fit-file", cancellationToken);

    private async Task<byte[]?> FileAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.Length == 0 ? null : bytes;
    }
}
