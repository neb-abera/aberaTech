using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One week's median pace at the reference heart rate, from its steady runs.</summary>
public sealed record WeekPace(LocalDate WeekStart, double MedianSecPerKm, int Runs);

/// <summary>A projected pace and its 80% range.</summary>
public sealed record PacePoint(int WeeksAhead, double SecPerKm, double LowSecPerKm, double HighSecPerKm);

/// <summary>The short-term forecast, or what it still needs.</summary>
public sealed record AerobicOutlookResult(
    bool Ready,
    int QualifyingWeeks,
    IReadOnlyList<WeekPace> Weeks,
    double? SlopeSecPerKmPerWeek,
    IReadOnlyList<PacePoint> Ahead);

/// <summary>
/// A short-term forecast that works on a few weeks of training: pace at the
/// AeT heart rate, week by week, carried 4 and 8 weeks ahead on a straight
/// line with its 80% prediction range.
/// </summary>
/// <remarks>
/// Pace at a fixed heart rate is Evoke's own measure of improvement, so the
/// line is fitted to what the plan says should improve. A straight line is
/// the least a few points can support. It says where the last weeks point,
/// and its range widens with how much the weeks scatter around it. It is an
/// estimate from training runs and is labeled so. It never sets a threshold.
/// </remarks>
public static class AerobicOutlook
{
    /// <summary>Weeks looked back over.</summary>
    public const int WindowWeeks = 12;

    /// <summary>Steady runs a week needs to count.</summary>
    public const int RunsPerWeek = 3;

    /// <summary>Counting weeks the line needs.</summary>
    public const int WeeksNeeded = 6;

    public static readonly int[] Horizons = [4, 8];

    // Student's t at 0.90, by degrees of freedom: the two-sided 80% range.
    private static readonly double[] T90 = [0, 3.078, 1.886, 1.638, 1.533, 1.476, 1.440, 1.415, 1.397, 1.383, 1.372, 1.363, 1.356];

    public static AerobicOutlookResult Build(IEnumerable<SteadyRun> runs, int referenceHr, LocalDate today)
    {
        var thisWeek = today.PlusDays(-(((int)today.DayOfWeek - 1 + 7) % 7));
        var first = thisWeek.PlusWeeks(-(WindowWeeks - 1));

        var weeks = runs
            .Where(r => r.Seconds >= AerobicAnalysis.MinimumSeconds && r.AverageHr > 0 && r.DistanceMeters > 0 && r.Date >= first && r.Date <= today)
            .GroupBy(r => r.Date.PlusDays(-(((int)r.Date.DayOfWeek - 1 + 7) % 7)))
            .Select(g =>
            {
                var paces = g.Select(r => AerobicAnalysis.NormalizedSecPerKm(r.DistanceMeters, r.Seconds, r.AverageHr, referenceHr)).OrderBy(x => x).ToArray();
                var n = paces.Length;
                var median = n % 2 == 1 ? paces[n / 2] : (paces[n / 2 - 1] + paces[n / 2]) / 2;
                return new WeekPace(g.Key, median, n);
            })
            .OrderBy(w => w.WeekStart)
            .ToArray();

        var counting = weeks.Where(w => w.Runs >= RunsPerWeek).ToArray();
        if (counting.Length < WeeksNeeded) return new AerobicOutlookResult(false, counting.Length, weeks, null, []);

        // Least squares of pace on weeks since the window opened.
        var x = counting.Select(w => (double)Period.Between(first, w.WeekStart, PeriodUnits.Weeks).Weeks).ToArray();
        var y = counting.Select(w => w.MedianSecPerKm).ToArray();
        var n2 = x.Length;
        var xBar = x.Average();
        var yBar = y.Average();
        var sxx = x.Sum(v => (v - xBar) * (v - xBar));
        var slope = x.Zip(y).Sum(p => (p.First - xBar) * (p.Second - yBar)) / sxx;
        var intercept = yBar - slope * xBar;
        var sse = x.Zip(y).Sum(p => Math.Pow(p.Second - (intercept + slope * p.First), 2));
        var s = Math.Sqrt(sse / (n2 - 2));
        var t = T90[Math.Min(n2 - 2, T90.Length - 1)];

        var now = (double)Period.Between(first, thisWeek, PeriodUnits.Weeks).Weeks;
        var ahead = Horizons.Select(h =>
        {
            var x0 = now + h;
            var fit = intercept + slope * x0;
            var half = t * s * Math.Sqrt(1 + 1.0 / n2 + Math.Pow(x0 - xBar, 2) / sxx);
            return new PacePoint(h, fit, fit - half, fit + half);
        }).ToArray();

        return new AerobicOutlookResult(true, counting.Length, weeks, slope, ahead);
    }
}
