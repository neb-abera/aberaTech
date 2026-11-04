using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>Where a number came from: a test the athlete took, or the model's estimate.</summary>
public enum Basis
{
    Measured,
    Modeled,

    /// <summary>
    /// The best set or effort in training: a floor for the test, not a test
    /// result, so it earns no test points. On 2026-11-04 a 12-rep set of
    /// push-ups scored -2 FBI points as if it were the one-minute test.
    /// </summary>
    Training
}

/// <summary>The athlete's current standing on one metric.</summary>
/// <param name="Metric">One of the keys in <see cref="SelectionReadiness.Metrics"/>.</param>
/// <param name="Value">In the metric's unit: seconds, repetitions, a bodyweight multiple, pounds, points or percent.</param>
/// <param name="Basis">Measured or modelled.</param>
/// <param name="Evidence">Where the value came from, in a phrase.</param>
/// <param name="On">When it was measured, if it was.</param>
public sealed record Measurement(string Metric, double Value, Basis Basis, string Evidence, LocalDate? On = null);

/// <summary>Whether the requirement wants the number at least, or at most, the target.</summary>
public enum Comparison
{
    AtLeast,
    AtMost
}

/// <summary>One line of a gate: a metric, a direction, a target, and where the target came from.</summary>
public sealed record Requirement(
    string Metric,
    string Label,
    Comparison Comparison,
    double Target,
    string Unit,
    string CitationId);

/// <summary>
/// The metrics a goal can be measured under, and the units its value is
/// stated in. The goals themselves are the athlete's workbook (GoalCatalog).
/// </summary>
public static class SelectionReadiness
{
    /// <summary>The metric keys a gate can ask about.</summary>
    public static class Metrics
    {
        public const string RunTwoMile = "run-2mi";
        public const string RunFiveMile = "run-5mi";
        public const string RuckTwelveMileAt45 = "ruck-12mi-45lb";
        public const string RuckTwelveMileAt35 = "ruck-12mi-35lb";
        public const string PullUps = Calisthenics.PullUps;
        public const string HandReleasePushUps = Calisthenics.HandReleasePushUps;
        public const string PushUps = Calisthenics.PushUps;
        public const string SitUps = Calisthenics.SitUps;
        public const string Plank = Calisthenics.Plank;
        public const string DeadliftTripleToBodyweight = "deadlift-3rm-bw";
        public const string FrontSquatTripleToBodyweight = "front-squat-3rm-bw";
        public const string DeadliftTripleLb = "deadlift-3rm-lb";
        public const string AftTotal = "aft-total";
        public const string AftLowestEvent = "aft-lowest-event";
        public const string BodyFat = "body-fat";

        /// <summary>Pace at the reference (aerobic-threshold) heart rate, in seconds per mile.</summary>
        public const string AerobicThresholdPace = "aet-pace";

        public const string BackSquatFiveLb = "back-squat-5rm-lb";

        /// <summary>Farmer's carry at 1.5× bodyweight, the longest distance held, in metres.</summary>
        public const string FarmersCarryMeters = "farmer-carry-1.5bw-m";

        public const string BodyweightLb = "bodyweight-lb";

        /// <summary>Estimated one-rep maxes over bodyweight, the way the athlete's own sheet states them.</summary>
        public const string BenchPressToBodyweight = "bench-1rm-bw";
        public const string FrontSquatToBodyweight = "front-squat-1rm-bw";

        public const string RunTenMile = "run-10mi";
        public const string RunOneAndHalfMile = "run-1.5mi";
        public const string Sprint300m = "sprint-300m";
        public const string SprintDragCarry = "sprint-drag-carry";
        public const string BenchPressLb = "bench-1rm-lb";
        public const string FrontSquatLb = "front-squat-1rm-lb";
        public const string BackSquatLb = "back-squat-1rm-lb";
    }

    /// <summary>Units a requirement is stated in.</summary>
    public static class Units
    {
        public const string Seconds = "s";
        public const string SecondsPerMile = "s/mi";
        public const string Reps = "reps";
        public const string Bodyweights = "xbw";
        public const string Pounds = "lb";
        public const string Points = "pts";
        public const string Percent = "%";
        public const string Meters = "m";
    }

    /// <summary>A value in a requirement's unit, the way an athlete would say it.</summary>
    public static string Describe(string unit, double value) => unit switch
    {
        Units.Seconds => Format.Clock(value),
        Units.SecondsPerMile => Text($"{Format.Clock(value)}/mile"),
        Units.Reps => Text($"{value:0} reps"),
        Units.Bodyweights => Text($"{value:0.00}× bodyweight"),
        Units.Pounds => Text($"{value:0} lb ({value / BodyMass.PoundsPerKg:0} kg)"),
        Units.Points => Text($"{value:0} pts"),
        Units.Percent => Text($"{value:0.#}%"),
        Units.Meters => Text($"{value:0} m"),
        _ => Text($"{value:0.##}")
    };

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
