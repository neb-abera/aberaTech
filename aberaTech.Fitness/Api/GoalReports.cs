using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using aberaTech.Fitness.OneDrive;
using aberaTech.Fitness.Workbook;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>Where the goals came from, and whether the read worked.</summary>
public sealed record GoalSourceDto(
    string Origin,
    bool Configured,
    bool Connected,
    string? ReadAt,
    string? Modified,
    string? Error,
    string? SheetName);

/// <summary>The forecast for one goal on its due date, from the training week the Plan tab states.</summary>
public sealed record GoalForecastDto(
    double? Probability,
    double? Projected,
    string Method,
    string Evidence,
    double? HoursToReach);

/// <summary>One essential workbook row, against the log.</summary>
/// <param name="Row">The sheet row, for "see row 7".</param>
/// <param name="Metric">The measurement it is read under; null when the console cannot measure it yet.</param>
/// <param name="Unit">s, s/mi, reps, lb, %, m.</param>
/// <param name="Goal">Column F, or column D when F is empty.</param>
/// <param name="GoalColumn">Which column the goal came from.</param>
/// <param name="Met">Whether the current value already meets the goal.</param>
/// <param name="PointsNow">Test points the current value earns, on a test row.</param>
/// <param name="PointsGoal">Test points the goal earns.</param>
/// <param name="Note">Column I, the athlete's evidence note.</param>
public sealed record GoalRowDto(
    int Row,
    string Label,
    string? Metric,
    string Unit,
    bool LowerIsBetter,
    double? Baseline,
    string? BaselineText,
    double? Goal,
    string GoalColumn,
    StandingDto? Current,
    bool? Met,
    int? PointsNow,
    int? PointsGoal,
    GoalForecastDto? Forecast,
    string? Note);

/// <summary>One group of rows: a section of the workbook, or a test scored the way its graders score it.</summary>
/// <param name="Test">aft or fbi for a test, null otherwise.</param>
/// <param name="PointsNow">The test total today, when every event has a current value.</param>
/// <param name="PointsGoal">The test total the goals add up to.</param>
/// <param name="PointsMax">The most the test awards.</param>
/// <param name="ScoringNote">How the points were read: the table, the band, the sex.</param>
public sealed record GoalSectionDto(
    string Name,
    string? Test,
    int? PointsNow,
    int? PointsGoal,
    int? PointsMax,
    string? ScoringNote,
    IReadOnlyList<GoalRowDto> Rows);

public sealed record GoalsDto(
    GoalSourceDto Source,
    string? DueOn,
    double WeeklyHours,
    string WeekBasis,
    IReadOnlyList<GoalSectionDto> Sections);

/// <summary>
/// The Goals tab: the workbook's essential rows, in its groups and order,
/// each with its current value, its source, its test points and its forecast
/// on the due date.
/// </summary>
internal static class GoalReports
{
    public const string AftTest = "aft";
    public const string FbiTest = "fbi";

    public static async Task<GoalsDto> BuildAsync(
        FitnessDbContext database,
        IWorkbookSource source,
        double? weeklyHours,
        double compliance,
        Instant now,
        CancellationToken cancellationToken)
    {
        var read = await source.ReadAsync(cancellationToken);
        var history = await TrainingHistory.LoadAsync(database, cancellationToken);
        var evidence = await ReadinessEvidence.LoadAsync(database, cancellationToken);
        return Build(read, history, evidence, weeklyHours, compliance, now.InUtc().Date, now);
    }

