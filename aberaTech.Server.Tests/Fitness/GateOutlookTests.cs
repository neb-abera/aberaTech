using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The gates as a forecast: a probability per line by its due date, from the
/// trajectory where the line is a running one and from the line's own history
/// otherwise.
/// </summary>
public sealed class GateOutlookTests
{
    private static readonly LocalDate Today = new(2026, 9, 7);
    private const double BodyKg = 79.4;

    private static OutlookContext Context(
        double startVdot = 35.4,
        double hours = 5,
        IReadOnlyDictionary<string, Measurement>? current = null,
        IReadOnlyDictionary<string, IReadOnlyList<DatedValue>>? histories = null)
    {
        var fit = ModelFit.Fit([], new ModelFit.Priors(startVdot));
        var p = new TrajectoryParameters(startVdot, null, fit.RatePerMonth.Value, fit.Responsiveness.Value);
        var limits = p.Limits();
        var schedule = DoseSchedule.Constant(DoseResponse.Allocate(hours, limits).Dose);
        return new OutlookContext(
            p, fit, schedule, limits, 15, BodyKg,
            current ?? new Dictionary<string, Measurement>(),
            histories ?? new Dictionary<string, IReadOnlyList<DatedValue>>(),
            Today);
    }

    private static Requirement Line(string metric, Comparison comparison, double target, string unit = "s") =>
        new(metric, metric, comparison, target, unit, "test");

    [Fact]
    public void A_two_mile_already_inside_the_standard_is_near_certain_and_a_hard_one_is_not()
    {
        var context = Context();

        // 16:49 today against an 18:00 line: clear now, and the trajectory
        // only rises.
        var easy = GateOutlook.Line(Line(SelectionReadiness.Metrics.RunTwoMile, Comparison.AtMost, 18 * 60), 6, context);
        Assert.Equal(OutlookMethod.Trajectory, easy.Method);
        Assert.InRange(easy.Probability!.Value, 0.9, 1.0);
        Assert.Equal(0, easy.ReadyInMonths);
        Assert.Equal(0, easy.HoursToReach);

        // 13:00 in six months at five hours a week is a long shot.
        var hard = GateOutlook.Line(Line(SelectionReadiness.Metrics.RunTwoMile, Comparison.AtMost, 13 * 60), 6, context);
        Assert.InRange(hard.Probability!.Value, 0.0, 0.3);
        Assert.True(hard.Projected > 13 * 60);
        Assert.Contains("Needs VDOT", hard.Evidence);
    }

    [Fact]
    public void The_ruck_line_is_the_load_carriage_model_inverted()
    {
        var vdot = GateOutlook.RuckVdot(BodyKg, LoadCarriage.ReferenceLoadKg, 3 * 3600);

        var predicted = LoadCarriage.PredictSeconds(vdot, BodyKg, LoadCarriage.ReferenceLoadKg, 12 * Vdot.MileMeters);
        Assert.Equal(3 * 3600, predicted, precision: 0);

        var line = GateOutlook.Line(Line(SelectionReadiness.Metrics.RuckTwelveMileAt45, Comparison.AtMost, 3 * 3600), 12, Context());
        Assert.Equal(OutlookMethod.Trajectory, line.Method);
        Assert.NotNull(line.Probability);
    }

    [Fact]
    public void The_aet_pace_line_scales_the_engine_by_todays_ratio()
    {
        var current = new Dictionary<string, Measurement>
        {
            [SelectionReadiness.Metrics.AerobicThresholdPace] =
                new(SelectionReadiness.Metrics.AerobicThresholdPace, 620, Basis.Measured, "")
        };
        var context = Context(current: current);

        var target = GateOutlook.TargetVdot(Line(SelectionReadiness.Metrics.AerobicThresholdPace, Comparison.AtMost, 480, "s/mi"), context);

        // 10:20 to 8:00 a mile is a 29% speed gain; VDOT has to rise by more
        // than that, since it is more than proportional to speed.
        Assert.NotNull(target);
        Assert.True(target > context.P.StartVdot * 1.29);

        // And without a measured AeT pace there is nothing to scale from.
        Assert.Null(GateOutlook.TargetVdot(Line(SelectionReadiness.Metrics.AerobicThresholdPace, Comparison.AtMost, 480, "s/mi"), Context()));
    }

    [Fact]
    public void An_event_with_no_model_is_held_or_blank_and_never_a_line_through_its_readings()
    {
        // His push-ups on 2026-11-24: six training sets falling from 20 to
        // 12 since April. The straight line projected -1 rep for 2027.
        var pushUps = SelectionReadiness.Metrics.PushUps;
        var falling = new Dictionary<string, IReadOnlyList<DatedValue>>
        {
            [pushUps] =
            [
                new(new(2026, 4, 8), 20), new(new(2026, 8, 20), 18), new(new(2026, 9, 2), 16),
                new(new(2026, 10, 30), 15), new(new(2026, 11, 8), 14), new(new(2026, 11, 21), 12)
            ]
        };

        var line = GateOutlook.Line(Line(pushUps, Comparison.AtLeast, 80, "reps"), 13.2, Context(histories: falling));

        Assert.Equal(OutlookMethod.None, line.Method);
        Assert.Null(line.Probability);
        Assert.Null(line.Projected);
        Assert.Equal(GateOutlook.NoModel, line.Evidence);

        var pullUps = SelectionReadiness.Metrics.PullUps;
        var passingNow = new Dictionary<string, Measurement> { [pullUps] = new(pullUps, 14, Basis.Measured, "") };
        var held = GateOutlook.Line(Line(pullUps, Comparison.AtLeast, 12, "reps"), 6, Context(current: passingNow));
        Assert.Equal(OutlookMethod.Held, held.Method);
        Assert.Null(held.Probability);
    }

