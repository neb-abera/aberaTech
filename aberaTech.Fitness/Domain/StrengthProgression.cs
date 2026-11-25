using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>
/// What a lift is likely to be on a date if training continues: today's max,
/// carried forward at the rate people at his level gain.
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
/// Two populations have published rates, and which one a lift belongs to
/// depends on where it stands. The line between them is the level at which
/// competing lifters start: the 10th percentile of drug-tested male
/// powerlifters aged 18 to 35, which is 1.75 × bodyweight in the squat,
/// 1.19 in the bench press, and 2.03 in the deadlift (van den Hoek 2024,
/// Table 4). The young-adult table is used at every age, because his
/// standards are not normed for age.
///
/// Below that level a lift gains as a beginner's does: 30 to 50% of where
/// it started in the first year, and 50 to 60% after six (Steele 2023,
/// 14,690 adults training once a week). The center is 40% and the spread
/// 10 points. From that level on it gains as a competing lifter's does:
/// 12% in the first year, with a spread of 6 points. Two studies bracket
/// that. Across powerlifters by age and weight class the first year gave
/// 7.5 to 12.5% (Latella 2024). The weakest quartile of 1,271 men, whose
/// totals were 4.57 times bodyweight, gained 17.2% a year (Latella 2020), a
/// figure taken from each lifter's best total and so biased upward. One
/// spread either side of 12% runs 6 to 18% and holds both figures. The
/// 6 points are a judgment and no study reports them.
///
/// On 2026-11-24 every lift was on the lifters' curve, with their slowdown
/// after the first year: up to 20% after ten years. His squat, best 221 lb
/// at 170 lb, is 1.30 × bodyweight, far below where those lifters start. The
/// curve capped it near 274 lb and the page said 315 lb was not reached in
/// ten years. 315 lb at 165 lb is 1.91 × bodyweight, under the 20th
/// percentile of the same lifters. He asked why the page thought he could
/// never squat it. A mean curve of people who start stronger says nothing
/// about a lift that has not reached their starting line.
///
/// After the first year each curve slows. Lifters: up to 20% after ten
/// years against 10% after one, so the gain after t years is the first
/// year's times 1 + 0.43 × ln(t). Beginners: 55% after six years against
/// 40% after one, so 1 + 0.21 × ln(t).
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
/// Citations: <see cref="Citations.SteeleBeginners"/>, <see cref="Citations.VanDenHoekNorms"/>,
/// <see cref="Citations.LatellaStrength"/>, <see cref="Citations.LatellaTimeCourse"/>,
/// <see cref="Citations.SchumannConcurrent"/>, <see cref="Citations.MurphyDeficit"/>,
/// <see cref="Citations.StrengthRetraining"/>, <see cref="Citations.ReynoldsRepMax"/>.
/// </remarks>
public static class StrengthProgression
{
    /// <summary>A competing lifter's gain past his best in the first year, a fraction.</summary>
    public const double GainPerYear = 0.12;

    /// <summary>The spread of that rate between lifters, in the same unit. A judgment: 6 to 18% holds both published figures.</summary>
    public const double GainSpread = 0.06;

    /// <summary>Latella 2024: what each later year adds, as a share of the first year's gain per ln(years).</summary>
    public static readonly double LaterYears = (20.0 / 10 - 1) / Math.Log(10);

    /// <summary>Steele 2023: a beginner's gain in the first year, a fraction of where the lift started (30 to 50%).</summary>
    public const double BeginnerGainPerYear = 0.40;

    /// <summary>Steele 2023: the half-width of that range.</summary>
    public const double BeginnerGainSpread = 0.10;

    /// <summary>Steele 2023: 50 to 60% after six years against 30 to 50% after one.</summary>
    public static readonly double BeginnerLaterYears = (55.0 / 40 - 1) / Math.Log(6);

    /// <summary>van den Hoek 2024, Table 4: the 10th percentile of drug-tested male lifters aged 18 to 35, as one-rep max over bodyweight.</summary>
    public const double SquatEntry = 1.75;

    public const double BenchEntry = 1.19;

    public const double DeadliftEntry = 2.03;

    /// <summary>Reynolds 2006: the error of a one-rep max estimated from a set, a fraction of it.</summary>
    public const double EstimateError = 0.035;

    /// <summary>Häkkinen 2000: strength lost per month without training, used as the spread an old estimate adds.</summary>
    public const double StalenessPerMonth = 0.01;

    /// <summary>Blocquiaux 2020: months to regain a 1RM lost to time off (8 weeks).</summary>
    public const double ReclaimMonths = 8 * 7 / 30.4375;

    /// <summary>How far ahead the reach date is searched.</summary>
    public const double HorizonMonths = 120;

    public const string Model =
        "Progression from today's max: back to your best logged estimate within 8 weeks (Blocquiaux 2020). Below the level competing lifters start at (van den Hoek 2024), 40% ± 10 in the first year, as beginners gain (Steele 2023). From that level on, 12% ± 6 in the first year, as drug-tested powerlifters gain (Latella 2020, Latella 2024). Both slow after a year. Running and a calorie deficit take nothing off (Schumann 2022, Murphy and Koehler 2022)";

