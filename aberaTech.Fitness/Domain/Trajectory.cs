namespace aberaTech.Fitness.Domain;

/// <summary>Tunable parameters of the fitness trajectory model.</summary>
/// <param name="StartVdot">Measured VDOT at the start of the projection.</param>
/// <param name="ReclaimVdot">
/// The age-adjusted lifetime peak (see <see cref="Retraining.AgeAdjustedPeak"/>),
/// or null for an athlete with no trained past. Fitness up to this level is
/// re-earned at <see cref="Retraining.ReclaimRateMultiplier"/> times the
/// de-novo rate.
/// </param>
/// <param name="RatePerMonth">
/// The de-novo exponential approach rate k. The default 0.0676/month was
/// fitted so a steady dose reproduces the improvement rates in Evoke
/// Endurance's published aerobic-deficiency case studies; a new time trial
/// recalibrates it.
/// </param>
/// <param name="CeilingBase">The b in ceiling C = b + m·hours.</param>
/// <param name="CeilingPerHour">The m in ceiling C = b + m·hours.</param>
public sealed record TrajectoryParameters(
    double StartVdot,
    double? ReclaimVdot = null,
    double RatePerMonth = 0.0676,
    double CeilingBase = 38.0,
    double CeilingPerHour = 1.6)
{
    public static TrajectoryParameters Default { get; } = new(StartVdot: 37.0);
}

/// <summary>
/// Projects VDOT forward under a training dose, and solves the inverse
/// question: what dose reaches a goal by a date.
/// </summary>
/// <remarks>
/// The shape is the classic impulse-response view of training: fitness rises
/// toward a ceiling set by the training dose, fast at first and slower as the
/// gap closes (Banister et&#160;al. 1975; Busso 2003). The ceiling
/// C&#160;=&#160;base&#160;+&#160;slope·hours encodes that sustainable volume,
/// not willpower, bounds aerobic development (Seiler 2010).
///
/// A trained past splits the climb into two phases. Up to the age-adjusted
/// lifetime peak, the athlete is <i>reclaiming</i> — the retraining
/// literature's fast lane (<see cref="Retraining"/>). Beyond the peak is new
/// territory at the de-novo rate. The curve is the sum of both exponentials,
/// so it is smooth, starts at the anchor, and still converges to the dose's
/// ceiling:
/// V(t) = V0 + (R−V0)(1−e^(−k·m·t)) + (C−R)(1−e^(−k·t)), R = min(peak, C).
///
/// Citations: <see cref="Citations.BanisterModel"/>,
/// <see cref="Citations.SeilerPolarized"/>, <see cref="Citations.MujikaRetraining"/>,
/// <see cref="Citations.EvokeMilitary"/>.
/// </remarks>
public static class Trajectory
{
    /// <summary>Hours that actually happen: planned hours scaled by compliance.</summary>
    public static double EffectiveHours(double weeklyHours, double compliance)
    {
        if (weeklyHours < 0) throw new ArgumentOutOfRangeException(nameof(weeklyHours));
        if (compliance is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(compliance));
        return weeklyHours * compliance;
    }

    /// <summary>The VDOT a sustained dose supports in the limit.</summary>
    public static double Ceiling(TrajectoryParameters p, double effectiveHours)
        => Math.Max(p.StartVdot, p.CeilingBase + p.CeilingPerHour * effectiveHours);

    /// <summary>Projected VDOT after <paramref name="months"/> at a constant dose.</summary>
    public static double VdotAt(TrajectoryParameters p, double effectiveHours, double months)
    {
        if (months < 0) throw new ArgumentOutOfRangeException(nameof(months));

        var ceiling = Ceiling(p, effectiveHours);
        var reclaim = Math.Min(p.ReclaimVdot ?? p.StartVdot, ceiling);

        var reclaimSpan = Math.Max(0, reclaim - p.StartVdot);
        var novelSpan = Math.Max(0, ceiling - Math.Max(reclaim, p.StartVdot));

        var reclaimed = reclaimSpan
                        * (1 - Math.Exp(-p.RatePerMonth * Retraining.ReclaimRateMultiplier * months));
        var built = novelSpan * (1 - Math.Exp(-p.RatePerMonth * months));

        return p.StartVdot + reclaimed + built;
    }

    /// <summary>
    /// Months until the projection reaches <paramref name="targetVdot"/>, or
    /// null when the dose's ceiling never gets there.
    /// </summary>
    /// <remarks>
    /// The two-exponential sum has no closed-form inverse, so this bisects on
    /// time; the curve is strictly increasing below its ceiling, which makes
    /// the bracket sound. Fifty years is comfortably past any horizon the UI
    /// shows and keeps the asymptote honest: near-ceiling goals report huge
    /// numbers rather than false precision.
    /// </remarks>
    public static double? MonthsToReach(TrajectoryParameters p, double effectiveHours, double targetVdot)
    {
        if (targetVdot <= p.StartVdot) return 0;
        if (Ceiling(p, effectiveHours) <= targetVdot) return null;

        double sooner = 0, later = 600;
        for (var i = 0; i < 60; i++)
        {
            var mid = (sooner + later) / 2;
            if (VdotAt(p, effectiveHours, mid) < targetVdot)
            {
                sooner = mid;
            }
            else
            {
                later = mid;
            }
        }

        return (sooner + later) / 2;
    }

    /// <summary>
    /// The inverse: the effective weekly hours whose trajectory passes through
    /// <paramref name="targetVdot"/> at <paramref name="months"/>. Null when
    /// even forty hours a week does not (the date is too close), zero when the
    /// target is already met.
    /// </summary>
    public static double? HoursToReach(TrajectoryParameters p, double targetVdot, double months)
    {
        if (months <= 0) throw new ArgumentOutOfRangeException(nameof(months));
        if (targetVdot <= p.StartVdot) return 0;

        const double most = 40;
        if (VdotAt(p, most, months) < targetVdot) return null;
        if (VdotAt(p, 0, months) >= targetVdot) return 0;

        double fewer = 0, more = most;
        for (var i = 0; i < 60; i++)
        {
            var mid = (fewer + more) / 2;
            if (VdotAt(p, mid, months) < targetVdot)
            {
                fewer = mid;
            }
            else
            {
                more = mid;
            }
        }

        return (fewer + more) / 2;
    }
}
