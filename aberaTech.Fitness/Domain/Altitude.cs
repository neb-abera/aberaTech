namespace aberaTech.Fitness.Domain;

/// <summary>
/// What thin air costs a distance runner, and what a time run at altitude is
/// worth at sea level.
/// </summary>
/// <remarks>
/// Aerobic race times slow with altitude because VO2max falls with the
/// partial pressure of oxygen. Péronnet, Thibault &amp; Cousineau's
/// theoretical analysis, anchored on the Mexico City record book, puts the
/// cost for events run mostly aerobically (two miles and up) near zero below
/// ~600&#160;m and around 2–3% at Mexico City's 2,240&#160;m. This linear
/// approximation reproduces that: about 1% at El&#160;Paso's ~1,190&#160;m.
/// Sprints, which are anaerobic and air-resistance-limited, actually speed up
/// at altitude — this model is for the aerobic events this app predicts and
/// deliberately never returns a benefit.
///
/// Citation: <see cref="Citations.PeronnetAltitude"/>.
/// </remarks>
public static class Altitude
{
    /// <summary>Below this elevation the aerobic cost is lost in the noise.</summary>
    public const double FreeMeters = 600;

    /// <summary>Fractional slowdown per metre above <see cref="FreeMeters"/>.</summary>
    public const double CostPerMeter = 0.0000183;

    /// <summary>The fractional time penalty for racing at <paramref name="altitudeMeters"/>.</summary>
    public static double Penalty(double altitudeMeters)
    {
        if (altitudeMeters < 0) throw new ArgumentOutOfRangeException(nameof(altitudeMeters));
        return Math.Max(0, altitudeMeters - FreeMeters) * CostPerMeter;
    }

    /// <summary>A sea-level time, slowed for the venue.</summary>
    public static double AtAltitude(double seaLevelSeconds, double altitudeMeters) =>
        seaLevelSeconds * (1 + Penalty(altitudeMeters));

    /// <summary>What a time run at altitude is worth at sea level.</summary>
    public static double ToSeaLevel(double altitudeSeconds, double altitudeMeters) =>
        altitudeSeconds / (1 + Penalty(altitudeMeters));
}
