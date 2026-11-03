using aberaTech.Fitness.Domain;

namespace aberaTech.Fitness.Workbook;

/// <summary>How a goal's number is read and written.</summary>
public enum GoalUnit
{
    /// <summary>A duration, h:mm:ss. The sheet stores it as a fraction of a day.</summary>
    Seconds,

    /// <summary>A pace per mile, m:ss. The sheet stores it as a fraction of a day.</summary>
    SecondsPerMile,
    Reps,
    Pounds,
    Percent,
    Meters
}

/// <summary>What the console knows about one workbook row.</summary>
/// <param name="Metric">The measurement key the log is read under.</param>
/// <param name="Unit">How the value is read from the sheet and shown.</param>
/// <param name="LowerIsBetter">Times and bodyweight fall toward the goal; everything else rises.</param>
/// <param name="Section">The group the row is shown in, when it differs from the sheet's column A label.</param>
/// <param name="Aft">The Army Fitness Test event the row is, if it is one.</param>
/// <param name="FbiEvent">The FBI test event the row is, if it is one.</param>
public sealed record GoalDefinition(
    string Metric,
    GoalUnit Unit,
    bool LowerIsBetter,
    string? Section = null,
    AftEvent? Aft = null,
    FbiEvent? FbiEvent = null);

/// <summary>
/// The rows of the goals workbook the console can measure, keyed by the label
/// the athlete wrote in column B.
/// </summary>
/// <remarks>
/// A label is matched after lower-casing and dropping everything that is not
/// a letter or a digit, so "HR pushups", "HR Push-ups" and "hr push ups" are
/// one row. Each key is a prefix of that form, longest first, so "Aerobic
/// threshold pace (flat, per mile)" matches "aerobicthreshold". A row the
/// catalog does not know is still shown, marked as not measured yet: a row
/// the athlete adds is never dropped. Which test a row belongs to is the
/// sheet's business; the catalog only says how each test scores it.
/// </remarks>
public static class GoalCatalog
{
    public const string StrengthSection = "Strength";

    private static readonly (string Key, GoalDefinition Definition)[] Rows =
    [
        ("aerobicthreshold", new(SelectionReadiness.Metrics.AerobicThresholdPace, GoalUnit.SecondsPerMile, LowerIsBetter: true)),
        ("12miruck", new(SelectionReadiness.Metrics.RuckTwelveMileAt35, GoalUnit.Seconds, LowerIsBetter: true)),
        ("10milerun", new(SelectionReadiness.Metrics.RunTenMile, GoalUnit.Seconds, LowerIsBetter: true)),
        ("5milerun", new(SelectionReadiness.Metrics.RunFiveMile, GoalUnit.Seconds, LowerIsBetter: true)),
        ("2milerun", new(SelectionReadiness.Metrics.RunTwoMile, GoalUnit.Seconds, LowerIsBetter: true, Aft: AftEvent.TwoMileRun)),
        ("15mrun", new(SelectionReadiness.Metrics.RunOneAndHalfMile, GoalUnit.Seconds, LowerIsBetter: true, FbiEvent: Domain.FbiEvent.Run1_5Mile)),
        ("15milerun", new(SelectionReadiness.Metrics.RunOneAndHalfMile, GoalUnit.Seconds, LowerIsBetter: true, FbiEvent: Domain.FbiEvent.Run1_5Mile)),
        ("300msprint", new(SelectionReadiness.Metrics.Sprint300m, GoalUnit.Seconds, LowerIsBetter: true, FbiEvent: Domain.FbiEvent.Sprint300m)),
        ("max3repdeadlift", new(SelectionReadiness.Metrics.DeadliftTripleLb, GoalUnit.Pounds, LowerIsBetter: false, Aft: AftEvent.Deadlift)),
        ("hrpushups", new(SelectionReadiness.Metrics.HandReleasePushUps, GoalUnit.Reps, LowerIsBetter: false, Aft: AftEvent.HandReleasePushUps)),
        ("handreleasepushups", new(SelectionReadiness.Metrics.HandReleasePushUps, GoalUnit.Reps, LowerIsBetter: false, Aft: AftEvent.HandReleasePushUps)),
        ("sprintdragcarry", new(SelectionReadiness.Metrics.SprintDragCarry, GoalUnit.Seconds, LowerIsBetter: true, Aft: AftEvent.SprintDragCarry)),
        ("plank", new(SelectionReadiness.Metrics.Plank, GoalUnit.Seconds, LowerIsBetter: false, Aft: AftEvent.Plank)),
        ("pullups", new(SelectionReadiness.Metrics.PullUps, GoalUnit.Reps, LowerIsBetter: false, FbiEvent: Domain.FbiEvent.PullUps)),
        ("pushups", new(SelectionReadiness.Metrics.PushUps, GoalUnit.Reps, LowerIsBetter: false, FbiEvent: Domain.FbiEvent.PushUps)),
        ("bodyweight", new(SelectionReadiness.Metrics.BodyweightLb, GoalUnit.Pounds, LowerIsBetter: true)),
        ("bodyfat", new(SelectionReadiness.Metrics.BodyFat, GoalUnit.Percent, LowerIsBetter: true)),
        ("benchpress", new(SelectionReadiness.Metrics.BenchPressLb, GoalUnit.Pounds, LowerIsBetter: false, Section: StrengthSection)),
        ("frontsquat", new(SelectionReadiness.Metrics.FrontSquatLb, GoalUnit.Pounds, LowerIsBetter: false, Section: StrengthSection)),
        ("backsquat", new(SelectionReadiness.Metrics.BackSquatLb, GoalUnit.Pounds, LowerIsBetter: false, Section: StrengthSection)),
        ("farmercarry", new(SelectionReadiness.Metrics.FarmersCarryMeters, GoalUnit.Meters, LowerIsBetter: false, Section: StrengthSection))
    ];

    /// <summary>The definition for a column B label, or null for a row the console cannot measure yet.</summary>
    public static GoalDefinition? Find(string label)
    {
        var key = Normalize(label);
        return Rows
            .Where(r => key.StartsWith(r.Key, StringComparison.Ordinal))
            .OrderByDescending(r => r.Key.Length)
            .Select(r => r.Definition)
            .FirstOrDefault();
    }

    /// <summary>The name a column A label stands for on the page.</summary>
    public static string SectionName(string sheetGroup)
    {
        var group = sheetGroup.Trim();
        if (group.StartsWith("AFT", StringComparison.OrdinalIgnoreCase)) return "Army Fitness Test";
        if (group.StartsWith("FBI", StringComparison.OrdinalIgnoreCase)) return "FBI Physical Fitness Test";
        if (group.StartsWith("Body comp", StringComparison.OrdinalIgnoreCase)) return "Body composition";
        return group.Length == 0 ? "Goals" : group;
    }

    /// <summary>The sheet value in the unit the console works in: seconds for times, as written for the rest.</summary>
    public static double? Read(WorkbookCell cell, GoalUnit unit)
    {
        if (cell.Number is not { } number) return null;
        return unit is GoalUnit.Seconds or GoalUnit.SecondsPerMile ? Math.Round(number * 86400, 1) : number;
    }

    internal static string Normalize(string label) =>
        new(label.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
