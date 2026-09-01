namespace aberaTech.Fitness.Domain;

/// <summary>The five things an hour of training can be spent on.</summary>
public enum TrainingZone
{
    /// <summary>Daniels E and M: conversational aerobic volume.</summary>
    Easy,

    /// <summary>Daniels T: comfortably hard, tempo and cruise intervals.</summary>
    Threshold,

    /// <summary>Daniels I and R: VO2max repeats and speed.</summary>
    Interval,

    /// <summary>Heavy resistance work, for economy and durability.</summary>
    Strength
}

/// <summary>
/// A week of training as a vector of hours by intensity, rather than one
/// undifferentiated "hours" number.
/// </summary>
/// <remarks>
/// Two athletes training seven hours a week are not doing the same thing, and
/// a model that only knows the total cannot say where the next hour should go.
/// Every question this engine answers — what will I run, what would it take,
/// what should I change — is asked and answered on this vector.
///
/// <b>Strain</b> weights the hours by what they cost to recover from. The
/// weights are session-load ratios of the kind training-load models use
/// (Coggan's TSS, Foster's session-RPE): an hour at threshold costs roughly two
/// and a half easy hours, an hour of VO2max work roughly four and a half. This
/// is why the optimum is mostly easy running, and why "just add intervals" is
/// not free.
///
/// Citations: <see cref="Citations.CogganPmc"/>, <see cref="Citations.SeilerPolarized"/>.
/// </remarks>
/// <param name="EasyHours">Weekly hours in the Easy/Marathon bands.</param>
/// <param name="ThresholdHours">Weekly hours at Threshold.</param>
/// <param name="IntervalHours">Weekly hours of Interval and Repetition work.</param>
/// <param name="StrengthHours">Weekly hours of heavy resistance training.</param>
public sealed record TrainingDose(
    double EasyHours = 0,
    double ThresholdHours = 0,
    double IntervalHours = 0,
    double StrengthHours = 0)
{
    /// <summary>Recovery cost of one hour in each zone, in easy-hour equivalents.</summary>
    public static double StrainWeight(TrainingZone zone) => zone switch
    {
        TrainingZone.Easy => 1.0,
        TrainingZone.Threshold => 2.5,
        TrainingZone.Interval => 4.5,
        TrainingZone.Strength => 1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    public static IReadOnlyList<TrainingZone> Zones { get; } =
        [TrainingZone.Easy, TrainingZone.Threshold, TrainingZone.Interval, TrainingZone.Strength];

    /// <summary>The running zones — the ones an allocation decides between.</summary>
    public static IReadOnlyList<TrainingZone> RunningZones { get; } =
        [TrainingZone.Easy, TrainingZone.Threshold, TrainingZone.Interval];

    public double this[TrainingZone zone] => zone switch
    {
        TrainingZone.Easy => EasyHours,
        TrainingZone.Threshold => ThresholdHours,
        TrainingZone.Interval => IntervalHours,
        TrainingZone.Strength => StrengthHours,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    public TrainingDose With(TrainingZone zone, double hours) => zone switch
    {
        TrainingZone.Easy => this with { EasyHours = hours },
        TrainingZone.Threshold => this with { ThresholdHours = hours },
        TrainingZone.Interval => this with { IntervalHours = hours },
        TrainingZone.Strength => this with { StrengthHours = hours },
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    public double TotalHours => EasyHours + ThresholdHours + IntervalHours + StrengthHours;

    public double RunningHours => EasyHours + ThresholdHours + IntervalHours;

    /// <summary>Weekly recovery cost in easy-hour equivalents.</summary>
    public double Strain => Zones.Sum(z => StrainWeight(z) * this[z]);

    /// <summary>The share of running time spent easy — the 80/20 number.</summary>
    public double EasyShare => RunningHours > 0 ? EasyHours / RunningHours : 0;

    public TrainingDose Scale(double factor)
    {
        if (factor < 0) throw new ArgumentOutOfRangeException(nameof(factor));
        return new TrainingDose(
            EasyHours * factor, ThresholdHours * factor,
            IntervalHours * factor, StrengthHours * factor);
    }

    /// <summary>
    /// A total split the way the intensity-distribution literature observes
    /// elite endurance athletes splitting it: about 80% easy, the rest divided
    /// between threshold and VO2max work. Used when the athlete has stated a
    /// total but not a plan.
    /// </summary>
    public static TrainingDose Polarized(double runningHours, double strengthHours = 0)
    {
        if (runningHours < 0) throw new ArgumentOutOfRangeException(nameof(runningHours));
        return new TrainingDose(
            EasyHours: 0.80 * runningHours,
            ThresholdHours: 0.12 * runningHours,
            IntervalHours: 0.08 * runningHours,
            StrengthHours: strengthHours);
    }
}
