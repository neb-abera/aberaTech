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

    [Fact]
    public void The_recording_is_split_at_its_midpoint_into_two_halves()
    {
        // An hour, one record a second: 140 bpm and 3.0 m/s for the first
        // half, 146 bpm and 2.9 m/s for the second.
        var start = new System.DateTime(2026, 11, 16, 6, 0, 0, DateTimeKind.Utc);
        var records = new List<RecordMesg>();
        var meters = 0.0;
        for (var second = 0; second <= 3600; second++)
        {
            var first = second < 1800;
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(start.AddSeconds(second)));
            record.SetHeartRate((byte)(first ? 140 : 146));
            record.SetDistance((float)meters);
            records.Add(record);
            meters += first ? 3.0 : 2.9;
        }

        var activity = new aberaTech.Fitness.Data.Activity { Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "" };
        FitImport.Halves(records, activity);

        Assert.Equal(140, activity.FirstHalfHr!.Value, precision: 1);
        Assert.Equal(146, activity.SecondHalfHr!.Value, precision: 1);
        Assert.Equal(3.0, activity.FirstHalfMetersPerSecond!.Value, precision: 2);
        Assert.Equal(2.9, activity.SecondHalfMetersPerSecond!.Value, precision: 2);
    }

    /// <summary>One record a second from <paramref name="from"/>, at the heart rate the function gives.</summary>
    private static void Records(List<RecordMesg> records, System.DateTime start, int from, int to, Func<int, double> hr, ref double meters)
    {
        for (var second = from; second <= to; second++)
        {
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(start.AddSeconds(second)));
            record.SetHeartRate((byte)Math.Round(hr(second)));
            record.SetDistance((float)meters);
            records.Add(record);
            meters += 2.0;
        }
    }

    [Fact]
    public void The_halves_leave_out_the_climb_after_each_start()
    {
        // Neb's drift test of 2026-11-23 in outline. The heart rate climbs
        // from 110 to 158 over four minutes, holds 158 to minute 36 and 161
        // to minute 60, when the treadmill stops. The watch is paused for a
        // minute. The heart rate then climbs from 148 to 158 over 90 seconds
        // and holds 161 to minute 70.
        var start = new System.DateTime(2026, 11, 23, 11, 20, 0, DateTimeKind.Utc);
        var records = new List<RecordMesg>();
        var meters = 0.0;
        Records(records, start, 0, 3600, s => s < 240 ? 110 + 48.0 * s / 240 : s < 2170 ? 158 : 161, ref meters);
        Records(records, start, 3660, 4260, s => s < 3750 ? 148 + 10.0 * (s - 3660) / 90 : 161, ref meters);

        var activity = new aberaTech.Fitness.Data.Activity { Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "", Indoor = true };
        FitImport.Halves(records, activity);

        // Read whole, the first half holds both climbs' worth of low heart
        // rate: about 155 bpm. Read settled, it is the 158 he held.
        Assert.InRange(activity.FirstHalfHr!.Value, 157.9, 158.1);
        Assert.InRange(activity.SecondHalfHr!.Value, 160.9, 161.0);

        // 71 minutes recorded, about 5.5 of them climbing: about 64 settled.
        Assert.InRange(activity.HalvesSeconds!.Value, 63 * 60, 65.5 * 60);
        Assert.Equal(2.0, activity.FirstHalfMetersPerSecond!.Value, precision: 2);
        Assert.Equal(2.0, activity.SecondHalfMetersPerSecond!.Value, precision: 2);
    }

    [Fact]
    public void A_recording_that_starts_settled_is_read_whole()
    {
        var start = new System.DateTime(2026, 11, 16, 6, 0, 0, DateTimeKind.Utc);
        var records = new List<RecordMesg>();
        var meters = 0.0;
        Records(records, start, 0, 3600, s => s < 1800 ? 140 : 146, ref meters);

        var activity = new aberaTech.Fitness.Data.Activity { Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "" };
        FitImport.Halves(records, activity);

        Assert.Equal(3600, activity.HalvesSeconds!.Value, precision: 0);
        Assert.Equal(140, activity.FirstHalfHr!.Value, precision: 1);
    }

    [Fact]
    public void A_run_too_short_to_settle_keeps_the_whole_recording()
    {
        // Six minutes, climbing all the way: no stretch is settled, so the
        // halves are the recording's, as they were before.
        var start = new System.DateTime(2026, 11, 16, 6, 0, 0, DateTimeKind.Utc);
        var records = new List<RecordMesg>();
        var meters = 0.0;
        Records(records, start, 0, 360, s => 100 + s / 6.0, ref meters);

        var activity = new aberaTech.Fitness.Data.Activity { Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "" };
        FitImport.Halves(records, activity);

        Assert.Equal(360, activity.HalvesSeconds!.Value, precision: 0);
        Assert.True(activity.FirstHalfHr < activity.SecondHalfHr);
    }

    [Fact]
    public void The_fastest_stretch_of_each_goal_distance_is_found_inside_the_run()
    {
        // 6 miles: easy at 4 m/s (6:42/mi), with a hard 2 miles at 5 m/s
        // (5:22/mi) in the middle. The fastest 2 miles is the hard block.
        var points = new List<(double Seconds, double Meters)>();
        double t = 0, m = 0;
        void Run(double meters, double speed)
        {
            for (var step = 0; step < meters; step += 10)
            {
                t += 10 / speed;
                m += 10;
                points.Add((t, m));
            }
        }
        points.Add((0, 0));
        Run(2 * FitImport.Mile, 4);
        Run(2 * FitImport.Mile, 5);
        Run(2 * FitImport.Mile, 4);

        var activity = new aberaTech.Fitness.Data.Activity { Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "" };
        FitImport.BestEfforts(points, activity);

        Assert.Equal(2 * FitImport.Mile / 5, activity.Best2MileSeconds!.Value, 0);
        Assert.True(activity.Best1_5MileSeconds < activity.Best2MileSeconds);
        // The best 5 miles takes the hard 2 and 3 easy miles around it.
        Assert.InRange(activity.Best5MileSeconds!.Value, 2 * FitImport.Mile / 5 + 3 * FitImport.Mile / 4 - 5, 2 * FitImport.Mile / 5 + 3 * FitImport.Mile / 4 + 5);
        Assert.Null(activity.Best10MileSeconds);
    }
}

