using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>Where a number came from: a test the athlete took, or the model's estimate.</summary>
public enum Basis
{
    Measured,
    Modeled
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

/// <summary>A standard the athlete has to clear by a date on the way to selection.</summary>
/// <param name="Id">Stable key.</param>
/// <param name="Name">What the gate is.</param>
/// <param name="Purpose">Why it is on the list.</param>
/// <param name="WeeksBeforeSelection">How far ahead of the selection date it should be cleared.</param>
/// <param name="Requirements">The lines the log can score.</param>
/// <param name="Untracked">The lines it cannot, said out loud rather than dropped.</param>
public sealed record Gate(
    string Id,
    string Name,
    string Purpose,
    int WeeksBeforeSelection,
    IReadOnlyList<Requirement> Requirements,
    IReadOnlyList<string> Untracked);

public enum GateStatus
{
    Pass,
    Fail,
    Unknown
}

/// <summary>One requirement, against what the athlete has.</summary>
public sealed record RequirementResult(Requirement Requirement, GateStatus Status, Measurement? Current, string Gap);

/// <summary>One gate, against what the athlete has.</summary>
public sealed record GateResult(
    Gate Gate,
    LocalDate? DueOn,
    GateStatus Status,
    int Passed,
    int Known,
    IReadOnlyList<RequirementResult> Requirements);

/// <summary>
/// The published standards between this athlete and a Special Forces slot,
/// as gates with dates, scored from the log.
/// </summary>
/// <remarks>
/// A goal list of running times cannot say whether someone is ready for a
/// selection course, because selection is not a running race. The standards
/// that actually gate the path are published — the day-one drop line at
/// SFAS, a Guard company's readiness-evaluation prerequisites, the entry test
/// for the final training block — and each is a handful of numbers over
/// rucking, calisthenics, strength and a run. They are transcribed here with
/// their sources, ordered by when they have to be cleared, and scored against
/// whatever the log can say; what it cannot say is listed, not hidden.
///
/// The dates hang off the selection date the athlete names — the readiness
/// evaluation is the first gate a Guard candidate faces — and count back:
/// the final block's entry test has to be passed before the block begins,
/// the day-one minimums should be in hand a year out.
///
/// Citations: <see cref="Citations.SfasDayOne"/>,
/// <see cref="Citations.EvokeSelectionPrep"/>,
/// <see cref="Citations.GuardSfrePrerequisites"/>,
/// <see cref="Citations.SfasCompetitive"/>, <see cref="Citations.ArmyAft"/>,
/// <see cref="Citations.FarinaSfasBody"/>.
/// </remarks>
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

    private const double Mile = Vdot.MileMeters;

