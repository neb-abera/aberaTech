using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>
/// What a lift is likely to be on a date if training continues: today's max,
/// carried forward at the rate trained lifters gain.
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
/// 15% in 12 weeks off regained their 1RM in under 8 weeks of retraining;
/// Ogasawara 2011: after 3 weeks off, bench gains matched the first 6 weeks).
/// So the projection climbs to the best estimate in the log over 8 weeks,
/// then gains from there at the trained rate.
///
/// The trained rate is 10.7% a year, the mean of 1,271 men's raw powerlifting
/// totals over 15 years (Latella 2020), times 1.44 ÷ 1.76 for concurrent
/// running (Wilson 2012), so 8.8% a year. The spread has three parts: the
/// rate, ±35% (Latella's weakest and strongest quartiles, 0.211 and 0.102 kg
/// a day around 0.150); the estimate itself, ±3.5% (Reynolds 2006, standard
/// error of 2.98 kg from a 5RM chest press); and 1% for each month the
/// estimate is old (Häkkinen 2000: 1RM fell 6% over 24 weeks off).
///
/// Citations: <see cref="Citations.LatellaStrength"/>, <see cref="Citations.WilsonConcurrent"/>,
/// <see cref="Citations.StrengthRetraining"/>, <see cref="Citations.ReynoldsRepMax"/>.
/// </remarks>
public static class StrengthProgression
{
    /// <summary>Latella 2020: mean gain of raw powerlifting totals, a fraction a year.</summary>
    public const double TrainedGainPerYear = 0.107;

    /// <summary>Wilson 2012: strength effect size 1.44 with running against 1.76 without.</summary>
    public const double ConcurrentFactor = 1.44 / 1.76;

    /// <summary>The rate a lift gains at past its old best, a fraction a year.</summary>
    public const double GainPerYear = TrainedGainPerYear * ConcurrentFactor;

    /// <summary>Latella 2020: the quartiles' rates sit about 35% either side of the mean.</summary>
    public const double RateSpread = 0.35;

    /// <summary>Reynolds 2006: the error of a one-rep max estimated from a set, a fraction of it.</summary>
    public const double EstimateError = 0.035;

    /// <summary>Häkkinen 2000: strength lost per month without training, used as the spread an old estimate adds.</summary>
    public const double StalenessPerMonth = 0.01;

    /// <summary>Blocquiaux 2020: months to regain a 1RM lost to time off (8 weeks).</summary>
    public const double ReclaimMonths = 8 * 7 / 30.4375;

    public const string Model =
        "Trained-lifter progression from today's max: back to your best logged estimate within 8 weeks (Blocquiaux 2020), then 8.8% a year, the 10.7% of Latella 2020 scaled for concurrent running by Wilson 2012";

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
        double gained;
        if (months <= reclaim)
        {
            projected = current + (ceiling - current) * months / reclaim;
            gained = 0;
        }
        else
        {
            projected = ceiling * Math.Pow(1 + GainPerYear, (months - reclaim) / 12);
            gained = projected - ceiling;
        }

        var estimate = EstimateError * projected;
        var rate = RateSpread * gained;
        var stale = StalenessPerMonth * Math.Max(0, ageMonths) * current;
        var sd = Math.Sqrt(estimate * estimate + rate * rate + stale * stale);
        return (projected, sd);
    }

    /// <summary>The arithmetic behind a projection, for the evidence line.</summary>
    public static string Explain(
        double current, double? best, (double Projected, double Sd) at, string unit, Func<string, double, string> describe)
    {
        var (projected, sd) = at;
        var ceiling = Math.Max(current, best ?? current);
        var climb = ceiling > current
            ? Text($"back to your best logged {describe(unit, ceiling)} within 8 weeks, then ")
            : "";
        return Text(
            $"From today's {describe(unit, current)}: {climb}{GainPerYear:0.0%} a year projects {describe(unit, projected)} ± {describe(unit, sd)} at the date, if you keep training the lift.");
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
