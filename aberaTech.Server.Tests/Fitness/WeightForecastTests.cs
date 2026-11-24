using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Bodyweight and body fat from the stated plan and the weigh-ins since it
/// began, on his own numbers of 2026-11-24.
/// </summary>
public sealed class WeightForecastTests
{
    private static readonly LocalDate Today = new(2026, 11, 24);
    private static readonly LocalDate Due = new(2027, 12, 31);

    // Lose It, read 2026-11-24: 1.5 lb a week from 30 October to 165 lb.
    private static readonly WeightPlan Plan = new(new LocalDate(2026, 10, 30), 1.5, 165);

    /// <summary>The year of weigh-ins the straight line ran through, and the four since the cut began.</summary>
    private static readonly IReadOnlyList<DatedValue> WeighIns =
    [
        new(new(2025, 12, 26), 169.6), new(new(2026, 4, 3), 174.0), new(new(2026, 4, 5), 176.0), new(new(2026, 8, 30), 175.0),
        new(new(2026, 10, 30), 172.0), new(new(2026, 10, 31), 175.0), new(new(2026, 11, 19), 170.6), new(new(2026, 11, 21), 170.0),
    ];

    private static OutlookContext Context(WeightPlan? plan, IReadOnlyList<DatedValue>? weighIns = null, double? fat = 20)
    {
        var fit = ModelFit.Fit([], new ModelFit.Priors(35.8));
        var p = new TrajectoryParameters(35.8, null, fit.RatePerMonth.Value, fit.Responsiveness.Value);
        var limits = p.Limits();
        var histories = new Dictionary<string, IReadOnlyList<DatedValue>>
        {
            [SelectionReadiness.Metrics.BodyweightLb] = weighIns ?? WeighIns
        };
        if (fat is { } percent) histories[SelectionReadiness.Metrics.BodyFat] = [new(new(2026, 8, 30), percent)];

        return new OutlookContext(
            p, fit, DoseSchedule.Constant(DoseResponse.Allocate(7, limits).Dose), limits, 15, 77.1,
            new Dictionary<string, Measurement>(), histories, Today, plan);
    }

    private static Requirement Weight => new(
        SelectionReadiness.Metrics.BodyweightLb, "Bodyweight", Comparison.AtMost, 165, "lb", "test");

    private static Requirement Fat => new(
        SelectionReadiness.Metrics.BodyFat, "Body fat %", Comparison.AtMost, 10, "%", "test");

    private static double Months(LocalDate to) => Period.Between(Today, to, PeriodUnits.Days).Days / 30.4375;

    [Fact]
    public void Weigh_ins_from_before_the_plan_move_nothing()
    {
        var state = WeightForecast.Fit(Plan, WeighIns)!;
        var sinceOnly = WeightForecast.Fit(Plan, WeighIns.Where(w => w.Date >= Plan.StartedOn).ToArray())!;

        Assert.Equal(sinceOnly, state);
        Assert.Equal(4, state.WeighIns);
        Assert.Equal(new LocalDate(2026, 10, 30), state.FirstWeighIn);
    }

    [Fact]
    public void The_rate_combines_the_plan_and_the_weigh_ins_with_early_water_taken_off()
    {
        var state = WeightForecast.Fit(Plan, WeighIns)!;

        // The plan at the 73% dieters reach: 1.5 × 20.1 ÷ 27.6 = 1.09 ± 0.61.
        Assert.Equal(1.5 * 20.1 / 27.6, state.PriorLbPerWeek, precision: 9);
        Assert.Equal(1.5 * 11.3 / 27.6, state.PriorSd, precision: 9);

        // Least squares through the four weigh-ins: 1.07 lb a week.
        Assert.Equal(1.071, state.ObservedLbPerWeek!.Value, precision: 2);

        // All 22 days sit in the first 28, so the whole early-water ratio applies.
        Assert.Equal(4858.0 / 6569.0, state.Discount, precision: 9);

        // Between the two, nearer the measured one: about 0.83 lb a week.
        Assert.InRange(state.RateLbPerWeek, 0.80, 0.86);
        Assert.InRange(state.RateSd, 0.18, 0.26);

        // The level is the fitted line at the last weigh-in, not the last reading alone.
        Assert.InRange(state.LevelLb, 170.0, 170.4);
        Assert.Equal(new LocalDate(2026, 11, 21), state.LevelOn);
    }

