namespace aberaTech.Fitness.Domain;

/// <summary>
/// What body composition says about a selection candidate, from the one
/// cohort it has been measured in.
/// </summary>
/// <remarks>
/// The weigh-in already records body fat, and the trajectory model already
/// treats mass as a denominator. What neither said is what selection itself
/// makes of the number. Farina and colleagues at the Army's research institute
/// measured 800 candidates before Special Forces Assessment and Selection and
/// followed them through it: the leanest quarter (about 14% body fat) was
/// selected at 51.6%, the fattest quarter (about 25%) at 13.8%; the quarter
/// with the most lean mass (about 73 kg) was selected at 58.6%, the quarter
/// with the least (about 54 kg) at 20.0%.
///
/// These are the cohort's rates, not this athlete's odds: they are what
/// happened to soldiers who looked like that, before any account of how they
/// ran or rucked. The two ends are the published quartile means; between them
/// the rate is interpolated linearly, which is the least presumptuous curve
/// through two points, and it is clamped outside them because the study has
/// nothing to say there.
///
/// Citation: <see cref="Citations.FarinaSfasBody"/>.
/// </remarks>
public static class BodyComposition
{
    /// <summary>Body fat of the leanest quartile and the rate it was selected at.</summary>
    public const double LeanestQuartileFat = 14.0;
    public const double LeanestQuartileRate = 0.516;

    /// <summary>Body fat of the fattest quartile and the rate it was selected at.</summary>
    public const double FattestQuartileFat = 25.0;
    public const double FattestQuartileRate = 0.138;

    /// <summary>Lean mass of the lowest quartile and the rate it was selected at.</summary>
    public const double LowestLeanMassKg = 54.0;
    public const double LowestLeanMassRate = 0.200;

    /// <summary>Lean mass of the highest quartile and the rate it was selected at.</summary>
    public const double HighestLeanMassKg = 73.0;
    public const double HighestLeanMassRate = 0.586;

    /// <summary>Fat-free mass, from a weigh-in that recorded body fat.</summary>
    public static double LeanMassKg(double weightKg, double bodyFatPercent)
    {
        if (weightKg <= 0) throw new ArgumentOutOfRangeException(nameof(weightKg));
        if (bodyFatPercent is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(bodyFatPercent));
        return weightKg * (1 - bodyFatPercent / 100);
    }

    /// <summary>The cohort selection rate at this body fat.</summary>
    public static double SelectionRateByBodyFat(double bodyFatPercent) =>
        Interpolate(bodyFatPercent, LeanestQuartileFat, LeanestQuartileRate, FattestQuartileFat, FattestQuartileRate);

    /// <summary>The cohort selection rate at this lean mass.</summary>
    public static double SelectionRateByLeanMass(double leanMassKg) =>
        Interpolate(leanMassKg, LowestLeanMassKg, LowestLeanMassRate, HighestLeanMassKg, HighestLeanMassRate);

    private static double Interpolate(double x, double x0, double y0, double x1, double y1)
    {
        var t = Math.Clamp((x - x0) / (x1 - x0), 0, 1);
        return y0 + (y1 - y0) * t;
    }
}
