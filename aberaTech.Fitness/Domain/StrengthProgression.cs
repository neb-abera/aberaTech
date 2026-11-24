using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>
/// What a lift is likely to be on a date if training continues: today's max,
/// carried forward at the rate lifters of his level gain.
/// </summary>
/// <remarks>
/// The projection starts from today's estimate and never from a line through
/// older ones. On 2026-11-21 a straight line through every bench estimate
/// since April read his 155 lb × 10 (207 lb) as the end of a slide and
/// projected 205 lb for December 2027. A trained lifter who keeps training
/// does not get weaker, so the old readings say nothing about the slope.
///
/// An old reading still counts as a ceiling he has held. Strength lost to
/// time off comes back within 8 weeks (Blocquiaux 2020: men who lost 5 to
/// 15% in 12 weeks off regained their 1RM in under 8 weeks of retraining).
/// So the projection climbs to the best estimate in the log over 8 weeks,
/// then gains from there.
///
/// The rate past the old best is 12% in the first year, with a spread of
/// 6 points. Two studies of drug-tested powerlifters bracket it. Across
/// 9,259 lifters the first year gave 7.5 to 12.5% (Latella 2024). The
/// weakest quartile of 1,271 men, whose totals were 4.57 times bodyweight,
/// gained 17.2% a year (Latella 2020), a figure taken from each lifter's
/// best total and so biased upward. On 2026-11-24 his total was about 4.3
/// times bodyweight, below that quartile. One spread either side of 12% runs 6 to
/// 18% and holds both figures. The 6 points are a judgment and no study
/// reports them. On 2026-11-24 the rate was 8.8% a year: the mean of all
/// 1,271 lifters, cut by a ratio of effect sizes from Wilson 2012. That gave
/// his 247.5 lb bench a 2% chance, and he called it crazy.
///
/// After the first year the gain slows. Latella 2024 found up to 20% after
/// ten years against 7.5 to 12.5% after one. Taking 10% for the first year,
/// the gain after t years is the first year's times 1 + 0.43 × ln(t).
///
/// Running takes nothing off. Concurrent training left maximal lower-body
/// strength unchanged (Schumann 2022: standardized difference −0.06, 95% CI
/// −0.20 to 0.09), and that review measured no bench press. A calorie
/// deficit takes nothing off either (Murphy and Koehler 2022: effect on
/// strength −0.31, p = 0.28).
///
/// The spread has three parts: the rate; the estimate itself, ±3.5%
/// (Reynolds 2006, standard error of 2.98 kg from a 5RM chest press); and
/// 1% for each month the estimate is old (Häkkinen 2000: 1RM fell 6% over
/// 24 weeks off).
///
/// Citations: <see cref="Citations.LatellaStrength"/>, <see cref="Citations.LatellaTimeCourse"/>,
/// <see cref="Citations.SchumannConcurrent"/>, <see cref="Citations.MurphyDeficit"/>,
/// <see cref="Citations.StrengthRetraining"/>, <see cref="Citations.ReynoldsRepMax"/>.
/// </remarks>
public static class StrengthProgression
{
    /// <summary>The gain past the old best in the first year, a fraction.</summary>
    public const double GainPerYear = 0.12;

    /// <summary>The spread of that rate between lifters, in the same unit. A judgment: 6 to 18% holds both published figures.</summary>
    public const double GainSpread = 0.06;

    /// <summary>Latella 2024: what each later year adds, as a share of the first year's gain per ln(years).</summary>
    public static readonly double LaterYears = (20.0 / 10 - 1) / Math.Log(10);

    /// <summary>Reynolds 2006: the error of a one-rep max estimated from a set, a fraction of it.</summary>
    public const double EstimateError = 0.035;

    /// <summary>Häkkinen 2000: strength lost per month without training, used as the spread an old estimate adds.</summary>
    public const double StalenessPerMonth = 0.01;

    /// <summary>Blocquiaux 2020: months to regain a 1RM lost to time off (8 weeks).</summary>
    public const double ReclaimMonths = 8 * 7 / 30.4375;

    /// <summary>How far ahead the reach date is searched.</summary>
    public const double HorizonMonths = 120;

    public const string Model =
        "Progression from today's max: back to your best logged estimate within 8 weeks (Blocquiaux 2020), then 12% ± 6 in the first year and slower after, from drug-tested powerlifters at your level (Latella 2020, Latella 2024). Running and a calorie deficit take nothing off (Schumann 2022, Murphy and Koehler 2022)";

    /// <summary>The gain past the old best after <paramref name="years"/> of training, as a multiple of the yearly rate.</summary>
    internal static double Years(double years) =>
        years <= 1 ? Math.Max(0, years) : 1 + LaterYears * Math.Log(years);

