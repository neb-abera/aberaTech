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
