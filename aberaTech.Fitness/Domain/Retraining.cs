namespace aberaTech.Fitness.Domain;

/// <summary>
/// What a training history is worth: the age-adjusted fitness an athlete can
/// reclaim quickly, as opposed to building for the first time.
/// </summary>
/// <remarks>
/// Detrained athletes are not beginners. Regaining previously held fitness
/// runs at a multiple of the de-novo rate — the detraining/retraining
/// literature (Mujika &amp; Padilla's two-part review) shows much of the loss
/// is rapidly reversible plasma volume and enzyme activity, and the muscle
/// keeps structural receipts: myonuclei added by past training persist
/// through years of detraining (Bruusgaard et&#160;al.), with an epigenetic
/// memory on top (Seaborne et&#160;al.). What was once held is re-earned fast;
/// only territory beyond the lifetime best moves at the slow de-novo rate.
///
/// The reclaimable level decays with age. World Masters Athletics age-grading
/// holds open-class standards flat through the early thirties and then
/// declines roughly 0.7% a year for middle-distance running — so a peak set
/// at 24 is essentially intact at 33 and meaningfully discounted at 45.
///
/// Citations: <see cref="Citations.MujikaRetraining"/>,
/// <see cref="Citations.MuscleMemory"/>, <see cref="Citations.WmaAgeGrading"/>.
/// </remarks>
public static class Retraining
{
    /// <summary>How much faster reclaiming old fitness runs than building new.</summary>
    public const double ReclaimRateMultiplier = 2.5;

    /// <summary>Age through which the peak carries forward undiminished.</summary>
    public const int PlateauEndAge = 34;

    /// <summary>Fractional decline per year past the plateau.</summary>
    public const double DeclinePerYear = 0.007;

    /// <summary>The VDOT a past peak is worth at the athlete's current age.</summary>
    public static double AgeAdjustedPeak(double peakVdot, int ageAtPeak, int ageNow)
    {
        if (peakVdot <= 0) throw new ArgumentOutOfRangeException(nameof(peakVdot));
        if (ageNow < ageAtPeak) throw new ArgumentOutOfRangeException(nameof(ageNow));

        var declineYears = Math.Max(0, ageNow - Math.Max(ageAtPeak, PlateauEndAge));
        return peakVdot * Math.Pow(1 - DeclinePerYear, declineYears);
    }
}
