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
/// The de-novo approach rate k. The default 0.0676/month was fitted so a
/// steady dose reproduces the improvement rates in Evoke Endurance's published
/// aerobic-deficiency case studies; the athlete's own history refits it
/// (<see cref="ModelFit"/>).
/// </param>
/// <param name="Responsiveness">
/// The athlete's trainability, scaling every zone's ceiling contribution.
/// 1.0 is the generic athlete the dose-response constants were calibrated on.
/// </param>
public sealed record TrajectoryParameters(
    double StartVdot,
    double? ReclaimVdot = null,
    double RatePerMonth = 0.0676,
    double Responsiveness = 1.0)
{
    public static TrajectoryParameters Default { get; } = new(StartVdot: 37.0);

    public DoseLimits Limits(double maxStrain = DoseResponse.EliteStrain) =>
        new(maxStrain, Responsiveness);
}

/// <summary>
/// A training dose over time: what the athlete does now, what the plan builds
/// to, and how fast it is allowed to get there.
/// </summary>
/// <remarks>
/// A plan is not a step function. An athlete at four hours a week does not
/// train nine next Monday — or rather, they can, and get hurt. Chronic load is
/// built by increments the body absorbs, so the schedule ramps geometrically
/// from where the athlete actually is, and every date this engine reports is
/// a date on the ramped path rather than on a dose nobody could start.
///
/// Citations: <see cref="Citations.GabbettWorkload"/>, <see cref="Citations.CogganPmc"/>.
/// </remarks>
/// <param name="Target">The week the plan builds to.</param>
/// <param name="Start">Where the athlete is now; null means starting at the target.</param>
/// <param name="RampPerWeek">Fractional weekly increase in volume while building.</param>
public sealed record DoseSchedule(TrainingDose Target, TrainingDose? Start = null, double RampPerWeek = 0.08)
{
    /// <summary>Weeks per month, for turning a weekly ramp into a monthly one.</summary>
    public const double WeeksPerMonth = 52.0 / 12.0;

    public static DoseSchedule Constant(TrainingDose dose) => new(dose, dose);

    /// <summary>The week being trained <paramref name="months"/> from the start.</summary>
    public TrainingDose At(double months)
    {
        if (Start is not { } start || Target.TotalHours <= 0) return Target;

        var ratio = start.TotalHours / Target.TotalHours;
        if (ratio >= 1) return Target;

        var grown = ratio * Math.Pow(1 + RampPerWeek, months * WeeksPerMonth);
        return Target.Scale(Math.Min(1, grown));
    }

    /// <summary>Months of ramping before the target week is being trained in full.</summary>
    public double MonthsToFullDose()
    {
        if (Start is not { } start || Target.TotalHours <= 0) return 0;
        var ratio = start.TotalHours / Target.TotalHours;
        if (ratio >= 1) return 0;
        return Math.Log(1 / ratio) / Math.Log(1 + RampPerWeek) / WeeksPerMonth;
    }
}

/// <summary>
/// Projects VDOT forward under a training dose, and solves the inverse
/// questions: when does a goal arrive, and what dose gets there by a date.
/// </summary>
/// <remarks>
/// The shape is the impulse-response view of training: fitness rises toward a
/// ceiling set by the training dose, fast at first and slower as the gap
/// closes (Banister et&#160;al. 1975; Busso 2003). Written as a differential
/// equation rather than a formula,
///
/// dV/dt = k(V)·(C(h(t)) − V),
///
/// which is the same curve when the dose is constant and still correct when it
/// is not — a ramping plan, a block of base work followed by a sharpening
/// block, a return from a layoff. There is no closed form for a time-varying
/// dose, so it is integrated numerically with fourth-order Runge-Kutta at a
/// step of about a day and a half; against the constant-dose exponential it
/// agrees to nine decimal places, which is the test that keeps it honest.
///
/// A trained past speeds the climb. Up to the age-adjusted lifetime peak the
/// athlete is <i>reclaiming</i> — the retraining literature's fast lane
/// (<see cref="Retraining"/>) — so k is multiplied by a factor that tapers
/// from <see cref="Retraining.ReclaimRateMultiplier"/> at the starting fitness
/// to 1 at the old peak. Beyond the peak is new territory at the de-novo rate.
///
/// Citations: <see cref="Citations.BanisterModel"/>,
/// <see cref="Citations.SeilerPolarized"/>, <see cref="Citations.MujikaRetraining"/>,
/// <see cref="Citations.UphillAthleteAet"/>.
/// </remarks>
public static class Trajectory
{
    /// <summary>Integration step, in months: about a day and a half.</summary>
    private const double Step = 0.05;

    /// <summary>Hours that actually happen: planned hours scaled by compliance.</summary>
    public static double EffectiveHours(double weeklyHours, double compliance)
    {
        if (weeklyHours < 0) throw new ArgumentOutOfRangeException(nameof(weeklyHours));
        if (compliance is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(compliance));
        return weeklyHours * compliance;
    }

    /// <summary>The VDOT a sustained dose supports in the limit.</summary>
    public static double Ceiling(TrajectoryParameters p, TrainingDose dose) =>
        Math.Max(p.StartVdot, DoseResponse.Ceiling(dose, p.Responsiveness));

    /// <summary>The VDOT a sustained total of running hours supports, split optimally.</summary>
    public static double Ceiling(TrajectoryParameters p, double effectiveHours) =>
        Ceiling(p, DoseResponse.Allocate(effectiveHours, p.Limits()).Dose);

