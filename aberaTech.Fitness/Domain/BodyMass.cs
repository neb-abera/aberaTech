namespace aberaTech.Fitness.Domain;

/// <summary>
/// How a change in body mass moves running performance.
/// </summary>
/// <remarks>
/// VDOT is oxygen uptake <i>per kilogram</i>. Losing fat mass leaves the
/// engine's absolute output (litres of O2 per minute) essentially unchanged
/// while shrinking the denominator, so relative VO2max — and therefore VDOT —
/// scales with the inverse of mass: VDOT' = VDOT × (m₀ / m₁). The experimental
/// anchor is Cureton &amp; Sparling's added-load studies, where each 1% of
/// added mass cost close to 1% of relative VO2max and a measurable slice of
/// 12-minute-run distance.
///
/// Two honest limits, both enforced here. The scaling only describes fat-mass
/// change — starve off muscle and the numerator falls with the denominator —
/// so the adjustment is clamped to ±10% of current mass, past which the
/// assumption has no evidence behind it. And it is symmetric: gaining mass
/// costs exactly what losing it buys.
///
/// Citations: <see cref="Citations.CuretonSparling"/>, <see cref="Citations.DanielsVdot"/>.
/// </remarks>
public static class BodyMass
{
    public const double MaxAdjustmentFraction = 0.10;

    /// <summary>
    /// The VDOT after moving from <paramref name="currentKg"/> to
    /// <paramref name="targetKg"/>, all else equal.
    /// </summary>
    public static double AdjustVdot(double vdot, double currentKg, double targetKg)
    {
        if (vdot <= 0) throw new ArgumentOutOfRangeException(nameof(vdot));
        if (currentKg <= 0) throw new ArgumentOutOfRangeException(nameof(currentKg));
        if (targetKg <= 0) throw new ArgumentOutOfRangeException(nameof(targetKg));

        var floor = currentKg * (1 - MaxAdjustmentFraction);
        var stop = currentKg * (1 + MaxAdjustmentFraction);
        var clamped = Math.Clamp(targetKg, floor, stop);

        return vdot * (currentKg / clamped);
    }

    /// <summary>
    /// A lifetime best re-scored at the weight the athlete intends to race at.
    /// </summary>
    /// <remarks>
    /// A past peak is a performance, and a performance was run at a bodyweight.
    /// Scoring it as though it belonged to no weight at all is what let the
    /// race-weight factor raise the starting fitness toward a ceiling that
    /// stayed put — which shrank the reclaim runway and made the model
    /// <i>less</i> optimistic the lighter the athlete planned to race, exactly
    /// backwards. The mark and the anchor get the same treatment, clamp and
    /// caveat included.
    ///
    /// Null weights mean the athlete has not said what the peak was set at, so
    /// the peak is returned untouched rather than guessed at.
    /// </remarks>
    public static double? AtRaceWeight(double? peakVdot, double? peakWeightKg, double? raceWeightKg)
    {
        if (peakVdot is not { } peak) return null;
        if (peakWeightKg is not { } setAt || raceWeightKg is not { } racing) return peakVdot;
        return AdjustVdot(peak, setAt, racing);
    }

    public const double PoundsPerKg = 2.2046226218;

    public static double PoundsToKg(double pounds) => pounds / PoundsPerKg;
}