    public static IReadOnlyList<Gate> Gates { get; } =
    [
        new Gate(
            "aft-combat",
            "AFT combat standard",
            "The fitness test every combat specialty has to pass, and the score the recruiter sees first.",
            52,
            [
                new Requirement(Metrics.AftLowestEvent, "Lowest AFT event", Comparison.AtLeast, Aft.CombatMinimumPerEvent, Units.Points, Citations.ArmyAft.Id),
                new Requirement(Metrics.AftTotal, "AFT total", Comparison.AtLeast, Aft.CombatMinimumTotal, Units.Points, Citations.ArmyAft.Id)
            ],
            []),
        new Gate(
            "sfas-day-one",
            "SFAS day-one minimums",
            "Under any of these on the first morning and the course is over before it starts. A year out, these should already be comfortable.",
            52,
            [
                new Requirement(Metrics.RunTwoMile, "Two-mile run", Comparison.AtMost, 15 * 60 + 12, Units.Seconds, Citations.SfasDayOne.Id),
                new Requirement(Metrics.HandReleasePushUps, "Hand-release push-ups", Comparison.AtLeast, 28, Units.Reps, Citations.SfasDayOne.Id),
                new Requirement(Metrics.PullUps, "Pull-ups", Comparison.AtLeast, 6, Units.Reps, Citations.SfasDayOne.Id)
            ],
            []),
        new Gate(
            "selection-prep-entry",
            "Selection Prep entry test",
            "What Evoke expects before the final fifteen-week block; it has to be passed before that block can start.",
            20,
            [
                new Requirement(Metrics.RunTwoMile, "Two-mile run", Comparison.AtMost, 14 * 60, Units.Seconds, Citations.EvokeSelectionPrep.Id),
                new Requirement(Metrics.RunFiveMile, "Five-mile run", Comparison.AtMost, 45 * 60, Units.Seconds, Citations.EvokeSelectionPrep.Id),
                new Requirement(Metrics.RuckTwelveMileAt35, "Twelve-mile ruck at 35 lb", Comparison.AtMost, 3 * 3600, Units.Seconds, Citations.EvokeSelectionPrep.Id),
                new Requirement(Metrics.HandReleasePushUps, "Hand-release push-ups", Comparison.AtLeast, 40, Units.Reps, Citations.EvokeSelectionPrep.Id),
                new Requirement(Metrics.PullUps, "Pull-ups", Comparison.AtLeast, 10, Units.Reps, Citations.EvokeSelectionPrep.Id),
                new Requirement(Metrics.FrontSquatTripleToBodyweight, "Front squat 3RM", Comparison.AtLeast, 1.0, Units.Bodyweights, Citations.EvokeSelectionPrep.Id),
                new Requirement(Metrics.DeadliftTripleToBodyweight, "Deadlift 3RM", Comparison.AtLeast, 1.5, Units.Bodyweights, Citations.EvokeSelectionPrep.Id)
            ],
            []),
        new Gate(
            "sfre-prerequisites",
            "SFRE prerequisites",
            "What a Guard Special Forces company publishes as the price of a seat at its readiness evaluation, the gate to an SFAS slot.",
            8,
            [
                new Requirement(Metrics.RunTwoMile, "Two-mile run", Comparison.AtMost, 13 * 60 + 42, Units.Seconds, Citations.GuardSfrePrerequisites.Id),
                new Requirement(Metrics.PushUps, "Push-ups in two minutes", Comparison.AtLeast, 64, Units.Reps, Citations.GuardSfrePrerequisites.Id),
                new Requirement(Metrics.SitUps, "Sit-ups in two minutes", Comparison.AtLeast, 72, Units.Reps, Citations.GuardSfrePrerequisites.Id),
                new Requirement(Metrics.RuckTwelveMileAt45, "Twelve-mile ruck at 45 lb dry", Comparison.AtMost, 3 * 3600, Units.Seconds, Citations.GuardSfrePrerequisites.Id)
            ],
            ["Rope climb", "100 m swim", "Pull-ups (no count published)"]),
        new Gate(
            "evoke-sfas-ready",
            "Paikowski's SFAS-ready numbers",
            "Where the Selection Prep block is meant to leave you: what Vince Paikowski and the Evoke coaches give when asked what ready for SFAS looks like, not the entry test. No ruck line on purpose — Paikowski's best ruckers were simply the best runners, and the aerobic-threshold pace is the number that moves the ruck; the ruck itself is scored on the competitive row.",
            4,
            [
                new Requirement(Metrics.AerobicThresholdPace, "Aerobic-threshold pace on the flat", Comparison.AtMost, 8 * 60, Units.SecondsPerMile, Citations.EvokeSfasReady.Id),
                new Requirement(Metrics.RunFiveMile, "Five-mile run", Comparison.AtMost, 35 * 60, Units.Seconds, Citations.EvokeSfasReady.Id),
                new Requirement(Metrics.PullUps, "Strict pull-ups", Comparison.AtLeast, 15, Units.Reps, Citations.EvokeSfasReady.Id),
                new Requirement(Metrics.PushUps, "Push-ups in two minutes", Comparison.AtLeast, 80, Units.Reps, Citations.EvokeSfasReady.Id),
                new Requirement(Metrics.DeadliftTripleLb, "Deadlift 3RM", Comparison.AtLeast, 350, Units.Pounds, Citations.EvokeSfasReady.Id),
                new Requirement(Metrics.BackSquatFiveLb, "Back squat 5RM", Comparison.AtLeast, 250, Units.Pounds, Citations.EvokeSfasReady.Id)
            ],
            ["Deadlift + squat + overhead press total of 750 lb"]),
        new Gate(
            "sfas-competitive",
            "Competitive at selection",
            "The row selected candidates sit on. The minimums get you in the door; these are what the cadre see from someone who belongs there.",
            0,
            [
                new Requirement(Metrics.RunTwoMile, "Two-mile run", Comparison.AtMost, 13 * 60 + 30, Units.Seconds, Citations.SfasDayOne.Id),
                new Requirement(Metrics.RunFiveMile, "Five-mile run", Comparison.AtMost, 35 * 60, Units.Seconds, Citations.SfasCompetitive.Id),
                new Requirement(Metrics.RuckTwelveMileAt45, "Twelve-mile ruck at 45 lb", Comparison.AtMost, 2 * 3600 + 45 * 60, Units.Seconds, Citations.SfasCompetitive.Id),
                new Requirement(Metrics.PullUps, "Pull-ups", Comparison.AtLeast, 12, Units.Reps, Citations.SfasCompetitive.Id),
                new Requirement(Metrics.HandReleasePushUps, "Hand-release push-ups", Comparison.AtLeast, 40, Units.Reps, Citations.SfasDayOne.Id),
                new Requirement(Metrics.BodyFat, "Body fat", Comparison.AtMost, 15, Units.Percent, Citations.FarinaSfasBody.Id)
            ],
            ["Land navigation, day and night", "Back-to-back days under load"]),
        new Gate(
            "own-standards",
            "Your own 2027 standards",
            "The physical fitness goals workbook, scored from the log. It is the one row with a grip line: no selection publishes a grip standard, and the farmer's carry is the grip-endurance test that most resembles what selection actually asks of the hands.",
            0,
            [
                new Requirement(Metrics.FarmersCarryMeters, "Farmer's carry at 1.5× bodyweight", Comparison.AtLeast, 100, Units.Meters, Citations.NebFitnessGoals.Id),
                new Requirement(Metrics.PullUps, "Pull-ups", Comparison.AtLeast, 15, Units.Reps, Citations.NebFitnessGoals.Id),
                new Requirement(Metrics.PushUps, "Push-ups in two minutes", Comparison.AtLeast, 80, Units.Reps, Citations.NebFitnessGoals.Id),
                new Requirement(Metrics.HandReleasePushUps, "Hand-release push-ups", Comparison.AtLeast, 62, Units.Reps, Citations.NebFitnessGoals.Id),
                new Requirement(Metrics.DeadliftTripleLb, "Deadlift 3RM", Comparison.AtLeast, 350, Units.Pounds, Citations.NebFitnessGoals.Id),
                new Requirement(Metrics.BodyweightLb, "Bodyweight", Comparison.AtMost, 170, Units.Pounds, Citations.NebFitnessGoals.Id),
                new Requirement(Metrics.BodyFat, "Body fat", Comparison.AtMost, 10, Units.Percent, Citations.NebFitnessGoals.Id)
            ],
            ["500-yard swim in 8:00", "Back squat 315 (a single, not the five the gates read)"])
    ];