    /// <summary>A competing lifter's gain after <paramref name="years"/>, as a multiple of his yearly rate.</summary>
    internal static double Years(double years) =>
        years <= 1 ? Math.Max(0, years) : 1 + LaterYears * Math.Log(years);

    /// <summary>A beginner's gain after <paramref name="years"/>, as a multiple of his yearly rate.</summary>
    internal static double BeginnerYears(double years) =>
        years <= 1 ? Math.Max(0, years) : 1 + BeginnerLaterYears * Math.Log(years);

    /// <summary>The years a beginner takes to gain a fraction of where he started; infinite when the curve never gets there.</summary>
    internal static double BeginnerYearsFor(double gain)
    {
        var multiple = gain / BeginnerGainPerYear;
        if (multiple <= 1) return Math.Max(0, multiple);
        var years = Math.Exp((multiple - 1) / BeginnerLaterYears);
        return years > HorizonMonths / 12 ? double.PositiveInfinity : years;
    }

    /// <summary>How a projection was put together, for the page to show.</summary>
    /// <param name="Ceiling">The best estimate held: the start of new gain.</param>
    /// <param name="ReclaimMonths">Months spent getting back to it.</param>
    /// <param name="BeginnerMonths">Months gaining as a beginner, below the lifters' entry level.</param>
    /// <param name="LifterMonths">Months gaining as a competing lifter, from the entry level or from his best.</param>
    /// <param name="LifterStart">The level the lifter phase starts from.</param>
    /// <param name="RateSd">The spread the rates add.</param>
    public sealed record Path(
        double Ceiling, double ReclaimMonths, double BeginnerMonths, double LifterMonths, double LifterStart, double RateSd);

    /// <summary>The lift at <paramref name="months"/> from now, and its standard deviation.</summary>
    /// <param name="current">Today's estimate, in the line's unit.</param>
    /// <param name="best">The best estimate in the log, in the same unit; null or lower than today when today is the best.</param>
    /// <param name="ageMonths">How old today's estimate is.</param>
    /// <param name="entry">The level competing lifters start at, in the line's unit; null when no table gives one.</param>
    public static (double Projected, double Sd) At(double current, double? best, double months, double ageMonths, double? entry = null)
    {
        var (projected, path) = Walk(current, best, months, entry);
        var estimate = EstimateError * projected;
        var stale = StalenessPerMonth * Math.Max(0, ageMonths) * current;
        return (projected, Math.Sqrt(estimate * estimate + path.RateSd * path.RateSd + stale * stale));
    }

    /// <summary>The projection with the phases it passed through.</summary>
    public static (double Projected, Path Path) Walk(double current, double? best, double months, double? entry = null)
    {
        if (current <= 0) throw new ArgumentOutOfRangeException(nameof(current));
        months = Math.Max(0, months);

        var ceiling = Math.Max(current, best ?? current);
        var reclaim = ceiling > current ? ReclaimMonths : 0;
        if (months <= reclaim)
        {
            return (current + (ceiling - current) * months / reclaim, new Path(ceiling, months, 0, 0, ceiling, 0));
        }

        var years = (months - reclaim) / 12;

        // At or past the lifters' entry level, or with no table for the lift.
        if (entry is not { } line || ceiling >= line)
        {
            return (ceiling * (1 + GainPerYear * Years(years)),
                new Path(ceiling, reclaim, 0, years * 12, ceiling, ceiling * GainSpread * Years(years)));
        }

        // Below it: a beginner's curve up to the line, then a lifter's from it.
        var crossing = BeginnerYearsFor(line / ceiling - 1);
        if (years <= crossing)
        {
            return (ceiling * (1 + BeginnerGainPerYear * BeginnerYears(years)),
                new Path(ceiling, reclaim, years * 12, 0, line, ceiling * BeginnerGainSpread * BeginnerYears(years)));
        }

        var after = years - crossing;
        var beginnerSd = ceiling * BeginnerGainSpread * BeginnerYears(crossing);
        var lifterSd = line * GainSpread * Years(after);
        return (line * (1 + GainPerYear * Years(after)),
            new Path(ceiling, reclaim, crossing * 12, after * 12, line, Math.Sqrt(beginnerSd * beginnerSd + lifterSd * lifterSd)));
    }

    /// <summary>Months until the central projection reaches a target; null when it does not within <see cref="HorizonMonths"/>.</summary>
    public static double? MonthsToReach(double current, double? best, double target, double? entry = null)
    {
        for (var m = 0.0; m <= HorizonMonths; m += 0.25)
        {
            if (Walk(current, best, m, entry).Projected >= target) return m;
        }

        return null;
    }

