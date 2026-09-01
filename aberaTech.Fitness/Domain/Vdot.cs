namespace aberaTech.Fitness.Domain;

/// <summary>
/// Daniels' VDOT: race-equivalency scoring for running performances.
/// </summary>
/// <remarks>
/// The two regression equations are from Daniels &amp; Gilbert, "Oxygen Power:
/// Performance Tables for Distance Runners" (1979), as popularised in Jack
/// Daniels, <i>Daniels' Running Formula</i> (Human Kinetics). VDOT is the
/// oxygen cost the performance implies, in ml/kg/min — "effective VO2max",
/// folding in running economy. Two races with equal VDOT are equivalent
/// performances, which is what lets one measured race predict times at other
/// distances.
///
/// Citation: <see cref="Citations.DanielsVdot"/>.
/// </remarks>
public static class Vdot
{
    /// <summary>The VDOT implied by covering <paramref name="distanceMeters"/> in <paramref name="minutes"/>.</summary>
    public static double FromRace(double distanceMeters, double minutes)
    {
        if (distanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes));

        var velocity = distanceMeters / minutes; // meters per minute

        // Oxygen cost of running at that velocity (Daniels & Gilbert 1979).
        var vo2 = -4.60 + 0.182258 * velocity + 0.000104 * velocity * velocity;

        // Fraction of VO2max sustainable for a race of that duration.
        var fraction = 0.8
                       + 0.1894393 * Math.Exp(-0.012778 * minutes)
                       + 0.2989558 * Math.Exp(-0.1932605 * minutes);

        return vo2 / fraction;
    }

    /// <summary>
    /// The race time, in minutes, that <paramref name="vdot"/> predicts over
    /// <paramref name="distanceMeters"/>.
    /// </summary>
    /// <remarks>
    /// The equations cannot be inverted in closed form, so this bisects: the
    /// predicted time is the one whose implied VDOT equals the input. Sixty
    /// iterations puts the answer well inside a millisecond of the true root
    /// over the 4-to-400-minute bracket, which covers everything from a fast
    /// 1500&#160;m to a slow 50&#160;km.
    /// </remarks>
    public static double MinutesFor(double distanceMeters, double vdot)
    {
        if (distanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (vdot <= 0) throw new ArgumentOutOfRangeException(nameof(vdot));

        double faster = 4, slower = 400;
        for (var i = 0; i < 60; i++)
        {
            var mid = (faster + slower) / 2;
            if (FromRace(distanceMeters, mid) > vdot)
            {
                faster = mid; // implied VDOT too high means the guess is too fast
            }
            else
            {
                slower = mid;
            }
        }

        return (faster + slower) / 2;
    }

    /// <summary>
    /// How hard VDOT reacts to speed: d(ln VDOT)/d(ln velocity) at a given
    /// performance.
    /// </summary>
    /// <remarks>
    /// Turning a measured pace improvement into a fitness improvement needs
    /// this number, and assuming it is 1 is wrong by several percent — the
    /// oxygen-cost curve is quadratic in velocity, so faster running costs
    /// disproportionately more. It is computed here by differentiating the
    /// equations above rather than quoted, so it stays right at whatever speed
    /// the athlete is actually running.
    /// </remarks>
    public static double SpeedElasticity(double distanceMeters, double minutes)
    {
        if (distanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes));

        const double nudge = 0.005;
        var faster = FromRace(distanceMeters, minutes * (1 - nudge));
        var slower = FromRace(distanceMeters, minutes * (1 + nudge));

        // Velocity is inversely proportional to time, so the log-velocity span
        // is the negative of the log-time span.
        return Math.Log(faster / slower) / Math.Log((1 + nudge) / (1 - nudge));
    }

    public const double MileMeters = 1609.344;
}
