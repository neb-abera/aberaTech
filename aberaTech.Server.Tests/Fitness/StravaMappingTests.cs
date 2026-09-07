using System.Text.Json;
using aberaTech.Fitness.Strava;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A Strava activity as the API sends it — metres, seconds, an ISO clock,
/// and a trainer flag — read into the log's own shape, laps and all.
/// </summary>
public sealed class StravaMappingTests
{
    private const string Summary = """
        {
          "id": 15208131442,
          "name": "Run (MAF intensity)",
          "sport_type": "Run",
          "type": "Run",
          "start_date": "2026-08-31T04:12:07Z",
          "distance": 5870.3,
          "moving_time": 2725,
          "elapsed_time": 2731,
          "average_heartrate": 153.4,
          "max_heartrate": 165.0,
          "trainer": true,
          "manual": false
        }
        """;

    private const string Laps = """
        [
          {"lap_index": 2, "distance": 4870.3, "moving_time": 1825, "elapsed_time": 1830, "average_heartrate": 156.2},
          {"lap_index": 1, "distance": 1000.0, "moving_time": 900, "elapsed_time": 901, "average_heartrate": 128.0},
          {"lap_index": 3, "distance": 0, "moving_time": 0, "elapsed_time": 0, "average_heartrate": null}
        ]
        """;

    [Fact]
    public void Reads_the_summary_the_laps_and_the_treadmill_flag()
    {
        var summary = JsonSerializer.Deserialize<StravaActivity>(Summary)!;
        var laps = JsonSerializer.Deserialize<List<StravaLap>>(Laps)!;

        var activity = StravaMapping.Map(summary, laps);

        Assert.NotNull(activity);
        Assert.Equal("strava", activity!.Source);
        Assert.Equal("strava:15208131442", activity.ExternalId);
        Assert.Equal("run", activity.Sport);
        Assert.Equal(5870.3, activity.DistanceMeters);
        Assert.Equal(2725, activity.DurationSeconds);
        Assert.Equal(153, activity.AverageHr);
        Assert.Equal(165, activity.MaxHr);
        Assert.True(activity.Indoor);
        Assert.Equal(2026, activity.StartedAt.InUtc().Year);

        // Ordered by Strava's index, the empty lap dropped.
        Assert.Equal(2, activity.Laps.Count);
        Assert.Equal(1000.0, activity.Laps[0].DistanceMeters);
        Assert.Equal(128, activity.Laps[0].AverageHr);
        Assert.Equal(1825, activity.Laps[1].Seconds);
        Assert.Equal([0, 1], activity.Laps.Select(l => l.Index));
    }

    [Theory]
    [InlineData("Run", "Morning Run", "run")]
    [InlineData("TrailRun", "Trail", "run")]
    [InlineData("Hike", "Ruck 45lb", "ruck")]
    [InlineData("Walk", "Evening Walk", "ruck")]
    [InlineData("Run", "Ruck shuffle", "ruck")]
    [InlineData("WeightTraining", "Lift", "strength")]
    [InlineData("Ride", "Ride", "other")]
    public void Sport_follows_the_type_and_the_name(string sportType, string name, string expected)
    {
        Assert.Equal(expected, StravaMapping.MapSport(sportType, name));
    }

    [Fact]
    public void A_ruck_named_with_its_load_carries_it()
    {
        var summary = JsonSerializer.Deserialize<StravaActivity>(Summary)! with { SportType = "Hike", Name = "Ruck 45lb", Trainer = false };

        var activity = StravaMapping.Map(summary, [])!;

        Assert.Equal("ruck", activity.Sport);
        Assert.Equal(45 / 2.2046226218, activity.LoadKg!.Value, precision: 3);
        Assert.False(activity.Indoor);
    }

    [Fact]
    public void The_authorize_redirect_carries_the_scope_and_state()
    {
        var url = StravaClient.AuthorizeRedirect("12345", "https://abera.tech/api/fitness/ingest/strava/callback", "abc.def");

        Assert.StartsWith(StravaClient.AuthorizeUrl + "?", url);
        Assert.Contains("client_id=12345", url);
        Assert.Contains("scope=activity%3Aread_all", url);
        Assert.Contains("state=abc.def", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Fabera.tech%2Fapi%2Ffitness%2Fingest%2Fstrava%2Fcallback", url);
    }
}
