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

    /// <summary>
    /// The dry load carried on a ruck, in kilograms. No watch records it, so
    /// it is read from the activity's name on import and typed on the page
    /// otherwise; without it a ruck is a walk as far as the models know.
    /// </summary>
    public double? LoadKg { get; set; }

    /// <summary>
    /// Whether the session was on a treadmill or trainer. Treadmill distance
    /// is a wrist estimate, so an indoor run is a weaker witness to pace than
    /// an outdoor one; null when the source did not say.
    /// </summary>
    public bool? Indoor { get; set; }

    public List<StrengthSet> Sets { get; set; } = [];

    /// <summary>The laps the watch recorded, when the source carried them.</summary>
    public List<Lap> Laps { get; set; } = [];

    /// <summary>
    /// The recording split in two at its midpoint: average heart rate and
    /// speed of each half, read from the second-by-second records of the FIT
    /// file. A heart-rate drift test is read from these, the way Evoke reads
    /// it in TrainingPeaks, so no laps are needed. Null when the file carried
    /// no records.
    /// </summary>
    public double? FirstHalfHr { get; set; }

    public double? SecondHalfHr { get; set; }

    public double? FirstHalfMetersPerSecond { get; set; }

    public double? SecondHalfMetersPerSecond { get; set; }

    /// <summary>
    /// The fastest continuous 1.5, 2, 5, and 10 miles inside the run, in
    /// seconds, read from the FIT records. A fast stretch in training counts
    /// toward the run goals (Neb, 2026-11-04). Null when the run was shorter
    /// or the file carried no records.
    /// </summary>
    public double? Best1_5MileSeconds { get; set; }

    public double? Best2MileSeconds { get; set; }

    public double? Best5MileSeconds { get; set; }

    public double? Best10MileSeconds { get; set; }

    /// <summary>
    /// Which reading of the FIT records this row holds: 0 none, 1 the halves,
    /// 2 the halves and the best efforts. The sync re-reads rows below the
    /// current version from their files.
    /// </summary>
    public int RecordsVersion { get; set; }
}

/// <summary>
/// One lap of a run or ruck: a distance, a time, a heart rate.
/// </summary>
/// <remarks>
/// A session's average pace hides its structure. An interval workout reads
/// as an easy run once the recoveries are averaged in. Laps are how the log
/// says what a session actually was, and a lap button pressed at the start
/// and end of the steady part of a test is how a test declares itself.
/// </remarks>
public class Lap
{
    public Guid Id { get; set; }

    public Guid ActivityId { get; set; }

    public int Index { get; set; }

    public double? DistanceMeters { get; set; }

    public double Seconds { get; set; }

    public int? AverageHr { get; set; }
}

/// <summary>
/// The grant to read the goals workbook from the owner's OneDrive. One row.
/// The refresh token is sealed with ASP.NET Data Protection, so the database
/// alone cannot read it.
/// </summary>
public class OneDriveConnection
{
    public int Id { get; set; }

    public required string ProtectedRefreshToken { get; set; }

    public Instant ConnectedAt { get; set; }
}

/// <summary>When an automatic source last ran, and what it said.</summary>
public class SyncState
{
    /// <summary>hevy-api or intervals-icu.</summary>
    public required string Source { get; set; }

    public Instant? LastRunAt { get; set; }

    public string? LastOutcome { get; set; }
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

    /// <summary>How long a timed set was held. A plank has no reps.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>How far a loaded set was carried. A farmer's walk has a distance, not reps.</summary>
    public double? DistanceMeters { get; set; }
}

/// <summary>
/// One Army Fitness Test, as taken: the five raw results. Scored on the way
/// out, never stored scored, so a change to the tables re-scores history.
/// </summary>
/// <summary>
/// One threshold value as measured: the aerobic-threshold heart rate, the
/// lactate-threshold heart rate, or the lactate-threshold pace. Each is
/// logged on its own, with the date of the test that set it, and nothing is
/// overwritten. The current value of each kind is its latest entry, which
/// the settings row mirrors for every reader of the zones. Until 2026-11-13
/// one form saved all three together, a blank box cleared a value, and the
/// old value was gone. Neb asked for the redesign that day.
/// </summary>
public class ThresholdEntry
{
    public const string AetHr = "aet-hr";
    public const string LtHr = "lt-hr";
    public const string LtPace = "lt-pace";

    public static readonly string[] Kinds = [AetHr, LtHr, LtPace];

    public Guid Id { get; set; }

    public string Kind { get; set; } = "";

    /// <summary>Beats per minute for the heart rates, seconds per km for the pace.</summary>
    public double Value { get; set; }

    /// <summary>The day of the test. Null only for values carried over from before the log existed.</summary>
    public LocalDate? MeasuredOn { get; set; }

    /// <summary>What set it, in words: the test's name, or "entered by hand".</summary>
    public string? Source { get; set; }

