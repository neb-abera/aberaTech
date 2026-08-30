using System.Net.Http.Json;
using System.Text.Json.Serialization;
using aberaTech.Fitness.Data;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// Hevy's official REST API. Needs a Pro subscription's API key; the CSV
/// upload path covers the free tier, so the key is optional configuration.
/// </summary>
public sealed class HevyApiClient(HttpClient http)
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

            activities.AddRange(response.Workouts.Select(Map).Where(a => a is not null).Select(a => a!));

            if (page >= response.PageCount) break;
        }

        return activities;
    }

    private static Activity? Map(HevyWorkout workout)
    {
        var started = InstantPattern.ExtendedIso.Parse(workout.StartTime);
        if (!started.Success) return null;

        var ended = InstantPattern.ExtendedIso.Parse(workout.EndTime);
        var duration = ended.Success ? (ended.Value - started.Value).TotalSeconds : 0;

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Source = "hevy-api",
            ExternalId = workout.Id,
            StartedAt = started.Value,
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
                    Reps = set.Reps ?? 0
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
        [property: JsonPropertyName("reps")] int? Reps);
}
