namespace aberaTech.Fitness.Domain;

/// <summary>
/// Estimated one-rep max from a submaximal set.
/// </summary>
/// <remarks>
/// Epley (1985) is the primary estimate — the linear form the strength
/// literature keeps returning to for sets under ~10 reps — with Brzycki (1993)
/// reported alongside as the standard cross-check; the two bracket most
/// measured maxes. Estimates degrade quickly past ten reps, so sets longer
/// than that are excluded from trend math rather than pretending precision.
///
/// Citations: <see cref="Citations.Epley"/>, <see cref="Citations.Brzycki"/>.
/// </remarks>
public static class OneRepMax
{
    /// <summary>Reps beyond this make the formulas unreliable; callers should skip the set.</summary>
    public const int MaxTrustworthyReps = 10;

    public static double Epley(double weight, int reps)
    {
        Validate(weight, reps);
        return reps == 1 ? weight : weight * (1 + reps / 30.0);
    }

    public static double Brzycki(double weight, int reps)
    {
        Validate(weight, reps);
        return reps == 1 ? weight : weight * 36.0 / (37.0 - reps);
    }

    /// <summary>True when the set is inside the range the formulas were built for.</summary>
    public static bool IsEstimable(double weight, int reps) =>
        weight > 0 && reps is >= 1 and <= MaxTrustworthyReps;

    private static void Validate(double weight, int reps)
    {
        if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
        if (reps is < 1 or > MaxTrustworthyReps) throw new ArgumentOutOfRangeException(nameof(reps));
    }
}
