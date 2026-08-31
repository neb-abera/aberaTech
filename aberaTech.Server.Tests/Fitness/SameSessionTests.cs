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
