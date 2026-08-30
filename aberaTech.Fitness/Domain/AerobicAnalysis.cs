using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>A steady run reduced to what the aerobic analysis needs.</summary>
public sealed record SteadyRun(LocalDate Date, double DistanceMeters, double Seconds, int AverageHr);

/// <summary>One month's aerobic fitness, as the median normalized pace.</summary>
public sealed record MonthlyAerobicPoint(int Year, int Month, double MedianNormalizedSecPerKm, int RunCount);

/// <summary>
/// Aerobic-base analysis: pace-at-heart-rate over time, and the aerobic
/// deficiency check.
/// </summary>
/// <remarks>
/// Comparing runs at different efforts requires a common denominator. Heart
/// rate is close to linear in running speed through the aerobic range (the
/// relationship exercise testing has leaned on since Åstrand), so each run's
/// pace is scaled to a reference heart rate: normalized pace = pace × HR/ref.
/// Falling normalized pace at a fixed reference HR is the cleanest field
/// evidence the aerobic engine is growing — it is the same signal a monthly
/// aerobic-threshold test measures, computed from every steady run instead of
/// one.
///
/// The deficiency check is the Uphill Athlete / Evoke 10% rule: when pace at
/// the aerobic threshold is more than about 10% slower than pace at the
/// lactate threshold, the aerobic system is underdeveloped relative to the
/// anaerobic one, and base volume is the prescription (Johnston; Kuenzle,
/// Paikowski &amp; Johnston). Zone-2 base building as the fix is the burden of
/// both the elite-athlete metabolic work (San-Millán &amp; Brooks 2018) and
/// the intensity-distribution literature (Seiler 2010).
///
/// Citations: <see cref="Citations.UphillAthleteAet"/>,
/// <see cref="Citations.SanMillanBrooks"/>, <see cref="Citations.SeilerPolarized"/>.
/// </remarks>
public static class AerobicAnalysis
{
    /// <summary>Runs shorter than this or without HR carry too little signal.</summary>
    public const double MinimumSeconds = 15 * 60;

    /// <summary>Pace scaled to what it would be at <paramref name="referenceHr"/>.</summary>
    public static double NormalizedSecPerKm(double distanceMeters, double seconds, int averageHr, int referenceHr)
    {
        if (distanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (averageHr <= 0) throw new ArgumentOutOfRangeException(nameof(averageHr));
        if (referenceHr <= 0) throw new ArgumentOutOfRangeException(nameof(referenceHr));

        var secPerKm = seconds / (distanceMeters / 1000.0);

        // Linear scaling: speed ∝ HR through the aerobic range, so pace scales
        // by HR/ref. Crude past ±15 bpm of the reference, which is why callers
        // filter to steady aerobic runs first.
        return secPerKm * averageHr / referenceHr;
    }

    /// <summary>Monthly medians of normalized pace, oldest first.</summary>
    /// <remarks>
    /// The median, not the mean: a single walk-run or treadmill mis-calibration
    /// should not drag a month, and with a handful of runs per month a robust
    /// statistic is the difference between a trend and noise.
    /// </remarks>
    public static IReadOnlyList<MonthlyAerobicPoint> MonthlyTrend(IEnumerable<SteadyRun> runs, int referenceHr)
    {
        return runs
            .Where(r => r.Seconds >= MinimumSeconds && r.AverageHr > 0)
            .GroupBy(r => (r.Date.Year, r.Date.Month))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var normalized = g
                    .Select(r => NormalizedSecPerKm(r.DistanceMeters, r.Seconds, r.AverageHr, referenceHr))
                    .OrderBy(x => x)
                    .ToArray();
                return new MonthlyAerobicPoint(g.Key.Year, g.Key.Month, Median(normalized), normalized.Length);
            })
            .ToArray();
    }

    /// <summary>
    /// The aerobic-deficiency spread: how much slower aerobic-threshold pace is
    /// than lactate-threshold pace, as a fraction. Above ~0.10 is deficient.
    /// </summary>
    public static double DeficiencySpread(double aetSecPerKm, double ltSecPerKm)
    {
        if (aetSecPerKm <= 0) throw new ArgumentOutOfRangeException(nameof(aetSecPerKm));
        if (ltSecPerKm <= 0) throw new ArgumentOutOfRangeException(nameof(ltSecPerKm));
        return (aetSecPerKm - ltSecPerKm) / ltSecPerKm;
    }

    public const double DeficiencyThreshold = 0.10;

    private static double Median(IReadOnlyList<double> sorted)
    {
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }
}
