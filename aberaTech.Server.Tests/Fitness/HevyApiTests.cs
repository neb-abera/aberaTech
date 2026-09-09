using System.Text.Json;
using aberaTech.Fitness.Ingest;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A Hevy workout as the API sends it, read into the log. The daily pull
/// once reported "0 fetched" with no error because every timestamp carried
/// an offset the strict instant parser refused; these pin the fix.
/// </summary>
public sealed class HevyApiTests
{
    private const string Workout = """
        {
          "id": "b459cba5-cd6d-463c-abd6-54f8eafcadcb",
          "title": "Upper Body",
          "start_time": "2026-09-08T17:02:11+00:00",
          "end_time": "2026-09-08T18:05:40+00:00",
          "exercises": [
            { "title": "Bench Press (Barbell)", "sets": [ { "weight_kg": 80, "reps": 5 }, { "weight_kg": 80, "reps": 5 } ] },
            { "title": "Pull Up", "sets": [ { "weight_kg": null, "reps": 12 } ] }
          ]
        }
        """;

    [Fact]
    public void A_workout_with_offset_timestamps_is_read_and_timed()
    {
        var workout = JsonSerializer.Deserialize<HevyApiClient.HevyWorkout>(Workout)!;

        var activity = HevyApiClient.Map(workout);

        Assert.NotNull(activity);
        Assert.Equal("hevy-api", activity!.Source);
        Assert.Equal("b459cba5-cd6d-463c-abd6-54f8eafcadcb", activity.ExternalId);
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 17, 2, 11), activity.StartedAt);
        Assert.Equal(3809, activity.DurationSeconds);
        Assert.Equal(3, activity.Sets.Count);
        Assert.Equal(0, activity.Sets[2].WeightKg);
        Assert.Equal(12, activity.Sets[2].Reps);
    }

    [Theory]
    [InlineData("2026-09-08T17:02:11Z")]
    [InlineData("2026-09-08T17:02:11.000Z")]
    [InlineData("2026-09-08T17:02:11+00:00")]
    [InlineData("2026-09-08T20:02:11+03:00")]
    [InlineData("2026-09-08T17:02:11.123456+00:00")]
    public void Every_iso_shape_reads_as_the_same_instant(string text)
    {
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 17, 2, 11).PlusNanoseconds(text.Contains(".123456") ? 123_456_000 : 0), IsoInstant.Parse(text));
    }

    [Fact]
    public void Garbage_is_null_not_an_exception()
    {
        Assert.Null(IsoInstant.Parse("yesterday"));
        Assert.Null(IsoInstant.Parse(""));
        Assert.Null(IsoInstant.Parse(null));
    }
}
