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
        Assert.Contains(steps, s => s.Label == "Needed" && s.Value == "17.9% past your best");
        Assert.Contains(steps, s => s.Label == "A lifter's gain");
        Assert.Contains(steps, s => s.Label == "Chance by the date");

        // A goal inside reach is likely, and ready on a stated month.
        var near = GateOutlook.Line(Line(bench, Comparison.AtLeast, 215, "lb"), 13.3, context);
        Assert.True(near.Probability > 0.8);
        Assert.NotNull(near.ReadyInMonths);
    }

    [Fact]
    public void A_squat_below_where_lifters_start_gains_as_a_beginner_and_reaches_315()
    {
        // His squat on 2026-11-24: 193 lb from 165 × 5, best logged 221 lb,
        // at 170 lb bodyweight. On the lifters' curve alone the page said
        // 315 lb was not reached in ten years.
        var today = new LocalDate(2026, 11, 24);
        var squat = SelectionReadiness.Metrics.BackSquatLb;
        IReadOnlyList<DatedValue> history = [new(new(2026, 4, 10), 221), new(new(2026, 11, 23), 193)];
        var current = new Dictionary<string, Measurement>
        {
            [squat] = new(squat, 193, Basis.Modeled, "", new LocalDate(2026, 11, 23), "Hevy")
        };
        var context = Context(current: current, histories: new Dictionary<string, IReadOnlyList<DatedValue>> { [squat] = history })
            with { Today = today, BodyKg = 170 / BodyMass.PoundsPerKg };
        var goal = Line(squat, Comparison.AtLeast, 315, "lb");

        // Competing lifters start at 1.75 × bodyweight: 297.5 lb at 170 lb.
        Assert.Equal(297.5, GateOutlook.LifterEntry(goal, context.BodyKg)!.Value, precision: 6);

        var line = GateOutlook.Line(goal, 13.2, context);

        // 221 lb back in 8 weeks. 40% a year takes it to 297.5 lb in 10.4
        // months, and 12% a year from there gives 300 lb on the date.
        Assert.InRange(line.Projected!.Value, 298, 303);
        Assert.InRange(line.Probability!.Value, 0.15, 0.35);

        // The central projection reaches 315 lb in mid 2028.
        Assert.NotNull(line.ReachMonths);
        Assert.InRange(line.ReachMonths!.Value, 16, 21);

        Assert.Contains("as a beginner gains, up to the 298 lb", line.Evidence);
        Assert.Contains(line.Steps!, s => s.Label == "Where competing lifters start");
        Assert.Contains(line.Steps!, s => s.Label == "Below it, a beginner's gain");
        Assert.Contains(line.Steps!, s => s.Label == "From it, a lifter's gain");

        // Without the table, the lifters' curve alone never gets there: the defect.
        Assert.Null(StrengthProgression.MonthsToReach(193, 221, 315));
        Assert.NotNull(StrengthProgression.MonthsToReach(193, 221, 315, 297.5));
    }

    [Fact]
    public void The_lifters_entry_level_is_read_in_each_lines_own_unit()
    {
        double? Entry(string metric, string unit) => GateOutlook.LifterEntry(Line(metric, Comparison.AtLeast, 1, unit), 170 / BodyMass.PoundsPerKg);

        Assert.Equal(1.19 * 170, Entry(SelectionReadiness.Metrics.BenchPressLb, "lb")!.Value, precision: 6);
        Assert.Equal(1.19, Entry(SelectionReadiness.Metrics.BenchPressToBodyweight, "xbw")!.Value, precision: 6);

        // A triple is a one-rep max over 1.1, and five reps over 1 + 5 ÷ 30 (Epley).
        Assert.Equal(2.03 * 170 / 1.1, Entry(SelectionReadiness.Metrics.DeadliftTripleLb, "lb")!.Value, precision: 6);
        Assert.Equal(1.75 * 170 / (1 + 5 / 30.0), Entry(SelectionReadiness.Metrics.BackSquatFiveLb, "lb")!.Value, precision: 6);

        // No table for the front squat, and no pounds without a bodyweight.
        Assert.Null(Entry(SelectionReadiness.Metrics.FrontSquatLb, "lb"));
        Assert.Null(GateOutlook.LifterEntry(Line(SelectionReadiness.Metrics.BackSquatLb, Comparison.AtLeast, 315, "lb"), null));
    }

    [Fact]
    public void A_beginners_curve_hands_over_to_a_lifters_at_the_entry_level()
    {
        // Below the line: 40% of the start in a year.
        Assert.Equal(200 * 1.2, StrengthProgression.At(200, null, 6, 0, 400).Projected, precision: 6);
        Assert.Equal(200 * 1.4, StrengthProgression.At(200, null, 12, 0, 400).Projected, precision: 6);

        // It slows after a year: 55% at six years.
        Assert.Equal(200 * 1.55, StrengthProgression.At(200, null, 72, 0, 400).Projected, precision: 6);

        // The line is crossed at 240 lb after 6 months, and the lifter's year runs from there.
        Assert.Equal(240, StrengthProgression.At(200, null, 6, 0, 240).Projected, precision: 6);
        Assert.Equal(240 * 1.12, StrengthProgression.At(200, null, 18, 0, 240).Projected, precision: 6);

        // At or past the line the beginner's curve never applies.
        Assert.Equal(StrengthProgression.At(200, null, 12, 0), StrengthProgression.At(200, null, 12, 0, 200));
        Assert.Equal(StrengthProgression.At(200, null, 12, 0), StrengthProgression.At(200, null, 12, 0, 150));

        // The beginner's rate is the less certain one.
        Assert.True(StrengthProgression.At(200, null, 12, 0, 400).Sd > StrengthProgression.At(200, null, 12, 0).Sd);
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
