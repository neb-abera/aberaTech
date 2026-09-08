using System.Text.Json;
using aberaTech.Fitness.IntervalsIcu;
using Dynastream.Fit;
using Xunit;
using File = Dynastream.Fit.File;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// An intervals.icu activity as the API lists it, read into the log's own
/// shape: from the original FIT file when there is one, from the listing
/// when there is not.
/// </summary>
public sealed class IntervalsIcuMappingTests
{
    private const string Summary = """
        {
          "id": "i84726351",
          "name": "Ruck 45lb, treadmill",
          "type": "Walk",
          "start_date": "2026-09-01T06:00:00Z",
          "start_date_local": "2026-09-01T09:00:00",
          "distance": 8046.7,
          "moving_time": 5400,
          "elapsed_time": 5460,
          "average_heartrate": 141.2,
          "max_heartrate": 158,
          "trainer": true,
          "icu_training_load": 62
        }
        """;

    private static IcuActivity Parse(string json) => JsonSerializer.Deserialize<IcuActivity>(json)!;

    private static byte[] Fit(SubSport subSport, params (double Seconds, double Meters, byte Hr)[] laps)
    {
        var stream = new MemoryStream();
        var encode = new Encode(ProtocolVersion.V20);
        encode.Open(stream);
        var start = new Dynastream.Fit.DateTime(new System.DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc));

        var fileId = new FileIdMesg();
        fileId.SetType(File.Activity);
        fileId.SetManufacturer(Manufacturer.Garmin);
        fileId.SetTimeCreated(start);
        encode.Write(fileId);

        double total = 0, meters = 0;
        foreach (var (seconds, distance, hr) in laps)
        {
            var lap = new LapMesg();
            lap.SetStartTime(start);
            lap.SetTotalTimerTime((float)seconds);
            lap.SetTotalElapsedTime((float)seconds);
            lap.SetTotalDistance((float)distance);
            lap.SetAvgHeartRate(hr);
            encode.Write(lap);
            total += seconds;
            meters += distance;
        }

        var session = new SessionMesg();
        session.SetStartTime(start);
        session.SetSport(Sport.Walking);
        session.SetSubSport(subSport);
        session.SetTotalTimerTime((float)total);
        session.SetTotalElapsedTime((float)total);
        session.SetTotalDistance((float)meters);
        session.SetAvgHeartRate(141);
        encode.Write(session);
        encode.Close();
        return stream.ToArray();
    }

    [Fact]
    public void The_listing_alone_becomes_an_activity_with_the_name_deciding_the_ruck()
    {
        var activity = IntervalsIcuMapping.Map(Parse(Summary), null);

        Assert.NotNull(activity);
        Assert.Equal("intervals-icu", activity!.Source);
        Assert.Equal("intervals:i84726351", activity.ExternalId);
        Assert.Equal("ruck", activity.Sport);
        Assert.Equal(45 / 2.2046226218, activity.LoadKg!.Value, precision: 3);
        Assert.Equal(8046.7, activity.DistanceMeters);
        Assert.Equal(5400, activity.DurationSeconds);
        Assert.Equal(141, activity.AverageHr);
        Assert.Equal(158, activity.MaxHr);
        Assert.True(activity.Indoor);
        Assert.Equal(2026, activity.StartedAt.InUtc().Year);
        Assert.Equal(6, activity.StartedAt.InUtc().Hour);
        Assert.Empty(activity.Laps);
    }

    [Fact]
    public void The_original_file_is_the_witness_and_the_listing_fills_in_the_name()
    {
        var file = Fit(SubSport.Generic, (2700, 4023, 138), (2700, 4023, 144));

        var activity = IntervalsIcuMapping.Map(Parse(Summary), file);

        Assert.NotNull(activity);
        Assert.Equal("intervals-icu", activity!.Source);
        Assert.Equal("intervals:i84726351", activity.ExternalId);
        Assert.Equal("Ruck 45lb, treadmill", activity.Name);
        Assert.Equal("ruck", activity.Sport);
        Assert.Equal(2, activity.Laps.Count);
        Assert.Equal(144, activity.Laps[1].AverageHr);

        // The file's sub-sport said nothing, so the listing's trainer flag stands.
        Assert.True(activity.Indoor);
        Assert.NotNull(activity.LoadKg);
    }

    [Fact]
    public void A_file_that_is_not_a_fit_file_falls_back_to_the_listing()
    {
        var activity = IntervalsIcuMapping.Map(Parse(Summary), "not a fit file"u8.ToArray());

        Assert.NotNull(activity);
        Assert.Empty(activity!.Laps);
        Assert.Equal(5400, activity.DurationSeconds);
    }

    [Fact]
    public void A_gzipped_file_is_inflated_first()
    {
        var raw = Fit(SubSport.Treadmill, (1800, 3000, 150));
        using var packed = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(raw);
        }

        var activity = IntervalsIcuMapping.Map(Parse(Summary), packed.ToArray());

        Assert.NotNull(activity);
        Assert.Single(activity!.Laps);
    }

    [Theory]
    [InlineData("Run", "run")]
    [InlineData("TrailRun", "run")]
    [InlineData("Hike", "ruck")]
    [InlineData("Walk", "ruck")]
    [InlineData("WeightTraining", "strength")]
    [InlineData("Ride", "other")]
    public void Sport_follows_the_type(string type, string expected)
    {
        Assert.Equal(expected, IntervalsIcuMapping.MapSport(type));
    }

    [Fact]
    public void The_zoned_start_wins_and_the_local_one_is_read_as_utc_only_when_alone()
    {
        var zoned = Parse(Summary);
        Assert.Equal(6, IntervalsIcuMapping.Started(zoned)!.Value.InUtc().Hour);

        var local = zoned with { StartDate = null };
        Assert.Equal(9, IntervalsIcuMapping.Started(local)!.Value.InUtc().Hour);

        Assert.Null(IntervalsIcuMapping.Started(zoned with { StartDate = null, StartDateLocal = null }));
    }
}
