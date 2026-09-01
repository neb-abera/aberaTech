using System.Text.Json;
using aberaTech.Fitness.Data;
using NodaTime;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// The activity summaries inside Garmin's "Export Your Data" archive —
/// <c>DI_CONNECT/DI-Connect-Fitness/*_summarizedActivities.json</c>.
///
/// This is the file Garmin actually sends when you ask for your data, and it
/// is the complete activity list for the account rather than the page-at-a-time
/// export the Connect website offers. Its units are its own: distances in
/// centimetres, durations in milliseconds, and two epoch-millisecond clocks —
/// <c>startTimeGmt</c> is real UTC, <c>startTimeLocal</c> is the same instant
/// pre-shifted to the watch's wall clock. Only the GMT one is used, so an
/// activity keeps one identity wherever it is read.
/// </summary>
public static class GarminExportJson
{
    /// <summary>True when a file's text is plausibly this export rather than some other JSON.</summary>
    public static bool Looks(string json) =>
        json.Contains("summarizedActivitiesExport", StringComparison.Ordinal);

    public static IReadOnlyList<Activity> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        var activities = new List<Activity>();
        foreach (var element in Summaries(document.RootElement))
        {
            var activity = ReadOne(element);
            if (activity is not null) activities.Add(activity);
        }

        return activities;
    }

    /// <summary>
    /// Garmin wraps the list in a single-element array of objects carrying a
    /// `summarizedActivitiesExport` property. Later exports may split it over
    /// several such objects, and a bare array is accepted too, so the shape is
    /// walked rather than assumed.
    /// </summary>
    private static IEnumerable<JsonElement> Summaries(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("summarizedActivitiesExport", out var inner)
                && inner.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in inner.EnumerateArray()) yield return item;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Array) yield break;

        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("summarizedActivitiesExport", out var inner)
                && inner.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in inner.EnumerateArray()) yield return item;
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                // A bare array of activities, which some exports produce.
                yield return element;
            }
        }
    }

    private static Activity? ReadOne(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        var startMillis = Number(element, "startTimeGmt");
        var durationMillis = Number(element, "duration");
        if (startMillis is null or <= 0 || durationMillis is null or <= 0) return null;

        var started = Instant.FromUnixTimeMilliseconds((long)startMillis.Value);
        var id = Number(element, "activityId");

        return new Activity
        {
            Id = Guid.NewGuid(),
            Source = "garmin-export",
            // Garmin's own activity id, so re-importing this archive — or a
            // later one that overlaps it — updates rather than duplicates.
            ExternalId = id is null
                ? $"garmin:{started.ToUnixTimeSeconds()}"
                : $"garmin:{(long)id.Value}",
            StartedAt = started,
            Sport = GarminActivitiesCsv.MapSport(Text(element, "activityType")),
            Name = Text(element, "name"),
            DistanceMeters = Number(element, "distance") is { } cm and > 0 ? cm / 100.0 : null,
            DurationSeconds = durationMillis.Value / 1000.0,
            AverageHr = Hr(element, "avgHr"),
            MaxHr = Hr(element, "maxHr")
        };
    }

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static int? Hr(JsonElement element, string name) =>
        Number(element, name) is { } hr and > 0 ? (int)Math.Round(hr) : null;

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
