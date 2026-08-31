namespace aberaTech.Fitness.Domain;

/// <summary>A world best, kept as the raw performance rather than a score.</summary>
/// <param name="Event">How the distance is normally named.</param>
/// <param name="DistanceMeters">The distance.</param>
/// <param name="Seconds">The time.</param>
/// <param name="Holder">Who ran it.</param>
/// <param name="Year">When.</param>
/// <param name="Female">Which record book it belongs to.</param>
public sealed record WorldBest(
    string Event,
    double DistanceMeters,
    double Seconds,
    string Holder,
    int Year,
    bool Female)
{
    /// <summary>The record scored by the same equations this app scores the athlete with.</summary>
    public double VdotScore => Vdot.FromRace(DistanceMeters, Seconds / 60.0);
}

/// <summary>
/// The outer edge of human distance running, and where a target sits against it.
/// </summary>
/// <remarks>
/// A goal calculator that will quote a training dose for any time you type is
/// not a model, it is a mirror. The bound here is empirical and deliberately
/// unflattering: the fastest performances on record, scored through the same
/// Daniels equations the athlete is scored with, so a target and a world record
/// are directly comparable numbers rather than two different kinds of claim.
///
/// Age-grading uses the same decline this app already applies to a lifetime
/// peak (<see cref="Retraining"/>): flat through the early thirties, then about
/// 0.7% a year, which is the World Masters Athletics shape. Records are set in
/// the mid-to-late twenties, so the open record is the age-34 value.
///
/// The percentage bands are the age-grading convention: 100% is the record,
/// 90%+ world class, 80%+ national class, 70%+ regional, 60%+ local. They are
/// reported as VDOT ratios rather than time ratios, which is the same ordering
/// and lets one number cover every distance.
///
/// Citations: <see cref="Citations.DanielsVdot"/>, <see cref="Citations.WmaAgeGrading"/>.
/// </remarks>
public static class HumanLimits
{
    /// <summary>
    /// Anchor records across the aerobic range. Only a handful: the ceiling is
    /// the maximum VDOT they imply, and adding more marks of the same era does
    /// not move a maximum. Times are the marks standing at the 2026 revision.
    /// </summary>
    public static IReadOnlyList<WorldBest> Records { get; } =
    [
        new("1500 m", 1500, 206.00, "Hicham El Guerrouj", 1998, false),
        new("5000 m", 5000, 755.36, "Joshua Cheptegei", 2020, false),
        new("10,000 m", 10_000, 1571.00, "Joshua Cheptegei", 2020, false),
        new("half marathon", 21_097.5, 3402, "Jacob Kiplimo", 2025, false),
        new("marathon", 42_195, 7235, "Kelvin Kiptum", 2023, false),
        new("1500 m", 1500, 229.04, "Faith Kipyegon", 2024, true),
        new("5000 m", 5000, 840.21, "Gudaf Tsegay", 2023, true),
        new("10,000 m", 10_000, 1734.14, "Beatrice Chebet", 2024, true),
        new("half marathon", 21_097.5, 3772, "Letesenbet Gidey", 2021, true),
        new("marathon", 42_195, 7796, "Ruth Chepngetich", 2024, true)
    ];

    /// <summary>Age at which the open record book stops being flattered by youth.</summary>
    public const int RecordAge = Retraining.PlateauEndAge;

    /// <summary>The record that scores highest in each book — the human ceiling.</summary>
    public static WorldBest BestRecord(bool female) =>
        Records.Where(r => r.Female == female).MaxBy(r => r.VdotScore)!;

    /// <summary>The highest VDOT any human has demonstrated in this record book.</summary>
    public static double OpenCeiling(bool female) => BestRecord(female).VdotScore;

    /// <summary>
    /// The human ceiling discounted to <paramref name="age"/>. Null age means
    /// the open ceiling, which is the most permissive reading.
    /// </summary>
    public static double AgeGradedCeiling(bool female, int? age)
    {
        var open = OpenCeiling(female);
        if (age is not { } years || years <= RecordAge) return open;
        return Retraining.AgeAdjustedPeak(open, RecordAge, years);
    }

    /// <summary>Where a performance sits against the age-graded record: 1.0 is the record.</summary>
    public static double Grade(double vdot, bool female, int? age)
    {
        if (vdot <= 0) throw new ArgumentOutOfRangeException(nameof(vdot));
        return vdot / AgeGradedCeiling(female, age);
    }

    /// <summary>The age-grading convention's name for a percentage band.</summary>
    public static string Band(double grade) => grade switch
    {
        >= 1.0 => "past the world record",
        >= 0.90 => "world class",
        >= 0.80 => "national class",
        >= 0.70 => "regional class",
        >= 0.60 => "local class",
        _ => "recreational"
    };

    /// <summary>
    /// The record nearest in distance, for quoting a target against something
    /// an athlete recognises.
    /// </summary>
    public static WorldBest NearestRecord(double distanceMeters, bool female) =>
        Records
            .Where(r => r.Female == female)
            .MinBy(r => Math.Abs(Math.Log(r.DistanceMeters / distanceMeters)))!;

    /// <summary>What the record holder's VDOT would run over an arbitrary distance.</summary>
    public static double RecordEquivalentSeconds(double distanceMeters, bool female, int? age) =>
        Vdot.MinutesFor(distanceMeters, AgeGradedCeiling(female, age)) * 60.0;

    /// <summary>The trace for a grading, so the athlete can check the arithmetic.</summary>
    public static IReadOnlyList<CalculationStep> Explain(double vdot, bool female, int? age)
    {
        var best = BestRecord(female);
        var ceiling = AgeGradedCeiling(female, age);
        var trace = new CalculationTrace()
            .Add(
                "Human ceiling",
                $"{best.Holder}'s {best.Event} {Format.Clock(best.Seconds)} ({best.Year}) scored by the same equations",
                $"VDOT {best.VdotScore:0.0}",
                Citations.DanielsVdot.Id);

        if (age is { } years && years > RecordAge)
        {
            trace.Add(
                "Age-graded ceiling",
                $"{best.VdotScore:0.0} × (1 − 0.007)^({years} − {RecordAge})",
                $"VDOT {ceiling:0.0}",
                Citations.WmaAgeGrading.Id);
        }

        var grade = vdot / ceiling;
        return trace
            .Add(
                "Where the target sits",
                $"{vdot:0.0} ÷ {ceiling:0.0}",
                $"{grade:P0} of the record — {Band(grade)}",
                Citations.WmaAgeGrading.Id)
            .Steps;
    }
}
