using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Two files from the same service describing one run.
///
/// Garmin's export carries a real UTC clock; the Connect website's activities
/// CSV carries a wall clock and no zone. This athlete's own export flips from
/// −6 to +3 partway through 2026, so no single zone would reconcile them — the
/// comparison has to be of what the session was, not of when a file says it
/// began.
/// </summary>
public sealed class SameSessionTests
{
    private static readonly Instant Noon = Instant.FromUtc(2026, 8, 27, 12, 0, 0);

    private static Activity Run(Instant started, double duration = 1200, double? distance = 3020) =>
        new()
        {
            Id = Guid.NewGuid(),
            Source = "garmin-export",
            Sport = "run",
            StartedAt = started,
            DurationSeconds = duration,
            DistanceMeters = distance
        };

    [Fact]
    public void The_same_run_read_from_both_files_is_one_session()
    {
        // The CSV's wall clock, read as UTC, sits three hours from the truth.
        var export = Run(Noon);
        var csv = Run(Noon.Plus(Duration.FromHours(3)), duration: 1200.4, distance: 3020);

        Assert.True(SameSession.Matches(export, csv));
    }

    [Fact]
    public void A_six_hour_offset_is_forgiven_too_because_he_used_to_live_at_one()
    {
        var export = Run(Noon);
        var csv = Run(Noon.Minus(Duration.FromHours(6)));

        Assert.True(SameSession.Matches(export, csv));
    }

    [Fact]
    public void Yesterdays_identical_treadmill_run_is_a_different_session()
    {
        // The whole risk of matching on shape rather than time: someone who
        // runs 20:00 on a treadmill every morning. A day apart is past any
        // real offset, so the window is what separates them.
        var today = Run(Noon);
        var yesterday = Run(Noon.Minus(Duration.FromHours(24)));

        Assert.False(SameSession.Matches(today, yesterday));
    }

    [Fact]
    public void Same_clock_but_a_different_effort_is_a_different_session()
    {
        Assert.False(SameSession.Matches(Run(Noon), Run(Noon, duration: 1800)));
        Assert.False(SameSession.Matches(Run(Noon), Run(Noon, distance: 5000)));
    }

    [Fact]
    public void A_run_and_a_lift_are_never_the_same_session()
    {
        var lift = Run(Noon);
        lift.Sport = "strength";

        Assert.False(SameSession.Matches(Run(Noon), lift));
    }

    [Fact]
    public void One_knowing_the_distance_and_the_other_not_is_too_little_in_common()
    {
        Assert.False(SameSession.Matches(Run(Noon), Run(Noon, distance: null)));
        Assert.True(SameSession.Matches(Run(Noon, distance: null), Run(Noon, distance: null)));
    }
}

/// <summary>
/// An athlete who has moved. The lifetime best and the anchor race were run at
/// one altitude and the goals will be run at another, so one number cannot
/// score both — the peak would be credited with thin air it never faced, or
/// the goals discounted for air that is no longer there.
/// </summary>
public sealed class PastAltitudeTests
{
    /// <summary>The 2019 five-mile best, set at El Paso's 1,190 m.</summary>
    private static AthleteSettings Peak(double home, double? past) => new()
    {
        Id = 1,
        PastPeakDistanceMeters = 5 * 1609.344,
        PastPeakSeconds = 34.5 * 60,
        PastPeakYear = 2019,
        BirthYear = 1993,
        HomeAltitudeMeters = home,
        PastAltitudeMeters = past
    };

    [Fact]
    public void Scores_the_lifetime_best_where_it_was_actually_run()
    {
        // Moved to sea level; the best was set at 1,190 m and is worth more
        // than the same clock time at sea level would be.
        var moved = FitnessReports.ReclaimVdotFrom(Peak(home: 0, past: 1190), currentYear: 2026);
        var neverMoved = FitnessReports.ReclaimVdotFrom(Peak(home: 0, past: null), currentYear: 2026);

        Assert.NotNull(moved);
        Assert.NotNull(neverMoved);
        Assert.True(moved > neverMoved, "thin air at the time makes the peak worth more, not less");
    }

    [Fact]
    public void Leaving_it_unset_reproduces_exactly_what_the_one_number_did()
    {
        // Every athlete who has not moved must see no change at all.
        var before = FitnessReports.ReclaimVdotFrom(Peak(home: 1190, past: null), currentYear: 2026);
        var after = FitnessReports.ReclaimVdotFrom(Peak(home: 1190, past: 1190), currentYear: 2026);

        Assert.Equal(before!.Value, after!.Value, precision: 10);
    }

    [Fact]
    public void Where_you_are_now_does_not_move_a_peak_set_somewhere_else()
    {
        var atSeaLevel = FitnessReports.ReclaimVdotFrom(Peak(home: 0, past: 1190), currentYear: 2026);
        var atAltitude = FitnessReports.ReclaimVdotFrom(Peak(home: 2400, past: 1190), currentYear: 2026);

        Assert.Equal(atSeaLevel!.Value, atAltitude!.Value, precision: 10);
    }
}
