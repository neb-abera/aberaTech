using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>Reading the week the athlete is actually on out of the log.</summary>
public sealed class SessionMixTests
{
    private const double Vdot37 = 37;

    private static LoggedSession Run(double km, string pace)
    {
        var parts = pace.Split(':').Select(double.Parse).ToArray();
        return new LoggedSession("run", km * 1000, (parts[0] * 60 + parts[1]) * km);
    }

    [Fact]
    public void Runs_are_placed_by_their_pace_against_the_athletes_own_bands()
    {
        var bands = TrainingPaces.For(Vdot37);
        var easy = bands.Single(b => b.Zone == "E");
        var threshold = bands.Single(b => b.Zone == "T");
        var interval = bands.Single(b => b.Zone == "I");

        Assert.Equal(TrainingZone.Easy, SessionMix.ZoneOf(Pace(easy.SlowSecPerKm), Vdot37));
        Assert.Equal(TrainingZone.Threshold, SessionMix.ZoneOf(Pace(threshold.SlowSecPerKm - 5), Vdot37));
        Assert.Equal(TrainingZone.Interval, SessionMix.ZoneOf(Pace(interval.SlowSecPerKm - 5), Vdot37));

        static LoggedSession Pace(double secPerKm) => new("run", 5000, secPerKm * 5);
    }

    [Fact]
    public void The_same_pace_is_easy_for_a_fitter_athlete_and_hard_for_a_slower_one()
    {
        var session = Run(8, "5:00");

        Assert.Equal(TrainingZone.Interval, SessionMix.ZoneOf(session, vdot: 30));
        Assert.Equal(TrainingZone.Easy, SessionMix.ZoneOf(session, vdot: 60));
    }

    [Fact]
    public void Rucking_is_aerobic_volume_and_lifting_is_its_own_zone()
    {
        Assert.Equal(TrainingZone.Easy, SessionMix.ZoneOf(new LoggedSession("ruck", 8000, 4800), Vdot37));
        Assert.Equal(TrainingZone.Strength, SessionMix.ZoneOf(new LoggedSession("strength", null, 3600), Vdot37));
    }

    [Fact]
    public void A_month_of_sessions_becomes_an_average_week()
    {
        LoggedSession[] sessions =
        [
            Run(10, "7:00"), Run(10, "7:00"), Run(8, "7:00"), Run(8, "7:00"),
            new("strength", null, 3600), new("strength", null, 3600)
        ];

        var week = SessionMix.WeeklyDose(sessions, weeks: 2, vdot: Vdot37);

        // Thirty-six kilometres at 7:00 is 4.2 hours over two weeks.
        Assert.Equal(2.1, week.EasyHours, precision: 6);
        Assert.Equal(1, week.StrengthHours, precision: 6);
        Assert.Equal(2.1, week.RunningHours, precision: 6);
    }

    [Fact]
    public void A_run_with_no_distance_counts_as_volume_rather_than_being_dropped()
    {
        var week = SessionMix.WeeklyDose([new LoggedSession("run", null, 3600)], weeks: 1, vdot: Vdot37);
        Assert.Equal(1, week.EasyHours, precision: 6);
    }
}

/// <summary>The derivative that turns a pace improvement into a fitness one.</summary>
public sealed class SpeedElasticityTests
{
    [Fact]
    public void Vdot_reacts_more_than_proportionally_to_speed()
    {
        // The oxygen-cost curve is quadratic in velocity, so the elasticity is
        // above one — assuming one understates every measured improvement.
        var elasticity = Vdot.SpeedElasticity(5000, 25);
        Assert.InRange(elasticity, 1.0, 1.4);
    }

    [Fact]
    public void The_elasticity_reproduces_the_scoring_it_was_taken_from()
    {
        // A 2% speed improvement, scored directly and scored through the
        // elasticity, must agree to within a fraction of a VDOT point.
        const double distance = 5000, minutes = 25;
        var slow = Vdot.FromRace(distance, minutes);
        var fast = Vdot.FromRace(distance, minutes / 1.02);

        var approximated = slow * Math.Pow(1.02, Vdot.SpeedElasticity(distance, minutes));
        Assert.Equal(fast, approximated, precision: 1);
    }
}

/// <summary>What laps and a heart rate add to the reading of a session.</summary>
public sealed class SessionMixLapTests
{
    private const double Vdot37 = 37;
    private static readonly HeartRateBands Bands = new(AetHr: 152, LtHr: 168);

    private static LoggedLap Lap(double secPerKm, double seconds, int? hr = null) =>
        new(seconds / secPerKm * 1000, seconds, hr);

