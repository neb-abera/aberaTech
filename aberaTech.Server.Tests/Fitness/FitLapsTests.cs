using aberaTech.Fitness.Ingest;
using Dynastream.Fit;
using Xunit;
using File = Dynastream.Fit.File;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A FIT file written with Garmin's own encoder and read back: the session,
/// its laps in order, and the sub-sport that says treadmill.
/// </summary>
public sealed class FitLapsTests
{
    private static MemoryStream Encoded(SubSport subSport, params (double Seconds, double Meters, byte Hr)[] laps)
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

        var total = 0.0;
        var meters = 0.0;
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
        session.SetSport(Sport.Running);
        session.SetSubSport(subSport);
        session.SetTotalTimerTime((float)total);
        session.SetTotalElapsedTime((float)total);
        session.SetTotalDistance((float)meters);
        session.SetAvgHeartRate(150);
        encode.Write(session);

        encode.Close();
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Laps_come_back_in_order_and_the_empty_one_is_dropped()
    {
        using var stream = Encoded(SubSport.Treadmill, (900, 1000, 128), (1800, 4870, 156), (0, 0, 0));

        var activity = FitImport.Parse(stream);

        Assert.NotNull(activity);
        Assert.Equal("run", activity!.Sport);
        Assert.True(activity.Indoor);
        Assert.Equal(2700, activity.DurationSeconds, precision: 1);
        Assert.Equal(2, activity.Laps.Count);
        Assert.Equal(1000, activity.Laps[0].DistanceMeters!.Value, precision: 1);
        Assert.Equal(128, activity.Laps[0].AverageHr);
        Assert.Equal(1800, activity.Laps[1].Seconds, precision: 1);
        Assert.Equal(156, activity.Laps[1].AverageHr);
    }

    [Theory]
    [InlineData(SubSport.Treadmill, true)]
    [InlineData(SubSport.IndoorRunning, true)]
    [InlineData(SubSport.Track, false)]
    [InlineData(SubSport.Trail, false)]
    [InlineData(SubSport.Generic, null)]
    public void The_sub_sport_says_indoors_or_not(SubSport subSport, bool? expected)
    {
        Assert.Equal(expected, FitImport.IsIndoor(subSport));
    }
}
