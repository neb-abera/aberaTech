using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>A deliberate weight change the athlete has stated: when it began, how fast, and where it stops.</summary>
/// <param name="StartedOn">The first day of the plan. Weigh-ins before it say nothing about its rate.</param>
/// <param name="LbPerWeek">The planned loss, in pounds a week.</param>
/// <param name="GoalLb">The weight the plan stops at and holds.</param>
public sealed record WeightPlan(LocalDate StartedOn, double LbPerWeek, double GoalLb);

/// <summary>Where the weight is and how fast it is moving, with the parts the answer was made from.</summary>
/// <param name="LevelLb">The weight on <paramref name="LevelOn"/>, read off the weigh-ins since the plan began.</param>
/// <param name="RateLbPerWeek">The expected loss from here, in pounds a week.</param>
/// <param name="ObservedLbPerWeek">The loss the weigh-ins show, before the early-water discount; null when they are too few.</param>
/// <param name="Discount">What the observed rate was multiplied by for early water.</param>
public sealed record WeightState(
    double LevelLb,
    double LevelSd,
    LocalDate LevelOn,
    double RateLbPerWeek,
    double RateSd,
    int WeighIns,
    LocalDate FirstWeighIn,
    double? ObservedLbPerWeek,
    double? ObservedSd,
    double Discount,
    double PriorLbPerWeek,
    double PriorSd);

/// <summary>The latest body fat reading, with the weight it was taken at.</summary>
public sealed record BodyFatReading(LocalDate Date, double Percent, double WeightLb);

/// <summary>
/// Bodyweight on a date, from the plan the athlete stated and the weigh-ins
/// since it began.
/// </summary>
/// <remarks>
/// A weight is forecast from a plan. On 2026-11-24 the console ran a straight
/// line through eight weigh-ins from December 2025 on, most of them taken
/// while he was not trying to lose weight, and projected 172 lb for December
/// 2027. He had started a cut on 30 October, and Lose It held the plan:
/// 1.5 lb a week to 165 lb. Weigh-ins before a plan starts say nothing about
/// its rate, so they are left out.
///
/// The rate has two sources. The plan's own rate is the 3,500 kcal a pound
/// rule, and dieters lose 73% of what that rule predicts, with a spread of
/// 41% (Thomas 2013: 20.1 ± 11.3 lb against 27.6 predicted). The weigh-ins
/// give the rate observed so far. In the first four weeks part of the loss
/// is water and glycogen. A kilogram lost held 4,858 kcal at week 4 and
/// 6,569 kcal at week 24 in the CALERIE trial (Thomas 2014), so the same
/// deficit moves the scale 0.74 times as fast afterward. The observed rate is discounted
/// by that share of its span inside the first 28 days. The two rates are
/// combined by their precisions.
///
/// One weigh-in is the weight plus noise. The change between consecutive
/// weigh-ins has a residual standard deviation of 0.764% of bodyweight
/// (Helander 2014, Table 2), so one weigh-in carries 0.764% ÷ √2 = 0.54%.
///
/// The plan stops at its goal and holds it. Holding is the plan and is not
/// forecast: the published regain rates come from obesity trials and from
/// 23 athletes who cut for a season (Hall and Kahan 2018, Garthe 2011).
///
/// Citations: <see cref="Citations.ThomasWeightLoss"/>, <see cref="Citations.HelanderWeighIns"/>.
/// </remarks>
public static class WeightForecast
{
    /// <summary>Thomas 2013: the share of the 3,500 kcal rule's predicted loss dieters lost (20.1 of 27.6 lb).</summary>
    public const double RuleShare = 20.1 / 27.6;

    /// <summary>Thomas 2013: the spread of that loss between dieters, as a share of the prediction (11.3 of 27.6 lb).</summary>
    public const double RuleSpread = 11.3 / 27.6;

    /// <summary>Thomas 2014: the energy in a kilogram lost at week 4 over the energy at week 24.</summary>
    public const double EarlyEnergyRatio = 4858.0 / 6569.0;

    /// <summary>The days in which part of the loss is water and glycogen.</summary>
    public const int EarlyDays = 28;