    /// <summary>Every gate, scored against what the athlete has.</summary>
    /// <param name="measurements">The latest standing on each metric, by key; a metric absent here is unknown.</param>
    /// <param name="selectionDate">The readiness evaluation the gates count back from, if named.</param>
    public static IReadOnlyList<GateResult> Evaluate(
        IReadOnlyDictionary<string, Measurement> measurements, LocalDate? selectionDate)
    {
        return Gates
            .Select(gate =>
            {
                var results = gate.Requirements.Select(r => Score(r, measurements)).ToArray();
                var known = results.Count(r => r.Status != GateStatus.Unknown);
                var passed = results.Count(r => r.Status == GateStatus.Pass);

                var status = results.Any(r => r.Status == GateStatus.Fail) ? GateStatus.Fail
                    : known < results.Length ? GateStatus.Unknown
                    : GateStatus.Pass;

                return new GateResult(
                    gate,
                    selectionDate?.PlusWeeks(-gate.WeeksBeforeSelection),
                    status,
                    passed,
                    known,
                    results);
            })
            .ToArray();
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

    private static RequirementResult Score(Requirement requirement, IReadOnlyDictionary<string, Measurement> measurements)
    {
        if (!measurements.TryGetValue(requirement.Metric, out var current))
        {
            return new RequirementResult(requirement, GateStatus.Unknown, null, "nothing in the log scores this yet");
        }

        var passes = requirement.Comparison == Comparison.AtLeast
            ? current.Value >= requirement.Target
            : current.Value <= requirement.Target;

        var margin = requirement.Comparison == Comparison.AtLeast
            ? current.Value - requirement.Target
            : requirement.Target - current.Value;

        var gap = passes
            ? Text($"clear by {Describe(requirement.Unit, Math.Abs(margin))}")
            : Text($"short by {Describe(requirement.Unit, Math.Abs(margin))}");

        return new RequirementResult(requirement, passes ? GateStatus.Pass : GateStatus.Fail, current, gap);
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