    internal static GoalsDto Build(
        WorkbookRead read,
        TrainingHistory history,
        ReadinessEvidence evidence,
        double? weeklyHours,
        double compliance,
        LocalDate today,
        Instant now)
    {
        var sourceDto = new GoalSourceDto(
            read.Origin,
            read.Configured,
            read.Connected,
            read.ReadAt?.ToString("uuuu-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            read.Modified?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            read.Error,
            read.Sheet?.SheetName);

        var frame = OutlookReports.FrameFor(history, evidence, weeklyHours, compliance, today, now);

        if (read.Sheet is not { } sheet)
        {
            return new GoalsDto(sourceDto, null, frame.Hours, frame.Basis, []);
        }

        var dueOn = sheet.GoalYear is { } year ? new LocalDate(year, 12, 31) : (LocalDate?)null;
        var months = dueOn is { } due ? Math.Max(0, Period.Between(today, due, PeriodUnits.Days).Days / 30.4375) : GateOutlook.DefaultHorizonMonths;

        var row = history.Row;
        var female = row.Female ?? false;
        var ageNow = row.BirthYear is { } born ? Math.Max(0, today.Year - born) : 0;
        var ageThen = row.BirthYear is { } b && dueOn is { } d ? Math.Max(0, d.Year - b) : ageNow;

        var sections = new List<(string Name, string? Test, List<GoalRowDto> Rows, List<(int? Now, int? Goal)> Points)>();
        foreach (var workbookRow in sheet.Rows)
        {
            var definition = GoalCatalog.Find(workbookRow.Label);
            var sheetSection = GoalCatalog.SectionName(workbookRow.Group);
            var name = definition?.Section ?? sheetSection;
            var test = definition?.Section is null ? TestOf(workbookRow.Group) : null;

            var section = sections.FirstOrDefault(s => s.Name == name);
            if (section.Rows is null)
            {
                section = (name, test, [], []);
                sections.Add(section);
            }

            var (dto, points) = RowFor(workbookRow, definition, test, frame, months, female, ageNow, ageThen);
            section.Rows.Add(dto);
            section.Points.Add(points);
        }

        return new GoalsDto(
            sourceDto,
            dueOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            frame.Hours,
            frame.Basis,
            sections.Select(s => Section(s.Name, s.Test, s.Rows, s.Points, female, ageNow, ageThen)).ToArray());
    }

    private static string? TestOf(string sheetGroup) =>
        sheetGroup.TrimStart().StartsWith("AFT", StringComparison.OrdinalIgnoreCase) ? AftTest
        : sheetGroup.TrimStart().StartsWith("FBI", StringComparison.OrdinalIgnoreCase) ? FbiTest
        : null;

    private static (GoalRowDto Row, (int? Now, int? Goal) Points) RowFor(
        WorkbookRow workbookRow,
        GoalDefinition? definition,
        string? test,
        OutlookReports.Frame frame,
        double months,
        bool female,
        int ageNow,
        int ageThen)
    {
        var unit = definition?.Unit ?? GoalUnit.Reps;
        var goalColumn = workbookRow.Goal.Number is null && workbookRow.Ideal.Number is not null ? "D" : "F";
        var goal = GoalCatalog.Read(goalColumn == "D" ? workbookRow.Ideal : workbookRow.Goal, unit);
        var baseline = GoalCatalog.Read(workbookRow.Baseline, unit);

        if (definition is null)
        {
            return (new GoalRowDto(
                workbookRow.Row, workbookRow.Label, null, "", false,
                workbookRow.Baseline.Number, workbookRow.Baseline.Text,
                (goalColumn == "D" ? workbookRow.Ideal : workbookRow.Goal).Number, goalColumn,
                null, null, null, null, null, workbookRow.Evidence), (null, null));
        }

        var current = frame.Readings.Measurements.GetValueOrDefault(definition.Metric);
        bool? met = current is not null && goal is { } g
            ? definition.LowerIsBetter ? current.Value <= g : current.Value >= g
            : null;

        int? pointsNow = null, pointsGoal = null;
        if (test == AftTest && definition.Aft is { } aftEvent)
        {
            pointsNow = current is not null ? Aft.Points(aftEvent, current.Value, ageNow, female) : null;
            pointsGoal = goal is { } ag ? Aft.Points(aftEvent, ag, ageThen, female) : null;
        }
        else if (test == FbiTest && definition.FbiEvent is { } fbiEvent)
        {
            pointsNow = current is not null ? Fbi.Points(fbiEvent, current.Value, female) : null;
            pointsGoal = goal is { } fg ? Fbi.Points(fbiEvent, fg, female) : null;
        }

        GoalForecastDto? forecast = null;
        if (goal is { } target)
        {
            var requirement = new Requirement(
                definition.Metric,
                workbookRow.Label,
                definition.LowerIsBetter ? Comparison.AtMost : Comparison.AtLeast,
                target,
                UnitText(unit),
                Citations.NebFitnessGoals.Id);
            var line = GateOutlook.Line(requirement, months, frame.Context);
            forecast = new GoalForecastDto(line.Probability, line.Projected, line.Method, line.Evidence, line.HoursToReach);
        }

        return (new GoalRowDto(
            workbookRow.Row,
            workbookRow.Label,
            definition.Metric,
            UnitText(unit),
            definition.LowerIsBetter,
            baseline,
            workbookRow.Baseline.Number is null ? workbookRow.Baseline.Text : null,
            goal,
            goalColumn,
            current is null ? null : new StandingDto(current.Metric, current.Value, current.Basis.ToString(), current.Evidence,
                current.On?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            met,
            pointsNow,
            pointsGoal,
            forecast,
            workbookRow.Evidence), (pointsNow, pointsGoal));
    }

    private static GoalSectionDto Section(
        string name, string? test, List<GoalRowDto> rows, List<(int? Now, int? Goal)> points, bool female, int ageNow, int ageThen)
    {
        if (test is null) return new GoalSectionDto(name, null, null, null, null, null, rows);

        var events = test == AftTest ? Aft.Events.Count : Fbi.Events.Count;
        var scored = points.Where(p => p.Now is not null || p.Goal is not null).ToArray();

        // A total is only a total when every event is in it.
        int? now = scored.Length == events && scored.All(p => p.Now is not null) ? scored.Sum(p => p.Now!.Value) : null;
        int? goal = scored.Length == events && scored.All(p => p.Goal is not null) ? scored.Sum(p => p.Goal!.Value) : null;
        var max = test == AftTest ? Aft.MaxPoints * events : Fbi.MaxPoints * events;

        var note = test == AftTest
            ? Text($"Army Fitness Test tables, {(female ? "women" : "men")}, age band {Aft.AgeBands[Aft.AgeBandIndex(ageNow)]} today and {Aft.AgeBands[Aft.AgeBandIndex(ageThen)]} on the due date. Today's total adds each event's best current reading.")
            : Text($"FBI special agent PFT tables, {(female ? "women" : "men")}: -2 to 10 points an event. Today's total adds each event's best current reading.");

        return new GoalSectionDto(name, test, now, goal, max, note, rows);
    }

    private static string UnitText(GoalUnit unit) => unit switch
    {
        GoalUnit.Seconds => "s",
        GoalUnit.SecondsPerMile => "s/mi",
        GoalUnit.Reps => "reps",
        GoalUnit.Pounds => "lb",
        GoalUnit.Percent => "%",
        GoalUnit.Meters => "m",
        _ => ""
    };

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
