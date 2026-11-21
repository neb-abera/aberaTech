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
    public void A_straight_line_through_dated_readings_projects_with_a_prediction_interval()
    {
        var history = new List<DatedValue>
        {
            new(Today.PlusDays(-90), 8), new(Today.PlusDays(-60), 9), new(Today.PlusDays(-30), 10), new(Today, 11)
        };

        var at = GateOutlook.TrendAt(history, Today.PlusDays(90));

        Assert.NotNull(at);
        Assert.Equal(14, at!.Value.Projected, precision: 6);
        Assert.True(at.Value.Sd > 0);

        // Two readings, or three within a fortnight, are not a trend.
        Assert.Null(GateOutlook.TrendAt(history.Take(2).ToList(), Today));
        Assert.Null(GateOutlook.TrendAt([new(Today.PlusDays(-10), 8), new(Today.PlusDays(-5), 9), new(Today, 10)], Today));
    }

    [Fact]
    public void A_reps_line_is_forecast_from_its_trend_and_held_or_blank_without_one()
    {
        var pullUps = SelectionReadiness.Metrics.PullUps;
        var rising = new Dictionary<string, IReadOnlyList<DatedValue>>
        {
            [pullUps] = [new(Today.PlusDays(-90), 8), new(Today.PlusDays(-60), 9), new(Today.PlusDays(-30), 10), new(Today, 11)]
        };

        var trend = GateOutlook.Line(Line(pullUps, Comparison.AtLeast, 12, "reps"), 6, Context(histories: rising));
        Assert.Equal(OutlookMethod.Trend, trend.Method);
        Assert.InRange(trend.Probability!.Value, 0.9, 1.0);
        Assert.NotNull(trend.ReadyInMonths);
        Assert.Contains("Straight line through 4 readings", trend.Evidence);

        var passingNow = new Dictionary<string, Measurement> { [pullUps] = new(pullUps, 14, Basis.Measured, "") };
        var held = GateOutlook.Line(Line(pullUps, Comparison.AtLeast, 12, "reps"), 6, Context(current: passingNow));
        Assert.Equal(OutlookMethod.Held, held.Method);
        Assert.Null(held.Probability);

        var blank = GateOutlook.Line(Line(pullUps, Comparison.AtLeast, 12, "reps"), 6, Context());
        Assert.Equal(OutlookMethod.None, blank.Method);
        Assert.Contains("the log has 0", blank.Evidence);
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
        var straight = GateOutlook.TrendAt(history, new LocalDate(2027, 12, 31));
        Assert.True(straight!.Value.Projected < 207);

        var current = new Dictionary<string, Measurement>
        {
            [bench] = new(bench, 207, Basis.Modeled, "", new LocalDate(2026, 11, 17), "Hevy")
        };
        var context = Context(current: current, histories: new Dictionary<string, IReadOnlyList<DatedValue>> { [bench] = history })
            with { Today = today };

        var line = GateOutlook.Line(Line(bench, Comparison.AtLeast, 247.5, "lb"), 13.3, context);

        Assert.Equal(OutlookMethod.Progression, line.Method);
        Assert.InRange(line.Projected!.Value, 220, 235);
        Assert.InRange(line.Probability!.Value, 0.005, 0.2);
        Assert.Contains("From today's 207 lb", line.Evidence);
        Assert.Contains("back to your best logged 210 lb", line.Evidence);

        // A goal inside reach is likely, and ready on a stated month.
        var near = GateOutlook.Line(Line(bench, Comparison.AtLeast, 215, "lb"), 13.3, context);
        Assert.True(near.Probability > 0.8);
        Assert.NotNull(near.ReadyInMonths);
    }

    [Fact]
    public void A_lift_climbs_back_to_its_best_in_eight_weeks_then_gains_at_the_trained_rate()
    {
        // No older best: 8.8% a year from today.
        var (year, _) = StrengthProgression.At(200, null, 12, 0);
        Assert.Equal(200 * (1 + StrengthProgression.GainPerYear), year, precision: 6);
        Assert.InRange(StrengthProgression.GainPerYear, 0.087, 0.088);

        // A best of 220 is back at 8 weeks, and the year runs from there.
        var (back, _) = StrengthProgression.At(200, 220, StrengthProgression.ReclaimMonths, 0);
        Assert.Equal(220, back, precision: 6);
        var (halfway, _) = StrengthProgression.At(200, 220, StrengthProgression.ReclaimMonths / 2, 0);
        Assert.Equal(210, halfway, precision: 6);

        // A lower old reading changes nothing.
        Assert.Equal(StrengthProgression.At(200, null, 6, 0), StrengthProgression.At(200, 150, 6, 0));

        // An old estimate is less certain than a fresh one.
        Assert.True(StrengthProgression.At(200, null, 6, 6).Sd > StrengthProgression.At(200, null, 6, 0).Sd);
    }

    [Fact]
    public void A_trend_reads_only_the_last_year_of_readings()
    {
        // Weigh-ins from 2017 at 167 lb would tilt a line through 2026's.
        IReadOnlyList<DatedValue> history =
        [
            new(new(2017, 1, 22), 167), new(new(2017, 2, 8), 167),
            new(new(2026, 4, 3), 174), new(new(2026, 8, 30), 174), new(new(2026, 11, 21), 174)
        ];

        var at = GateOutlook.TrendAt(history, new LocalDate(2027, 12, 31));

        Assert.Equal(174, at!.Value.Projected, precision: 6);
    }
}
