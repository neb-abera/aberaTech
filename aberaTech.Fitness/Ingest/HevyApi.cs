using System.Net.Http.Json;
using System.Text.Json.Serialization;
using aberaTech.Fitness.Data;
using NodaTime;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// Hevy's official REST API. Needs a Pro subscription's API key; the CSV
/// upload path covers the free tier, so the key is optional configuration.
/// </summary>
public sealed class HevyApiClient(HttpClient http, ILogger<HevyApiClient>? logger = null)
{
    public const string BaseAddress = "https://api.hevyapp.com/";

    /// <summary>All workouts, newest first, walking the pages.</summary>
    public async Task<IReadOnlyList<Activity>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var activities = new List<Activity>();

        for (var page = 1; ; page++)
        {
            var response = await http.GetFromJsonAsync<WorkoutsPage>(
                $"v1/workouts?page={page}&pageSize=10", cancellationToken);

            if (response?.Workouts is null || response.Workouts.Count == 0) break;

            var mapped = response.Workouts.Select(Map).Where(a => a is not null).Select(a => a!).ToList();
            activities.AddRange(mapped);

            // A page that lists workouts none of which can be read is the one
            // failure that looks like an empty account; say what was refused.
            if (mapped.Count < response.Workouts.Count)
            {
                logger?.LogWarning(
                    "Hevy page {Page}: {Listed} workouts listed, {Mapped} readable; first start_time was '{StartTime}'.",
                    page, response.Workouts.Count, mapped.Count, response.Workouts[0].StartTime);
            }

            if (page >= response.PageCount) break;
        }

        return activities;
    }

    internal static Activity? Map(HevyWorkout workout)
    {
        if (IsoInstant.Parse(workout.StartTime) is not { } started) return null;

        var duration = IsoInstant.Parse(workout.EndTime) is { } ended ? (ended - started).TotalSeconds : 0;

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Source = "hevy-api",
            ExternalId = workout.Id,
            StartedAt = started,
            Sport = "strength",
            Name = workout.Title ?? "",
            DurationSeconds = Math.Max(0, duration)
        };

        var index = 0;
        foreach (var exercise in workout.Exercises ?? [])
        {
            foreach (var set in exercise.Sets ?? [])
            {
                activity.Sets.Add(new StrengthSet
                {
                    Id = Guid.NewGuid(),
                    ActivityId = activity.Id,
                    Exercise = exercise.Title ?? "unknown",
                    SetIndex = index++,
                    WeightKg = set.WeightKg ?? 0,
                    Reps = set.Reps ?? 0,
                    DurationSeconds = set.DurationSeconds is > 0 ? set.DurationSeconds : null,
                    DistanceMeters = set.DistanceMeters is > 0 ? set.DistanceMeters : null
                });
            }
        }

        return activity;
    }

    internal sealed record WorkoutsPage(
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("page_count")] int PageCount,
        [property: JsonPropertyName("workouts")] List<HevyWorkout>? Workouts);

    internal sealed record HevyWorkout(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("start_time")] string StartTime,
        [property: JsonPropertyName("end_time")] string EndTime,
        [property: JsonPropertyName("exercises")] List<HevyExercise>? Exercises);

    internal sealed record HevyExercise(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("sets")] List<HevySet>? Sets);

    internal sealed record HevySet(
        [property: JsonPropertyName("weight_kg")] double? WeightKg,
        [property: JsonPropertyName("reps")] int? Reps,
        [property: JsonPropertyName("duration_seconds")] double? DurationSeconds = null,
        [property: JsonPropertyName("distance_meters")] double? DistanceMeters = null);
}