    /// <summary>Helander 2014: one weigh-in's noise, a fraction of bodyweight (0.764% ÷ √2).</summary>
    public static readonly double WeighInNoise = 0.00764 / Math.Sqrt(2);

    /// <summary>Weigh-ins closer together than this give a level and no rate.</summary>
    public const int MinimumSpanDays = 14;

    public const string Model =
        "Your stated weight plan and the weigh-ins since it began: the plan's rate at the 73% dieters reach (Thomas 2013), combined with your observed rate, which is discounted for early water (Thomas 2014)";

    /// <summary>The level and rate from the weigh-ins since the plan began; null when there are none.</summary>
    public static WeightState? Fit(WeightPlan plan, IReadOnlyList<DatedValue> weighIns)
    {
        var points = weighIns
            .Where(w => w.Date >= plan.StartedOn)
            .GroupBy(w => w.Date)
            .Select(g => g.Last())
            .OrderBy(w => w.Date)
            .ToArray();
        if (points.Length == 0) return null;

        var prior = plan.LbPerWeek * RuleShare;
        var priorSd = plan.LbPerWeek * RuleSpread;

        var first = points[0].Date;
        var last = points[^1].Date;
        var mean = points.Average(p => p.Value);
        var noise = WeighInNoise * mean;
        var span = Period.Between(first, last, PeriodUnits.Days).Days;

        if (points.Length < 2 || span < MinimumSpanDays)
        {
            return new WeightState(
                mean, noise / Math.Sqrt(points.Length), last, prior, priorSd, points.Length, first, null, null, 1, prior, priorSd);
        }

        // Least squares of weight on days since the first weigh-in.
        var xs = points.Select(p => (double)Period.Between(first, p.Date, PeriodUnits.Days).Days).ToArray();
        var xBar = xs.Average();
        var sxx = xs.Sum(x => (x - xBar) * (x - xBar));
        var slope = xs.Zip(points, (x, p) => (x - xBar) * (p.Value - mean)).Sum() / sxx;

        var level = mean + slope * (xs[^1] - xBar);
        var levelSd = noise * Math.Sqrt(1.0 / points.Length + (xs[^1] - xBar) * (xs[^1] - xBar) / sxx);

        var observed = -slope * 7;
        var observedSd = noise / Math.Sqrt(sxx) * 7;

        // The share of the observed span that lies in the plan's first 28 days.
        var earlyEnd = plan.StartedOn.PlusDays(EarlyDays);
        var earlyDays = Math.Max(0, Period.Between(first, last < earlyEnd ? last : earlyEnd, PeriodUnits.Days).Days);
        var discount = 1 - (1 - EarlyEnergyRatio) * earlyDays / span;

        var seen = observed * discount;
        var seenSd = observedSd * discount;

        var priorWeight = 1 / (priorSd * priorSd);
        var seenWeight = 1 / (seenSd * seenSd);
        var rate = (prior * priorWeight + seen * seenWeight) / (priorWeight + seenWeight);
        var rateSd = Math.Sqrt(1 / (priorWeight + seenWeight));

        return new WeightState(
            level, levelSd, last, rate, rateSd, points.Length, first, observed, observedSd, discount, prior, priorSd);
    }

    /// <summary>The weight on a date: the loss carried forward, stopped at the goal. With its standard deviation.</summary>
    public static (double Projected, double Sd) At(WeightState state, double goalLb, LocalDate date)
    {
        var weeks = Weeks(state, date);
        var falling = state.LevelLb - state.RateLbPerWeek * weeks;
        var projected = state.LevelLb <= goalLb ? state.LevelLb : Math.Max(goalLb, falling);

        // Once the goal is reached the weight is held, so the spread stops
        // growing on the day it is reached.
        var moving = state.LevelLb > goalLb && state.RateLbPerWeek > 0
            ? Math.Min(weeks, (state.LevelLb - goalLb) / state.RateLbPerWeek)
            : state.LevelLb <= goalLb ? 0 : weeks;
        return (projected, Spread(state, moving));
    }

    /// <summary>The chance the weight has reached the goal by a date.</summary>
    public static double ChanceReached(WeightState state, double goalLb, LocalDate date)
    {
        var weeks = Weeks(state, date);
        var falling = state.LevelLb - state.RateLbPerWeek * weeks;
        return Linear.NormalCdf((goalLb - falling) / Spread(state, weeks));
    }