    [Fact]
    public void He_reaches_165_in_early_January_and_holds_it_where_the_straight_line_said_172()
    {
        var line = GateOutlook.Line(Weight, Months(Due), Context(Plan));

        Assert.Equal(OutlookMethod.Plan, line.Method);

        // The straight line projected 172 lb on the due date with a 14% chance.
        Assert.Equal(165, line.Projected!.Value, precision: 6);
        Assert.True(line.Probability > 0.99);

        // (170.2 − 165) ÷ 0.83 = 6.3 weeks after 21 November: the first week of January.
        var reach = WeightForecast.ReachOn(WeightForecast.Fit(Plan, WeighIns)!, 165)!.Value;
        Assert.InRange(reach, new LocalDate(2026, 12, 30), new LocalDate(2027, 1, 10));
        Assert.InRange(line.ReachMonths!.Value, 1.1, 1.6);

        Assert.Contains("165 lb on about", line.Evidence);
        Assert.Contains("Holding the goal is your plan and is not forecast.", line.Evidence);
        Assert.Contains(line.Steps!, s => s.Label == "Less early water");
        Assert.Contains(line.Steps!, s => s.Label == "Reaches 165 lb");
    }

    [Fact]
    public void Lose_Its_own_date_of_18_December_is_unlikely_and_the_page_says_so()
    {
        var line = GateOutlook.Line(Weight, Months(new LocalDate(2026, 12, 18)), Context(Plan));

        // 170.2 − 0.83 × 3.9 weeks = 167.0 lb, about 1.1 lb of spread.
        Assert.InRange(line.Projected!.Value, 166.5, 167.5);
        Assert.InRange(line.Probability!.Value, 0.005, 0.10);
    }

    [Fact]
    public void Without_a_plan_or_a_weigh_in_since_it_began_nothing_is_forecast()
    {
        var none = GateOutlook.Line(Weight, Months(Due), Context(null));
        Assert.Equal(OutlookMethod.None, none.Method);
        Assert.Equal(GateOutlook.NoPlan, none.Evidence);
        Assert.Null(none.Probability);

        var before = WeighIns.Where(w => w.Date < Plan.StartedOn).ToArray();
        var waiting = GateOutlook.Line(Weight, Months(Due), Context(Plan, before));
        Assert.Equal(OutlookMethod.None, waiting.Method);
        Assert.Contains("no weigh-in since the plan began on 30 Oct 2026", waiting.Evidence);
    }

    [Fact]
    public void One_weigh_in_gives_a_level_and_the_plans_rate_alone()
    {
        var state = WeightForecast.Fit(Plan, [new(new(2026, 10, 31), 175.0)])!;

        Assert.Null(state.ObservedLbPerWeek);
        Assert.Equal(state.PriorLbPerWeek, state.RateLbPerWeek);
        Assert.Equal(175.0, state.LevelLb);
    }

    [Fact]
    public void A_later_span_takes_less_off_for_early_water()
    {
        // Weigh-ins from day 0 to day 56: half the span is past the first 28 days.
        IReadOnlyList<DatedValue> eightWeeks =
        [
            new(new(2026, 10, 30), 175), new(new(2026, 11, 27), 171), new(new(2026, 12, 25), 168)
        ];

        var state = WeightForecast.Fit(Plan, eightWeeks)!;

        Assert.Equal(1 - (1 - 4858.0 / 6569.0) * 28 / 56, state.Discount, precision: 9);
    }

    [Fact]
    public void Body_fat_follows_the_weight_and_stops_where_the_plan_stops()
    {
        var line = GateOutlook.Line(Fat, Months(Due), Context(Plan));

        Assert.Equal(OutlookMethod.Plan, line.Method);

        // 20% of 175.0 lb is 35.0 lb of fat. Ten pounds lost, 83% of it fat:
        // (35.0 − 8.3) ÷ 165 = 16.2%.
        Assert.Equal((35.0 - 0.83 * 10) / 165 * 100, line.Projected!.Value, precision: 6);
        Assert.True(line.Probability < 0.01);
        Assert.Null(line.ReachMonths);
        Assert.Contains("The plan stops at 165 lb, which gives 16.2%.", line.Evidence);
        Assert.Contains("10% there needs 10.2 lb more fat lost at the same weight.", line.Evidence);

        // The forecast moves with the reading: the sheet's 15% gives 10.9%.
        var lean = GateOutlook.Line(Fat, Months(Due), Context(Plan, fat: 15));
        Assert.Equal((26.25 - 8.3) / 165 * 100, lean.Projected!.Value, precision: 6);

        // No reading, no forecast.
        var blank = GateOutlook.Line(Fat, Months(Due), Context(Plan, fat: null));
        Assert.Equal(OutlookMethod.None, blank.Method);
    }
}