    [Fact]
    public void A_lift_is_forecast_forward_from_todays_max_and_not_through_older_readings()
    {
        // His bench as Hevy read it on 2026-11-21: 207 lb in April, 210 in
        // May, a dip to 175 on 31 October, and 207 again from 155 × 10 on
        // 17 November. The straight line through them fell to 205 lb.
        var today = new LocalDate(2026, 11, 21);
        var bench = SelectionReadiness.Metrics.BenchPressLb;
        IReadOnlyList<DatedValue> history =
        [
            new(new(2026, 4, 5), 207), new(new(2026, 5, 15), 210), new(new(2026, 8, 19), 196),
            new(new(2026, 9, 1), 192), new(new(2026, 10, 31), 175), new(new(2026, 11, 8), 202),
            new(new(2026, 11, 17), 207)
        ];
        var current = new Dictionary<string, Measurement>
        {
            [bench] = new(bench, 207, Basis.Modeled, "", new LocalDate(2026, 11, 17), "Hevy")
        };
        var context = Context(current: current, histories: new Dictionary<string, IReadOnlyList<DatedValue>> { [bench] = history })
            with { Today = today };

        var line = GateOutlook.Line(Line(bench, Comparison.AtLeast, 247.5, "lb"), 13.3, context);

        Assert.Equal(OutlookMethod.Progression, line.Method);

        // 210 lb back in 8 weeks, then 12% a year for the 11.5 months left:
        // 210 × (1 + 0.12 × 11.46 ÷ 12) = 234 lb.
        Assert.InRange(line.Projected!.Value, 233, 235);

        // 247.5 lb is 17.9% past his best, against 12% ± 6: a real chance,
        // where 8.8% a year gave 2%.
        Assert.InRange(line.Probability!.Value, 0.15, 0.30);
        Assert.Contains("From today's 207 lb", line.Evidence);
        Assert.Contains("back to your best logged 210 lb", line.Evidence);

        // The central projection reaches it, on a stated month past the due date.
        Assert.NotNull(line.ReachMonths);
        Assert.InRange(line.ReachMonths!.Value, 13.3, 48);

        // The arithmetic names the rate needed and the rate assumed.
        var steps = line.Steps!;
        Assert.Contains(steps, s => s.Label == "Back to your best");
        Assert.Contains(steps, s => s.Label == "Needed" && s.Value == "against 12% ± 6%");
        Assert.Contains(steps, s => s.Label == "Chance by the date");

        // A goal inside reach is likely, and ready on a stated month.
        var near = GateOutlook.Line(Line(bench, Comparison.AtLeast, 215, "lb"), 13.3, context);
        Assert.True(near.Probability > 0.8);
        Assert.NotNull(near.ReadyInMonths);
    }

    [Fact]
    public void A_lift_climbs_back_to_its_best_in_eight_weeks_then_gains_at_the_rate_of_his_level()
    {
        // No older best: 12% in the first year from today.
        var (year, _) = StrengthProgression.At(200, null, 12, 0);
        Assert.Equal(200 * 1.12, year, precision: 6);

        // The gain slows after the first year: up to 20% at ten years
        // against 10% at one (Latella 2024), so ten years doubles one.
        var (decade, _) = StrengthProgression.At(200, null, 120, 0);
        Assert.Equal(200 * 1.24, decade, precision: 6);
        Assert.True(StrengthProgression.At(200, null, 24, 0).Projected < 200 * 1.24);

        // A best of 220 is back at 8 weeks, and the year runs from there.
        var (back, _) = StrengthProgression.At(200, 220, StrengthProgression.ReclaimMonths, 0);
        Assert.Equal(220, back, precision: 6);
        var (halfway, _) = StrengthProgression.At(200, 220, StrengthProgression.ReclaimMonths / 2, 0);
        Assert.Equal(210, halfway, precision: 6);

        // A lower old reading changes nothing.
        Assert.Equal(StrengthProgression.At(200, null, 6, 0), StrengthProgression.At(200, 150, 6, 0));

        // An old estimate is less certain than a fresh one.
        Assert.True(StrengthProgression.At(200, null, 6, 6).Sd > StrengthProgression.At(200, null, 6, 0).Sd);

        // The reach date is when the central projection gets there, or null.
        Assert.Equal(12, StrengthProgression.MonthsToReach(200, null, 223.9)!.Value, precision: 6);
        Assert.Null(StrengthProgression.MonthsToReach(200, null, 400));
    }
}