    /// <summary>The day the central projection reaches the goal; null when the rate is not a loss.</summary>
    public static LocalDate? ReachOn(WeightState state, double goalLb)
    {
        if (state.LevelLb <= goalLb) return state.LevelOn;
        if (state.RateLbPerWeek <= 0) return null;
        var days = (state.LevelLb - goalLb) / state.RateLbPerWeek * 7;
        return days > 3650 ? null : state.LevelOn.PlusDays((int)Math.Ceiling(days));
    }

    /// <summary>The arithmetic, line by line.</summary>
    public static IReadOnlyList<CalculationStep> Explain(
        WeightPlan plan, WeightState state, double goalLb, LocalDate date, (double Projected, double Sd) at, double chance)
    {
        var trace = new CalculationTrace();
        trace.Add("Plan", Text($"{plan.LbPerWeek:0.##} lb a week from {Day(plan.StartedOn)} to {goalLb:0.#} lb"), "your stated plan");
        trace.Add("Plan rate dieters reach",
            Text($"{plan.LbPerWeek:0.##} × 20.1 ÷ 27.6"),
            Text($"{state.PriorLbPerWeek:0.00} ± {state.PriorSd:0.00} lb a week"), Citations.ThomasWeightLoss.Id);

        if (state.ObservedLbPerWeek is { } observed && state.ObservedSd is { } observedSd)
        {
            trace.Add("Your rate so far",
                Text($"least squares through {state.WeighIns} weigh-ins, {Day(state.FirstWeighIn)} to {Day(state.LevelOn)}"),
                Text($"{observed:0.00} ± {observedSd:0.00} lb a week"), Citations.HelanderWeighIns.Id);
            trace.Add("Less early water",
                Text($"{observed:0.00} × {state.Discount:0.00}"),
                Text($"{observed * state.Discount:0.00} ± {observedSd * state.Discount:0.00} lb a week"), Citations.ThomasWeightLoss.Id);
            trace.Add("Rate from here", "the two rates, weighted by 1 ÷ spread²",
                Text($"{state.RateLbPerWeek:0.00} ± {state.RateSd:0.00} lb a week"));
        }
        else
        {
            trace.Add("Rate from here",
                Text($"the plan's rate alone: {state.WeighIns} weigh-in{(state.WeighIns == 1 ? "" : "s")} since the plan began, and a rate needs two that are {MinimumSpanDays} days apart"),
                Text($"{state.RateLbPerWeek:0.00} ± {state.RateSd:0.00} lb a week"));
        }

        trace.Add("Weight now",
            Text($"the weigh-ins since {Day(plan.StartedOn)}, read on {Day(state.LevelOn)}"),
            Text($"{state.LevelLb:0.0} ± {state.LevelSd:0.0} lb"), Citations.HelanderWeighIns.Id);

        if (ReachOn(state, goalLb) is { } reach && state.LevelLb > goalLb)
        {
            trace.Add(Text($"Reaches {goalLb:0.#} lb"),
                Text($"({state.LevelLb:0.0} − {goalLb:0.#}) ÷ {state.RateLbPerWeek:0.00} = {(state.LevelLb - goalLb) / state.RateLbPerWeek:0.0} weeks after {Day(state.LevelOn)}"),
                Day(reach));
        }

        var weeks = Weeks(state, date);
        trace.Add(Text($"On {Day(date)}"),
            Text($"{state.LevelLb:0.0} − {state.RateLbPerWeek:0.00} × {weeks:0.0} weeks = {state.LevelLb - state.RateLbPerWeek * weeks:0.0}, held at the goal once reached"),
            Text($"{at.Projected:0.0} lb"));
        trace.Add("Chance it is reached by then",
            Text($"spread of the falling weight √({state.LevelSd:0.0}² + ({state.RateSd:0.00} × {weeks:0.0})²) = {Spread(state, weeks):0.0} lb"),
            Text($"{chance:0%}"));
        return trace.Steps;
    }

    private static double Weeks(WeightState state, LocalDate date) =>
        Math.Max(0, Period.Between(state.LevelOn, date, PeriodUnits.Days).Days) / 7.0;