    /// <summary>The arithmetic behind a projection, in one sentence.</summary>
    public static string Explain(
        double current, double? best, (double Projected, double Sd) at, string unit, Func<string, double, string> describe,
        double? entry = null)
    {
        var (projected, sd) = at;
        var ceiling = Math.Max(current, best ?? current);
        var climb = ceiling > current
            ? Text($"back to your best logged {describe(unit, ceiling)} within 8 weeks, then ")
            : "";
        var rate = entry is { } line && ceiling < line
            ? Text($"{BeginnerGainPerYear:0%} in the first year, as a beginner gains, up to the {describe(unit, line)} competing lifters start at, then {GainPerYear:0%}")
            : Text($"{GainPerYear:0%} in the first year");
        return Text(
            $"From today's {describe(unit, current)}: {climb}{rate} projects {describe(unit, projected)} ± {describe(unit, sd)} at the date, if you keep training the lift.");
    }

    /// <summary>The arithmetic, line by line.</summary>
    public static IReadOnlyList<CalculationStep> Steps(
        double current, double? best, double target, double months, double ageMonths,
        (double Projected, double Sd) at, double chance, string unit, Func<string, double, string> describe,
        double? entry = null)
    {
        var (_, path) = Walk(current, best, months, entry);
        var ceiling = path.Ceiling;
        var trace = new CalculationTrace();

        trace.Add("Today", "your latest max or estimate", describe(unit, current));
        if (ceiling > current)
        {
            trace.Add("Back to your best", Text($"{describe(unit, ceiling)} within 8 weeks ({ReclaimMonths:0.0} months)"),
                describe(unit, ceiling), Citations.StrengthRetraining.Id);
        }

        var training = Math.Max(0, months - path.ReclaimMonths);
        trace.Add("Months of new gain", Text($"{months:0.0} − {path.ReclaimMonths:0.0}"), Text($"{training:0.0} months"));

        if (entry is { } line)
        {
            trace.Add("Where competing lifters start",
                "the 10th percentile of drug-tested men 18 to 35, at your bodyweight",
                describe(unit, line), Citations.VanDenHoekNorms.Id);
        }

        if (entry is { } start && ceiling < start)
        {
            var beginnerYears = BeginnerYears(path.BeginnerMonths / 12);
            trace.Add("Below it, a beginner's gain",
                path.BeginnerMonths <= 12
                    ? Text($"{describe(unit, ceiling)} × (1 + {BeginnerGainPerYear:0%} × {path.BeginnerMonths:0.0} ÷ 12)")
                    : Text($"{describe(unit, ceiling)} × (1 + {BeginnerGainPerYear:0%} × (1 + {BeginnerLaterYears:0.00} × ln({path.BeginnerMonths / 12:0.00} years)))"),
                Text($"{describe(unit, ceiling * (1 + BeginnerGainPerYear * beginnerYears))} after {path.BeginnerMonths:0.0} months, ± {BeginnerGainSpread:0%} a year"),
                Citations.SteeleBeginners.Id);
        }

        if (path.LifterMonths > 0)
        {
            var lifterYears = Years(path.LifterMonths / 12);
            trace.Add(entry is { } from && ceiling < from ? "From it, a lifter's gain" : "A lifter's gain",
                path.LifterMonths <= 12
                    ? Text($"{describe(unit, path.LifterStart)} × (1 + {GainPerYear:0%} × {path.LifterMonths:0.0} ÷ 12)")
                    : Text($"{describe(unit, path.LifterStart)} × (1 + {GainPerYear:0%} × (1 + {LaterYears:0.00} × ln({path.LifterMonths / 12:0.00} years)))"),
                Text($"{describe(unit, path.LifterStart * (1 + GainPerYear * lifterYears))}, ± {GainSpread:0%} a year"),
                path.LifterMonths <= 12 ? Citations.LatellaStrength.Id : Citations.LatellaTimeCourse.Id);
        }

        trace.Add("On the date", "the phases above", describe(unit, at.Projected));

        var estimate = EstimateError * at.Projected;
        var stale = StalenessPerMonth * Math.Max(0, ageMonths) * current;
        trace.Add("Spread",
            Text($"√(rate {Number(unit, path.RateSd)}² + estimate {Number(unit, estimate)}² + age {Number(unit, stale)}²)"),
            Text($"± {describe(unit, at.Sd)}"), Citations.ReynoldsRepMax.Id);
        trace.Add("Needed",
            Text($"{describe(unit, target)} ÷ {describe(unit, ceiling)} − 1"),
            Text($"{target / ceiling - 1:0.0%} past your best"));
        trace.Add("Chance by the date",
            Text($"({Number(unit, at.Projected)} − {Number(unit, target)}) ÷ {Number(unit, at.Sd)} = {(at.Projected - target) / at.Sd:0.00} spreads"),
            Text($"{chance:0%}"));
        return trace.Steps;
    }

    private static string Number(string unit, double value) =>
        unit == SelectionReadiness.Units.Bodyweights ? Text($"{value:0.00}") : Text($"{value:0.0}");

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