    /// <summary>The logged run the value came from, when it came from one.</summary>
    public Guid? ActivityId { get; set; }

    public Instant RecordedAt { get; set; }
}

/// <summary>
/// A race or time trial entered by hand: one with no recording, or run
/// before the log began. A recording named for its distance is read from the
/// log itself and needs no entry here.
/// </summary>
public class RaceResult
{
    public Guid Id { get; set; }

    public LocalDate Date { get; set; }

    public string Name { get; set; } = "";

    public double DistanceMeters { get; set; }

    public double Seconds { get; set; }

    /// <summary>Where it was run, when it differs from the place log for that day.</summary>
    public double? AltitudeMeters { get; set; }

    public Instant RecordedAt { get; set; }
}

/// <summary>
/// Where the athlete lived and trained from a date on. A test is scored at
/// the altitude of the place it was run, and the times shown are for where
/// the athlete is now. A place with no start date covers everything before
/// the first dated one.
/// </summary>
public class TrainingPlace
{
    public Guid Id { get; set; }

    public LocalDate? Since { get; set; }

    public string Place { get; set; } = "";

    public double AltitudeMeters { get; set; }
}

public class AftResult
{
    public Guid Id { get; set; }

    public LocalDate Date { get; set; }

    /// <summary>Three-repetition maximum deadlift. The test records pounds; this keeps kilograms like everything else.</summary>
    public double DeadliftKg { get; set; }

    public int HandReleasePushUps { get; set; }

    public double SprintDragCarrySeconds { get; set; }

    public double PlankSeconds { get; set; }

    public double TwoMileSeconds { get; set; }
}

/// <summary>
/// One FBI Physical Fitness Test, as taken. An event left blank was not
/// run. FBI push-ups are their own technique, so only this sets them
/// (Neb, 2026-11-04).
/// </summary>
public class FbiResult
{
    public Guid Id { get; set; }

    public LocalDate Date { get; set; }

    public int? PullUps { get; set; }

    public double? Sprint300mSeconds { get; set; }

    public int? PushUps { get; set; }

    public double? Run1_5MileSeconds { get; set; }
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
    /// The distance for a timed running goal, in meters. Any distance, not
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

    /// <summary>
    /// The lactate-threshold heart rate, when a test has set it. With the
    /// aerobic-threshold heart rate (<see cref="ReferenceHr"/>) it places
    /// treadmill effort, where pace cannot.
    /// </summary>
    public int? LtHr { get; set; }

    /// <summary>
    /// The incline treadmill runs are set to from <see cref="TreadmillGradeSince"/>
    /// on, in percent. A watch cannot record it. A grade in a run's title wins.
    /// </summary>
    public double? TreadmillGradePercent { get; set; }

    /// <summary>The first day treadmill runs were at <see cref="TreadmillGradePercent"/>.</summary>
    public LocalDate? TreadmillGradeSince { get; set; }

    /// <summary>The plan's weekly endurance volume, for compliance math.</summary>
    public double PlanMinutesPerWeek { get; set; } = 160;

    /// <summary>Measured VDOT anchor (from the most recent time trial).</summary>
    public double StartVdot { get; set; } = 37;

    /// <summary>
    /// The race the anchor was scored from, so the page can name it: the
    /// 2-mile of an AFT reads differently from a 5-mile time trial. Null when
    /// the anchor was typed as a raw VDOT.
    /// </summary>
    public double? AnchorDistanceMeters { get; set; }

    public double? AnchorSeconds { get; set; }

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

    /// <summary>
    /// Where the lifetime best was run, from the place log for its year. Set
    /// when the log is read, never stored.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double? PastPeakAltitudeMeters { get; set; }

    /// <summary>
    /// The test the anchor came from, in words: "your AFT 2-mile of
    /// 2026-04-03". Set when the log is read, never stored.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? AnchorSource { get; set; }

    /// <summary>
    /// The date the athlete is assessing on. For a Guard candidate it is the
    /// readiness evaluation, the first gate. The readiness gates count back
    /// from it; unset, they carry no dates.
    /// </summary>
    public LocalDate? SelectionDate { get; set; }
}

/// <summary>
/// A prediction written down before the fact, so it can be scored after it.
/// </summary>
/// <remarks>
/// A model that has never been checked against an outcome is a calculator with
/// good manners. This one had grown considerably without ever having made a
/// prediction anybody later compared to reality. The fix is not more
/// modeling. It is writing one down and waiting.
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

/// <summary>
/// One JSON document the owner keeps: training ticks, drill history, a
/// course plan. Keyed by a name from a short allowlist rather than by user,
/// because there is one owner and the policy on the route is what decides
/// who may read or write it.
/// </summary>
public class OwnerDocument
{
    public string Key { get; set; } = string.Empty;

    /// <summary>The document as the page sent it. Opaque to the server beyond being a JSON object.</summary>
    public string Json { get; set; } = "{}";

    public Instant UpdatedAt { get; set; }
}
