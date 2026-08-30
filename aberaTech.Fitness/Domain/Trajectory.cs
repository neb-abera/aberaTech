namespace aberaTech.Fitness.Domain;

/// <summary>Tunable parameters of the fitness trajectory model.</summary>
/// <param name="StartVdot">Measured VDOT at the start of the projection.</param>
/// <param name="RatePerMonth">
/// The exponential approach rate k. The default 0.0676/month was fitted so a
/// steady dose reproduces the improvement rates in Evoke Endurance's published
/// aerobic-deficiency case studies; a new time trial recalibrates it.
/// </param>
/// <param name="CeilingBase">The b in ceiling C = b + m·hours.</param>
/// <param name="CeilingPerHour">The m in ceiling C = b + m·hours.</param>
public sealed record TrajectoryParameters(
    double StartVdot,
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
/// gap closes — V(t) = C − (C − V0)·e^(−kt), the steady-state solution of the
/// Banister model family under a constant dose (Banister et&#160;al. 1975;
/// Calvert et&#160;al. 1976; Busso 2003). The ceiling C = base + slope·hours
/// encodes that sustainable volume, not willpower, bounds aerobic development —
/// the intensity-distribution literature's central finding (Seiler 2010).
///
/// Citations: <see cref="Citations.BanisterModel"/>,
/// <see cref="Citations.SeilerPolarized"/>, <see cref="Citations.EvokeMilitary"/>.
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
        return ceiling - (ceiling - p.StartVdot) * Math.Exp(-p.RatePerMonth * months);
    }

    /// <summary>
    /// Months until the projection reaches <paramref name="targetVdot"/>, or
    /// null when the dose's ceiling never gets there.
    /// </summary>
    public static double? MonthsToReach(TrajectoryParameters p, double effectiveHours, double targetVdot)
    {
        if (targetVdot <= p.StartVdot) return 0;
        var ceiling = Ceiling(p, effectiveHours);
        if (ceiling <= targetVdot) return null;

        return -Math.Log((ceiling - targetVdot) / (ceiling - p.StartVdot)) / p.RatePerMonth;
    }

    /// <summary>
    /// The inverse: the effective weekly hours whose trajectory passes through
    /// <paramref name="targetVdot"/> at <paramref name="months"/>. Null when no
    /// finite dose does (the date is too close for any ceiling to matter).
    /// </summary>
    /// <remarks>
    /// Solving V(T) = target for C gives C = (target − V0·e^(−kT)) / (1 − e^(−kT));
    /// hours then fall out of the ceiling line. The required C explodes as T → 0,
    /// which is the model saying what a coach would: no dose makes a big jump
    /// arrive next week.
    /// </remarks>
    public static double? HoursToReach(TrajectoryParameters p, double targetVdot, double months)
    {
        if (months <= 0) throw new ArgumentOutOfRangeException(nameof(months));
        if (targetVdot <= p.StartVdot) return 0;

        var decay = Math.Exp(-p.RatePerMonth * months);
        var requiredCeiling = (targetVdot - p.StartVdot * decay) / (1 - decay);
        var hours = (requiredCeiling - p.CeilingBase) / p.CeilingPerHour;

        if (hours is <= 0 or > 40 || double.IsNaN(hours) || double.IsInfinity(hours))
        {
            // Forty hours a week is past any interpretation the calibration
            // supports; report "no realistic dose" rather than a fantasy number.
            return hours <= 0 ? 0 : null;
        }

        return hours;
    }
}