    [Fact]
    public void An_interval_workout_is_split_by_its_laps_rather_than_averaged_into_easy()
    {
        var bands = TrainingPaces.For(Vdot37);
        var interval = bands.Single(b => b.Zone == "I").SlowSecPerKm - 5;
        var easy = bands.Single(b => b.Zone == "E").SlowSecPerKm;

        // 10 min warm-up, 5 × (3 min hard / 3 min jog), 10 min cool-down: 50 min.
        var laps = new List<LoggedLap> { Lap(easy, 600) };
        for (var i = 0; i < 5; i++)
        {
            laps.Add(Lap(interval, 180));
            laps.Add(Lap(easy, 180));
        }
        laps.Add(Lap(easy, 600));

        var session = new LoggedSession("run", laps.Sum(l => l.DistanceMeters), 3000, laps);

        // Averaged, the whole thing is an easy run.
        Assert.Equal(TrainingZone.Easy, SessionMix.ZoneOf(session, Vdot37));

        var split = SessionMix.Split(session, Vdot37, null);
        Assert.Equal(0.25, split.IntervalHours, precision: 6);
        Assert.Equal(35.0 / 60, split.EasyHours, precision: 6);
    }

    [Fact]
    public void Laps_that_cover_too_little_of_the_session_are_not_trusted_to_describe_it()
    {
        // One lap pressed at the end of an hour.
        var session = new LoggedSession("run", 9000, 3600, [Lap(300, 120), Lap(300, 60)]);

        Assert.False(SessionMix.LapsDescribe(session));
        Assert.Equal(1, SessionMix.Split(session, Vdot37, null).EasyHours, precision: 6);
    }

    [Fact]
    public void Time_the_laps_did_not_cover_is_counted_as_easy()
    {
        var session = new LoggedSession("run", 10000, 3600, [Lap(400, 1500), Lap(400, 1500)]);

        Assert.Equal(1, SessionMix.Split(session, Vdot37, null).EasyHours, precision: 6);
    }

    [Fact]
    public void A_treadmill_session_is_placed_by_heart_rate_because_the_belt_cannot_be_trusted_on_pace()
    {
        // A fast belt reading at an easy heart rate.
        var fast = TrainingPaces.For(Vdot37).Single(b => b.Zone == "I").SlowSecPerKm - 5;
        var session = new LoggedSession("run", 3600 / fast * 1000, 3600, Indoor: true, AverageHr: 148);

        Assert.Equal(TrainingZone.Interval, SessionMix.ZoneOf(session, Vdot37));
        Assert.Equal(TrainingZone.Easy, SessionMix.ZoneOf(session, Vdot37, Bands));
        Assert.Equal(TrainingZone.Threshold, SessionMix.ZoneOf(session with { AverageHr = 165 }, Vdot37, Bands));
        Assert.Equal(TrainingZone.Interval, SessionMix.ZoneOf(session with { AverageHr = 175 }, Vdot37, Bands));
    }

    [Fact]
    public void Treadmill_laps_are_placed_by_heart_rate_and_outdoor_laps_by_pace()
    {
        var easy = TrainingPaces.For(Vdot37).Single(b => b.Zone == "E").SlowSecPerKm;
        var laps = new[] { Lap(easy, 900, 150), Lap(easy, 900, 172) };
        var indoors = new LoggedSession("run", laps.Sum(l => l.DistanceMeters), 1800, laps, Indoor: true, AverageHr: 161);
        var outdoors = indoors with { Indoor = false };

        var byHeart = SessionMix.Split(indoors, Vdot37, Bands);
        Assert.Equal(0.25, byHeart.EasyHours, precision: 6);
        Assert.Equal(0.25, byHeart.IntervalHours, precision: 6);

        var byPace = SessionMix.Split(outdoors, Vdot37, Bands);
        Assert.Equal(0.5, byPace.EasyHours, precision: 6);
    }

    [Fact]
    public void The_heart_rate_bands_meet_the_pace_bands_at_the_midpoint_of_the_thresholds()
    {
        Assert.Equal(160, Bands.EasyCeiling);
        Assert.Equal(168, Bands.ThresholdCeiling);

        // Without a measured LT, it is assumed 10% above the AeT.
        var assumed = new HeartRateBands(152, null);
        Assert.Equal(167, assumed.ThresholdCeiling);
    }

    [Fact]
    public void A_steady_run_is_one_below_the_lactate_threshold_with_even_laps()
    {
        var even = new LoggedSession("run", 10000, 4000, [Lap(400, 1000, 150), Lap(405, 1000, 151), Lap(398, 1000, 152), Lap(402, 1000, 153)], AverageHr: 151);
        Assert.True(SessionMix.IsSteady(even, Bands));

        Assert.False(SessionMix.IsSteady(even with { AverageHr = 172 }, Bands));

        var repeats = new LoggedSession("run", 10000, 4000, [Lap(280, 300, 170), Lap(520, 300, 135), Lap(280, 300, 172), Lap(520, 300, 138)], AverageHr: 152);
        Assert.False(SessionMix.IsSteady(repeats, Bands));

        Assert.False(SessionMix.IsSteady(new LoggedSession("run", 10000, 4000), Bands));
        Assert.True(SessionMix.IsSteady(new LoggedSession("run", 10000, 4000, AverageHr: 150), Bands));
    }

    [Fact]
    public void The_explanation_says_how_many_sessions_were_split_by_lap()
    {
        var steps = SessionMix.Explain(new TrainingDose(EasyHours: 3), weeks: 8, sessions: 20, lapSplit: 12);
        Assert.Contains("12 of them split lap by lap", steps[0].Expression);
    }
}