    /// <summary>How much faster fitness closes on the ceiling while old ground is being re-taken.</summary>
    public static double RateMultiplier(TrajectoryParameters p, double vdot)
    {
        if (p.ReclaimVdot is not { } reclaim || reclaim <= p.StartVdot) return 1;
        var reclaimed = Math.Clamp((reclaim - vdot) / (reclaim - p.StartVdot), 0, 1);
        return 1 + (Retraining.ReclaimRateMultiplier - 1) * reclaimed;
    }

    /// <summary>
    /// Projected VDOT after <paramref name="months"/> under any dose that
    /// varies with time — a ramp, a block plan, or the weeks an athlete
    /// actually trained.
    /// </summary>
    public static double VdotAt(TrajectoryParameters p, Func<double, TrainingDose> dose, double months)
    {
        if (months < 0) throw new ArgumentOutOfRangeException(nameof(months));

        var vdot = p.StartVdot;
        var elapsed = 0.0;
        while (elapsed < months - 1e-12)
        {
            var step = Math.Min(Step, months - elapsed);
            vdot = Advance(p, dose, elapsed, step, vdot);
            elapsed += step;
        }

        return vdot;
    }

    /// <summary>Projected VDOT after <paramref name="months"/> under a schedule.</summary>
    public static double VdotAt(TrajectoryParameters p, DoseSchedule schedule, double months) =>
        VdotAt(p, schedule.At, months);

    /// <summary>Projected VDOT under a constant dose held from the start.</summary>
    public static double VdotAt(TrajectoryParameters p, TrainingDose dose, double months) =>
        VdotAt(p, DoseSchedule.Constant(dose), months);

    /// <summary>Projected VDOT under a constant total of running hours, split optimally.</summary>
    public static double VdotAt(TrajectoryParameters p, double effectiveHours, double months) =>
        VdotAt(p, DoseResponse.Allocate(effectiveHours, p.Limits()).Dose, months);

    /// <summary>One Runge-Kutta step of dV/dt = k(V)·(C(h(t)) − V).</summary>
    private static double Advance(
        TrajectoryParameters p, Func<double, TrainingDose> dose, double at, double step, double vdot)
    {
        double Slope(double time, double v)
        {
            var ceiling = Ceiling(p, dose(time));
            return p.RatePerMonth * RateMultiplier(p, v) * (ceiling - v);
        }

        var k1 = Slope(at, vdot);
        var k2 = Slope(at + step / 2, vdot + step * k1 / 2);
        var k3 = Slope(at + step / 2, vdot + step * k2 / 2);
        var k4 = Slope(at + step, vdot + step * k3);
        return vdot + step * (k1 + 2 * k2 + 2 * k3 + k4) / 6;
    }

    /// <summary>
    /// Months until the projection reaches <paramref name="targetVdot"/>, or
    /// null when the schedule's ceiling never gets there.
    /// </summary>
    /// <remarks>
    /// The trajectory has no closed-form inverse, so this bisects on time; the
    /// curve is strictly increasing below its ceiling, which makes the bracket
    /// sound. Fifty years is comfortably past any horizon the UI shows and
    /// keeps the asymptote honest: near-ceiling goals report huge numbers
    /// rather than false precision.
    /// </remarks>
    public static double? MonthsToReach(TrajectoryParameters p, DoseSchedule schedule, double targetVdot)
    {
        if (targetVdot <= p.StartVdot) return 0;
        if (Ceiling(p, schedule.Target) <= targetVdot) return null;
        if (VdotAt(p, schedule, 600) < targetVdot) return null;

        double sooner = 0, later = 600;
        for (var i = 0; i < 50; i++)
        {
            var mid = (sooner + later) / 2;
            if (VdotAt(p, schedule, mid) < targetVdot) sooner = mid; else later = mid;
        }

        return (sooner + later) / 2;
    }

    public static double? MonthsToReach(TrajectoryParameters p, double effectiveHours, double targetVdot) =>
        MonthsToReach(p, DoseSchedule.Constant(DoseResponse.Allocate(effectiveHours, p.Limits()).Dose), targetVdot);

    /// <summary>
    /// The inverse: the weekly running hours whose trajectory passes through
    /// <paramref name="targetVdot"/> at <paramref name="months"/>, ramped from
    /// <paramref name="from"/> if given. Null when no sustainable week does it
    /// in the time; zero when the target is already met.
    /// </summary>
    public static double? HoursToReach(
        TrajectoryParameters p,
        double targetVdot,
        double months,
        TrainingDose? from = null,
        DoseLimits? limits = null)
    {
        if (months <= 0) throw new ArgumentOutOfRangeException(nameof(months));
        if (targetVdot <= p.StartVdot) return 0;

        var bounds = limits ?? p.Limits();
        var most = bounds.MaxStrain / TrainingDose.StrainWeight(TrainingZone.Easy);

        double Reached(double hours) =>
            VdotAt(p, new DoseSchedule(DoseResponse.Allocate(hours, bounds).Dose, from), months);

        if (Reached(most) < targetVdot) return null;
        if (Reached(0) >= targetVdot) return 0;

        double fewer = 0, more = most;
        for (var i = 0; i < 50; i++)
        {
            var mid = (fewer + more) / 2;
            if (Reached(mid) < targetVdot) fewer = mid; else more = mid;
        }

        return (fewer + more) / 2;
    }
}
