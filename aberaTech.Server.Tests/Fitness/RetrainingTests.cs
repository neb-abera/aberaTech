using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class RetrainingTests
{
    [Fact]
    public void A_twenties_peak_is_intact_in_the_early_thirties()
    {
        // WMA open-class factors run flat through 34: what was held at 24
        // is still fully reclaimable at 33.
        Assert.Equal(48.7, Retraining.AgeAdjustedPeak(48.7, ageAtPeak: 24, ageNow: 33), precision: 6);
    }

    [Fact]
    public void Decline_starts_after_the_plateau_and_compounds()
    {
        var at45 = Retraining.AgeAdjustedPeak(50, ageAtPeak: 24, ageNow: 45);

        // Eleven years past the plateau at ~0.7%/yr: about 7.4% down.
        Assert.InRange(at45, 50 * 0.90, 50 * 0.94);
        Assert.True(at45 < Retraining.AgeAdjustedPeak(50, 24, 40));
    }

    [Fact]
    public void A_peak_set_past_the_plateau_only_decays_from_when_it_was_set()
    {
        // Peak at 40, now 45: five years of decline, not eleven.
        var value = Retraining.AgeAdjustedPeak(50, ageAtPeak: 40, ageNow: 45);
        Assert.Equal(50 * Math.Pow(0.993, 5), value, precision: 6);
    }
}

public sealed class AltitudeTests
{
    [Fact]
    public void Sea_level_costs_nothing()
    {
        Assert.Equal(0, Altitude.Penalty(0));
        Assert.Equal(600, Altitude.AtAltitude(600, 300), precision: 6);
    }

    [Fact]
    public void El_Paso_costs_about_one_percent()
    {
        Assert.InRange(Altitude.Penalty(1190), 0.008, 0.014);
    }

    [Fact]
    public void Mexico_City_costs_two_to_three_percent_matching_peronnet()
    {
        Assert.InRange(Altitude.Penalty(2240), 0.02, 0.035);
    }

    [Fact]
    public void Conversion_roundtrips()
    {
        var atAltitude = Altitude.AtAltitude(1009, 1190);
        Assert.Equal(1009, Altitude.ToSeaLevel(atAltitude, 1190), precision: 8);
        Assert.True(atAltitude > 1009);
    }
}

/// <summary>The two-phase trajectory: reclaiming beats building, and the seams are smooth.</summary>
public sealed class RetrainedTrajectoryTests
{
    // The athlete this app serves: anchored at 37 with a documented 12:45
    // two-mile past (VDOT ~48.7, fully reclaimable at his age).
    private static readonly TrajectoryParameters Returning = new(StartVdot: 37, ReclaimVdot: 48.7);
    private static readonly TrajectoryParameters Novice = new(StartVdot: 37);

    [Fact]
    public void A_trained_past_accelerates_everything_below_the_old_peak()
    {
        // Same dose, same start: the returning athlete is ahead at every
        // horizon, dramatically so early.
        foreach (var months in new[] { 3.0, 6, 12, 18 })
        {
            Assert.True(
                Trajectory.VdotAt(Returning, 8.55, months) > Trajectory.VdotAt(Novice, 8.55, months) + 1);
        }
    }

    [Fact]
    public void Without_a_past_peak_the_model_is_unchanged()
    {
        // Back-compatibility is the calibration: the de-novo curve must be
        // exactly what it was before retraining existed.
        var ceiling = Trajectory.Ceiling(Novice, 5.74);
        var expected = ceiling - (ceiling - 37) * Math.Exp(-0.0676 * 12);
        Assert.Equal(expected, Trajectory.VdotAt(Novice, 5.74, 12), precision: 8);
    }

    [Fact]
    public void The_five_mile_goal_arrives_in_about_a_year_for_the_returning_athlete()
    {
        // VDOT 48 sits below the reclaimable 48.7, so nearly the whole climb
        // runs at the fast rate: on the committed dose it lands near a year,
        // where the de-novo model said twenty months.
        var months = Trajectory.MonthsToReach(Returning, 8.55, 48);
        Assert.NotNull(months);
        Assert.InRange(months!.Value, 8, 15);

        var noviceMonths = Trajectory.MonthsToReach(Novice, 8.55, 48);
        Assert.True(months < noviceMonths);
    }

    [Fact]
    public void Beyond_the_lifetime_best_is_still_the_slow_road()
    {
        // VDOT 51 is past the 48.7 peak: the last stretch runs at the de-novo
        // rate, so it lands well after the reclaim phase ends.
        var toPeak = Trajectory.MonthsToReach(Returning, 8.55, 48.5)!.Value;
        var pastPeak = Trajectory.MonthsToReach(Returning, 8.55, 51)!.Value;
        Assert.True(pastPeak > toPeak + 4);
    }

    [Fact]
    public void The_curve_still_starts_at_the_anchor_and_respects_the_ceiling()
    {
        Assert.Equal(37, Trajectory.VdotAt(Returning, 8.55, 0), precision: 8);

        var ceiling = Trajectory.Ceiling(Returning, 8.55);
        Assert.True(Trajectory.VdotAt(Returning, 8.55, 120) < ceiling);
    }

    [Fact]
    public void A_low_dose_caps_the_reclaim_at_its_own_ceiling()
    {
        // 1.5 effective hours supports only VDOT 40.4: even a big past peak
        // cannot be reclaimed past what the current dose sustains.
        Assert.True(Trajectory.VdotAt(Returning, 1.5, 240) < 40.5);
        Assert.Null(Trajectory.MonthsToReach(Returning, 1.5, 48));
    }

    [Fact]
    public void The_inverse_solver_reflects_the_faster_path()
    {
        // Reaching 48 in 12 months needs meaningfully fewer hours for the
        // returning athlete than for the novice.
        var returningHours = Trajectory.HoursToReach(Returning, 48, 12);
        var noviceHours = Trajectory.HoursToReach(Novice, 48, 12);

        Assert.NotNull(returningHours);
        Assert.True(noviceHours is null || returningHours < noviceHours - 1);

        // And the answer roundtrips through the forward model.
        Assert.Equal(48, Trajectory.VdotAt(Returning with { }, returningHours!.Value, 12), precision: 4);
    }
}
