using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The race-weight factor used to move the anchor and nothing else, so a plan
/// to race lighter raised the starting fitness toward a ceiling that stayed
/// where it was. That shortened the stretch re-earned at the retraining rate
/// and made the model gloomier the lighter the athlete planned to be — the
/// opposite of the physiology it was modelling.
/// </summary>
public sealed class RaceWeightTests(ITestOutputHelper output)
{
    private const double Mile = 1609.344;
    private static double Lb(double pounds) => pounds / BodyMass.PoundsPerKg;

    [Fact]
    public void A_lifetime_best_is_re_scored_at_the_weight_you_plan_to_race()
    {
        // VDOT is per kilogram, so the same absolute engine in a lighter body
        // scores higher — the treatment the anchor already had.
        var lighter = BodyMass.AtRaceWeight(48.7, Lb(178), Lb(165));
        Assert.NotNull(lighter);
        Assert.Equal(48.7 * 178 / 165, lighter!.Value, precision: 6);

        var heavier = BodyMass.AtRaceWeight(48.7, Lb(178), Lb(185));
        Assert.True(heavier < 48.7);
    }

    [Fact]
    public void The_same_clamp_and_caveat_apply_to_the_peak_as_to_the_anchor()
    {
        // Past ±10% the fat-mass-only assumption has nothing behind it, so the
        // adjustment stops rather than extrapolating.
        var absurd = BodyMass.AtRaceWeight(48.7, Lb(178), Lb(120))!.Value;
        var atTheClamp = BodyMass.AtRaceWeight(48.7, Lb(178), Lb(178 * 0.9))!.Value;

        Assert.Equal(atTheClamp, absurd, precision: 9);
    }

    [Fact]
    public void Without_a_recorded_peak_weight_the_peak_is_left_exactly_as_run()
    {
        Assert.Equal(48.7, BodyMass.AtRaceWeight(48.7, null, Lb(165)));
        Assert.Equal(48.7, BodyMass.AtRaceWeight(48.7, Lb(178), null));
        Assert.Null(BodyMass.AtRaceWeight(null, Lb(178), Lb(165)));
    }

    private static SolverContext Context(double? peakWeightKg)
    {
        var truth = new TrajectoryParameters(37, null, 0.09, 1.1);
        var dose = DoseResponse.Allocate(6).Dose;
        var rng = new Rng(31);
        var history = Enumerable.Range(0, 12)
            .Select(m => new FitObservation(
                m, Trajectory.VdotAt(truth, dose, m) + rng.Normal(0, 0.5), dose))
            .ToArray();

        // A 12:45 two-mile, the way the athlete's own lifetime best is scored.
        var peak = Vdot.FromRace(2 * Mile, 765 / 60.0);

        return new SolverContext(
            Posterior.Sample(history, new Posterior.Priors(history[0].ObservedVdot)),
            AnchorVdot: 37,
            ReclaimVdot: peak,
            CurrentDose: DoseResponse.Allocate(3).Dose,
            CurrentMassKg: Lb(174),
            AltitudeMeters: 1190,
            Limits: new DoseLimits(),
            PeakWeightKg: peakWeightKg);
    }

    [Fact]
    public void Racing_lighter_lifts_the_ceiling_and_not_only_the_floor()
    {
        // The regression, stated as the athlete would feel it: plan to race
        // lighter and the projection must improve by more than moving the
        // anchor alone would give, because the reclaimable peak moves too.
        var withPeakWeight = Context(Lb(178));
        var anchorOnly = Context(peakWeightKg: null);

        var atCurrent = new Scenario(2 * Mile, 24, 7, 0.85, Lb(174));
        var atGoal = atCurrent with { RaceMassKg = Lb(165) };

        var fixedCeiling = Solver.Predict(anchorOnly, atGoal).Median;
        var movingCeiling = Solver.Predict(withPeakWeight, atGoal).Median;

        output.WriteLine(
            $"2-mile at 165 lb: peak fixed {fixedCeiling:0.0}s, peak re-scored {movingCeiling:0.0}s");

        Assert.True(movingCeiling < fixedCeiling);

        // And lighter still beats heavier, which is the whole point of the slider.
        Assert.True(
            Solver.Predict(withPeakWeight, atGoal).Median <
            Solver.Predict(withPeakWeight, atCurrent).Median);
    }

    [Fact]
    public void A_goal_can_cross_from_new_territory_into_the_reclaim_band()
    {
        // Not a decimal: the athlete's own two-mile goal sits above the
        // age-adjusted peak at today's weight and inside it at race weight,
        // which is the difference between re-earning fitness at the retraining
        // rate and building it from scratch.
        var peak = Vdot.FromRace(2 * Mile, 765 / 60.0);
        var goal = Vdot.FromRace(2 * Mile, Altitude.ToSeaLevel(735, 1190) / 60.0);

        var atToday = BodyMass.AtRaceWeight(peak, Lb(178), Lb(174))!.Value;
        var atRaceWeight = BodyMass.AtRaceWeight(peak, Lb(178), Lb(165))!.Value;

        output.WriteLine(
            $"2-mile 12:15 needs VDOT {goal:0.0}; peak is {atToday:0.0} at 174 lb and {atRaceWeight:0.0} at 165 lb");

        Assert.True(goal > atToday, "the goal should be past the peak at today's weight");
        Assert.True(goal < atRaceWeight, "and inside it at race weight");
    }
}