    private static double Spread(WeightState state, double weeks) =>
        Math.Sqrt(state.LevelSd * state.LevelSd + weeks * state.RateSd * weeks * state.RateSd);

    internal static string Day(LocalDate date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Body fat on a date, from the latest reading and the weight the plan
/// reaches.
/// </summary>
/// <remarks>
/// Of the weight dieters lost while resistance training, 17% ± 14% was lean
/// tissue and the rest fat (Heymsfield 2014, pooling 44 overweight adults
/// from Chaston 2007). So fat mass falls by 83% of the weight lost since
/// the reading, and the percentage is that fat over the new weight. Leaner
/// men lose a larger lean share: Ranger students at 15% fat lost 40% lean
/// on extreme restriction (same review). The reading itself is the largest
/// unknown: the forecast moves point for point with it.
///
/// Citation: <see cref="Citations.HeymsfieldWeightLoss"/>.
/// </remarks>
public static class BodyFatForecast
{
    /// <summary>Heymsfield 2014: the lean share of weight lost with resistance training.</summary>
    public const double LeanShare = 0.17;

    public const double LeanShareSd = 0.14;

    public const string Model =
        "Your latest body fat reading and the weight your plan reaches: 83% of the weight lost is fat when you lift through it (Heymsfield 2014)";

    /// <summary>Body fat at a weight, in percent.</summary>
    public static double At(BodyFatReading reading, double weightLb, double leanShare = LeanShare)
    {
        var fat = reading.Percent / 100 * reading.WeightLb - (1 - leanShare) * (reading.WeightLb - weightLb);
        return Math.Max(0, fat) / weightLb * 100;
    }

    /// <summary>Body fat at a projected weight, with the spread the lean share and the weight add.</summary>
    public static (double Projected, double Sd) At(BodyFatReading reading, (double Projected, double Sd) weight)
    {
        var center = At(reading, weight.Projected);
        var byShare = At(reading, weight.Projected, LeanShare + LeanShareSd) - center;
        var byWeight = At(reading, weight.Projected + weight.Sd) - center;
        return (center, Math.Sqrt(byShare * byShare + byWeight * byWeight));
    }

    public static IReadOnlyList<CalculationStep> Explain(
        BodyFatReading reading, (double Projected, double Sd) weight, (double Projected, double Sd) at, LocalDate date, double targetPercent, double chance)
    {
        var fat = reading.Percent / 100 * reading.WeightLb;
        var lost = reading.WeightLb - weight.Projected;
        var trace = new CalculationTrace();
        trace.Add("Fat at the reading",
            Text($"{reading.Percent:0.#}% × {reading.WeightLb:0.0} lb on {WeightForecast.Day(reading.Date)}"),
            Text($"{fat:0.0} lb"));
        trace.Add(Text($"Weight on {WeightForecast.Day(date)}"), "from the Bodyweight row", Text($"{weight.Projected:0.0} ± {weight.Sd:0.0} lb"));
        trace.Add("Fat lost",
            Text($"({reading.WeightLb:0.0} − {weight.Projected:0.0}) × (1 − {LeanShare:0.00})"),
            Text($"{(1 - LeanShare) * lost:0.0} lb"), Citations.HeymsfieldWeightLoss.Id);
        trace.Add("Body fat then",
            Text($"({fat:0.0} − {(1 - LeanShare) * lost:0.0}) ÷ {weight.Projected:0.0}"),
            Text($"{at.Projected:0.0}% ± {at.Sd:0.0}"));
        trace.Add(Text($"Chance of {targetPercent:0.#}% by then"),
            Text($"({targetPercent:0.#} − {at.Projected:0.0}) ÷ {at.Sd:0.0} = {(targetPercent - at.Projected) / at.Sd:0.0} spreads"),
            Text($"{chance:0%}"));
        trace.Add(Text($"Fat to lose for {targetPercent:0.#}% at {weight.Projected:0.0} lb"),
            Text($"{fat - (1 - LeanShare) * lost:0.0} − {targetPercent / 100:0.00} × {weight.Projected:0.0}"),
            Text($"{fat - (1 - LeanShare) * lost - targetPercent / 100 * weight.Projected:0.0} lb"));
        return trace.Steps;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
