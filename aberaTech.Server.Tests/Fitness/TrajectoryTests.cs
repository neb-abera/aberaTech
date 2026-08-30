using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class TrajectoryTests
{
    private static readonly TrajectoryParameters P = new(StartVdot: 37);

    [Fact]
    public void Starts_at_the_anchor()
    {
        Assert.Equal(37, Trajectory.VdotAt(P, effectiveHours: 5.7, months: 0), precision: 6);
    }

    [Fact]
    public void Approaches_but_never_passes_the_ceiling()
    {
        var ceiling = Trajectory.Ceiling(P, 8.55);
        Assert.Equal(38 + 1.6 * 8.55, ceiling, precision: 6);

        var atTwoYears = Trajectory.VdotAt(P, 8.55, 24);
        var atFiveYears = Trajectory.VdotAt(P, 8.55, 60);
        Assert.True(atTwoYears < atFiveYears);
        Assert.True(atFiveYears < ceiling);
    }

    [Fact]
    public void Committed_dose_matches_the_published_scenario_table()
    {
        // 9.5 h at 90% compliance — the "committed" scenario: ~44.2 at 12
        // months, ~45.2 at 24. These values were computed independently before
        // this class existed; they are the calibration, not its echo.
        var effective = Trajectory.EffectiveHours(9.5, 0.90);
        Assert.InRange(Trajectory.VdotAt(P, effective, 12), 44.7, 45.7);
        Assert.InRange(Trajectory.VdotAt(P, effective, 24), 48.3, 49.3);
    }

    [Fact]
    public void Unreachable_goal_reports_no_date_rather_than_a_wrong_one()
    {
        // 1.5 effective hours gives ceiling 40.4; VDOT 48 never arrives.
        Assert.Null(Trajectory.MonthsToReach(P, 1.5, 48));
    }

    [Fact]
    public void Reachable_goal_dates_are_consistent_with_the_forward_model()
    {
        var months = Trajectory.MonthsToReach(P, 8.55, 48);
        Assert.NotNull(months);
        Assert.Equal(48, Trajectory.VdotAt(P, 8.55, months!.Value), precision: 6);
    }

    [Fact]
    public void Inverse_dose_roundtrips_through_the_forward_model()
    {
        var hours = Trajectory.HoursToReach(P, targetVdot: 45, months: 12);
        Assert.NotNull(hours);
        Assert.Equal(45, Trajectory.VdotAt(P, hours!.Value, 12), precision: 6);
    }

    [Fact]
    public void Impossible_timelines_are_refused_not_flattered()
    {
        // VDOT 51 in three months would take an absurd dose; the model says so.
        Assert.Null(Trajectory.HoursToReach(P, targetVdot: 51, months: 3));
    }

    [Fact]
    public void Already_met_goals_need_no_dose()
    {
        Assert.Equal(0, Trajectory.HoursToReach(P, targetVdot: 36, months: 6));
        Assert.Equal(0, Trajectory.MonthsToReach(P, 5, targetVdot: 37));
    }
}