    /// <summary>The lift at <paramref name="months"/> from now, and its standard deviation.</summary>
    /// <param name="current">Today's estimate, in the line's unit.</param>
    /// <param name="best">The best estimate in the log, in the same unit; null or lower than today when today is the best.</param>
    /// <param name="ageMonths">How old today's estimate is.</param>
    public static (double Projected, double Sd) At(double current, double? best, double months, double ageMonths)
    {
        if (current <= 0) throw new ArgumentOutOfRangeException(nameof(current));
        months = Math.Max(0, months);

        var ceiling = Math.Max(current, best ?? current);
        var reclaim = ceiling > current ? ReclaimMonths : 0;

        double projected;
        double rate;
        if (months <= reclaim)
        {
            projected = current + (ceiling - current) * months / reclaim;
            rate = 0;
        }
        else
        {
            var years = Years((months - reclaim) / 12);
            projected = ceiling * (1 + GainPerYear * years);
            rate = ceiling * GainSpread * years;
        }

        var estimate = EstimateError * projected;
        var stale = StalenessPerMonth * Math.Max(0, ageMonths) * current;
        var sd = Math.Sqrt(estimate * estimate + rate * rate + stale * stale);
        return (projected, sd);
    }

    /// <summary>Months until the central projection reaches a target; null when it does not within <see cref="HorizonMonths"/>.</summary>
    public static double? MonthsToReach(double current, double? best, double target)
    {
        for (var m = 0.0; m <= HorizonMonths; m += 0.25)
        {
            if (At(current, best, m, 0).Projected >= target) return m;
        }

        return null;
    }

    /// <summary>The arithmetic behind a projection, in one sentence.</summary>
    public static string Explain(
        double current, double? best, (double Projected, double Sd) at, string unit, Func<string, double, string> describe)
    {
        var (projected, sd) = at;
        var ceiling = Math.Max(current, best ?? current);
        var climb = ceiling > current
            ? Text($"back to your best logged {describe(unit, ceiling)} within 8 weeks, then ")
            : "";
        return Text(
            $"From today's {describe(unit, current)}: {climb}{GainPerYear:0%} in the first year projects {describe(unit, projected)} ± {describe(unit, sd)} at the date, if you keep training the lift.");
    }

    /// <summary>The arithmetic, line by line.</summary>
    public static IReadOnlyList<CalculationStep> Steps(
        double current, double? best, double target, double months, double ageMonths,
        (double Projected, double Sd) at, double chance, string unit, Func<string, double, string> describe)
    {
        var ceiling = Math.Max(current, best ?? current);
        var reclaim = ceiling > current ? ReclaimMonths : 0;
        var training = Math.Max(0, months - reclaim);
        var years = Years(training / 12);
        var trace = new CalculationTrace();

        trace.Add("Today", "your latest max or estimate", describe(unit, current));
        if (ceiling > current)
        {
            trace.Add("Back to your best", Text($"{describe(unit, ceiling)} within 8 weeks ({ReclaimMonths:0.0} months)"),
                describe(unit, ceiling), Citations.StrengthRetraining.Id);
        }

        trace.Add("Months of new gain", Text($"{months:0.0} − {reclaim:0.0}"), Text($"{training:0.0} months"));
        trace.Add("Gain in that time",
            training <= 12
                ? Text($"{GainPerYear:0%} a year × {training:0.0} ÷ 12")
                : Text($"{GainPerYear:0%} × (1 + {LaterYears:0.00} × ln({training / 12:0.00} years))"),
            Text($"{GainPerYear * years:0.0%} ± {GainSpread * years:0.0%}"),
            training <= 12 ? Citations.LatellaStrength.Id : Citations.LatellaTimeCourse.Id);
        trace.Add("On the date",
            Text($"{describe(unit, ceiling)} × (1 + {GainPerYear * years:0.000})"),
            describe(unit, at.Projected));

        var rate = ceiling * GainSpread * years;
        var estimate = EstimateError * at.Projected;
        var stale = StalenessPerMonth * Math.Max(0, ageMonths) * current;
        trace.Add("Spread",
            Text($"√(rate {Number(unit, rate)}² + estimate {Number(unit, estimate)}² + age {Number(unit, stale)}²)"),
            Text($"± {describe(unit, at.Sd)}"), Citations.ReynoldsRepMax.Id);
        trace.Add("Needed",
            Text($"{describe(unit, target)} ÷ {describe(unit, ceiling)} − 1 = {target / ceiling - 1:0.0%} past your best, {(training > 0 ? (target / ceiling - 1) / (training / 12) : 0):0.0%} a year"),
            Text($"against {GainPerYear:0%} ± {GainSpread:0%}"));
        trace.Add("Chance by the date",
            Text($"({Number(unit, at.Projected)} − {Number(unit, target)}) ÷ {Number(unit, at.Sd)} = {(at.Projected - target) / at.Sd:0.00} spreads"),
            Text($"{chance:0%}"));
        return trace.Steps;
    }

    private static string Number(string unit, double value) =>
        unit == SelectionReadiness.Units.Bodyweights ? Text($"{value:0.00}") : Text($"{value:0.0}");

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
