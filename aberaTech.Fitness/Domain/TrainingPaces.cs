namespace aberaTech.Fitness.Domain;

/// <summary>One of Daniels' five training intensities, as a pace band.</summary>
/// <param name="Zone">E, M, T, I or R.</param>
/// <param name="Name">The zone's full name.</param>
/// <param name="Purpose">What the zone is for, in one clause.</param>
/// <param name="FastSecPerKm">The fast end of the band.</param>
/// <param name="SlowSecPerKm">The slow end of the band.</param>
public sealed record TrainingPace(
    string Zone,
    string Name,
    string Purpose,
    double FastSecPerKm,
    double SlowSecPerKm);

/// <summary>
/// Daniels' five training paces, computed from the current VDOT.
/// </summary>
/// <remarks>
/// The most actionable number a VDOT gives is not a prediction but a
/// prescription: the pace each kind of training should happen at. The bands
/// are Daniels' published intensity ranges as fractions of VO2max — Easy
/// 59–74%, Marathon 75–84%, Threshold 83–88%, Interval 95–100%, Repetition
/// ~105–110% — inverted through the same oxygen-cost curve the VDOT score
/// comes from. Computed this way the paces land within a few seconds per
/// kilometre of Daniels' printed tables, which carry additional empirical
/// smoothing; treat these as bands, not laps to hit to the second.
///
/// Citation: <see cref="Citations.DanielsVdot"/>.
/// </remarks>
public static class TrainingPaces
{
    public static IReadOnlyList<TrainingPace> For(double vdot)
    {
        if (vdot <= 0) throw new ArgumentOutOfRangeException(nameof(vdot));

        return
        [
            Band(vdot, "E", "Easy", "aerobic base — where Zone 2 volume lives", 0.59, 0.74),
            Band(vdot, "M", "Marathon", "steady long efforts and ruck-pace calibration", 0.75, 0.84),
            Band(vdot, "T", "Threshold", "cruise intervals and tempo, comfortably hard", 0.83, 0.88),
            Band(vdot, "I", "Interval", "VO2max work, 3–5 minute repeats", 0.95, 1.00),
            Band(vdot, "R", "Repetition", "speed and economy — strides live here", 1.05, 1.10)
        ];
    }

    private static TrainingPace Band(
        double vdot, string zone, string name, string purpose, double lowFraction, double highFraction)
    {
        // Slower fraction of VO2max means a slower pace, so the low end of the
        // intensity band is the slow end of the pace band.
        return new TrainingPace(
            zone, name, purpose,
            FastSecPerKm: SecPerKmAt(vdot * highFraction),
            SlowSecPerKm: SecPerKmAt(vdot * lowFraction));
    }

    /// <summary>
    /// The pace whose oxygen cost equals <paramref name="vo2"/> — the Daniels
    /// &amp; Gilbert cost curve solved for velocity.
    /// </summary>
    private static double SecPerKmAt(double vo2)
    {
        // 0.000104·v² + 0.182258·v − (4.60 + vo2) = 0, taking the positive root.
        const double a = 0.000104;
        const double b = 0.182258;
        var c = -(4.60 + vo2);

        var metersPerMinute = (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
        return 60_000.0 / metersPerMinute;
    }
}
