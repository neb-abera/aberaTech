using NodaTime;

namespace aberaTech.Fitness.Data;

/// <summary>One recorded session, whatever tracked it.</summary>
public class Activity
{
    public Guid Id { get; set; }

    /// <summary>Where the record came from: garmin-fit, garmin-csv, hevy-api, hevy-csv, manual.</summary>
    public required string Source { get; set; }

    /// <summary>
    /// The source's own identifier, when it has one. Unique per source, which
    /// is what makes every import idempotent: re-uploading a file or re-running
    /// a sync updates rather than duplicates.
    /// </summary>
    public string? ExternalId { get; set; }

    public Instant StartedAt { get; set; }

    /// <summary>run, ruck, strength or other.</summary>
    public required string Sport { get; set; }

    public string Name { get; set; } = "";

    public double? DistanceMeters { get; set; }

    public double DurationSeconds { get; set; }

    public int? AverageHr { get; set; }

    public int? MaxHr { get; set; }

    public List<StrengthSet> Sets { get; set; } = [];
}

/// <summary>One set inside a strength activity.</summary>
public class StrengthSet
{
    public Guid Id { get; set; }

    public Guid ActivityId { get; set; }

    public required string Exercise { get; set; }

    public int SetIndex { get; set; }

    /// <summary>Zero for bodyweight-only sets (push-ups, pull-ups).</summary>
    public double WeightKg { get; set; }

    public int Reps { get; set; }
}

/// <summary>A dated bodyweight observation. Entered by hand; nutrition lives elsewhere.</summary>
public class BodyMetric
{
    public Guid Id { get; set; }

    public LocalDate Date { get; set; }

    public double WeightKg { get; set; }

    public double? BodyFatPercent { get; set; }
}

/// <summary>A goal: a metric, the value to reach, and when.</summary>
public class Goal
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key for the goal. Distances the athlete names themselves get a
    /// generated one (run-8047m), which is what keeps the goal list open-ended
    /// rather than a menu of four.
    /// </summary>
    public required string Metric { get; set; }

    /// <summary>
    /// The distance for a timed running goal, in metres. Any distance, not
    /// only the ones with a preset; null for goals that are not races.
    /// </summary>
    public double? DistanceMeters { get; set; }

    /// <summary>What the athlete calls it, when they call it something.</summary>
    public string? Label { get; set; }

    /// <summary>Seconds for timed metrics, kilograms for loads, count for reps.</summary>
    public double TargetValue { get; set; }

    public LocalDate TargetDate { get; set; }
}

/// <summary>
/// The athlete's measured anchors and model parameters. One row; the engine is
/// single-athlete by design.
/// </summary>
public class AthleteSettings
{
    public int Id { get; set; }

    /// <summary>Reference HR that normalized pace is scaled to (bpm).</summary>
    public int ReferenceHr { get; set; } = 152;

    /// <summary>Lactate-threshold pace, for the aerobic-deficiency check.</summary>
    public double? LtSecondsPerKm { get; set; }

    /// <summary>The plan's weekly endurance volume, for compliance math.</summary>
    public double PlanMinutesPerWeek { get; set; } = 160;

    /// <summary>Measured VDOT anchor (from the most recent time trial).</summary>
    public double StartVdot { get; set; } = 37;

    /// <summary>When the anchor was measured; predictions project from here.</summary>
    public LocalDate? VdotMeasuredOn { get; set; }

    /// <summary>Birth year, for the age adjustment on the reclaimable peak.</summary>
    public int? BirthYear { get; set; }

    /// <summary>
    /// Which record book a target is graded against. Null means unstated, and
    /// the model says so rather than guessing: it grades against the open
    /// men's book, which is the most permissive reading available.
    /// </summary>
    public bool? Female { get; set; }

    /// <summary>Weekly running hours the athlete says they can commit to.</summary>
    public double AvailableHoursPerWeek { get; set; } = 7;

    /// <summary>
    /// The biggest week the athlete has held for a month without breaking
    /// down, in running hours. It sets the recovery budget the model plans
    /// against; unset, the budget is a full-time athlete's, which is generous
    /// and said so in the assumptions.
    /// </summary>
    public double? SustainedWeeklyHours { get; set; }

    /// <summary>The lifetime-best race: distance, time, and roughly when.</summary>
    public double? PastPeakDistanceMeters { get; set; }

    public double? PastPeakSeconds { get; set; }

    public int? PastPeakYear { get; set; }

    /// <summary>
    /// What the athlete weighed when the lifetime best was set. Without it the
    /// peak cannot be re-scored at a different race weight, so a plan to race
    /// lighter raises the starting fitness toward a ceiling that does not move.
    /// </summary>
    public double? PastPeakWeightKg { get; set; }

    /// <summary>
    /// The weight the athlete intends to race at, kept so the projection opens
    /// on the plan rather than resetting to today's weight on every reload.
    /// </summary>
    public double? GoalWeightKg { get; set; }

    /// <summary>Where races happen; thin air slows aerobic times (~1% at El Paso).</summary>
    public double HomeAltitudeMeters { get; set; }
}

/// <summary>
/// A prediction written down before the fact, so it can be scored after it.
/// </summary>
/// <remarks>
/// A model that has never been checked against an outcome is a calculator with
/// good manners. This one had grown considerably without ever having made a
/// prediction anybody later compared to reality — and the way that gets fixed
/// is not more modelling, it is writing one down and waiting.
///
/// The scenario is stored alongside the answer because a prediction is only
/// meaningful with its assumptions attached: what it assumed about training,
/// compliance and race weight is the difference between the model being wrong
/// and the plan not having happened.
/// </remarks>
public class LockedPrediction
{
    public Guid Id { get; set; }

    /// <summary>When the prediction was made.</summary>
    public LocalDate MadeOn { get; set; }

    /// <summary>The date the prediction is about.</summary>
    public LocalDate TargetDate { get; set; }

    public double DistanceMeters { get; set; }

    /// <summary>The median predicted time, in seconds.</summary>
    public double PredictedSeconds { get; set; }

    /// <summary>The fast and slow ends of the interval that was quoted with it.</summary>
    public double PredictedFastSeconds { get; set; }

    public double PredictedSlowSeconds { get; set; }

    /// <summary>The plan it assumed, so a miss can be attributed rather than argued about.</summary>
    public double WeeklyHours { get; set; }

    public double Compliance { get; set; }

    public double? RaceMassKg { get; set; }

    /// <summary>What actually happened, once it has.</summary>
    public double? ActualSeconds { get; set; }

    /// <summary>Anything worth remembering about the day.</summary>
    public string? Note { get; set; }
}
