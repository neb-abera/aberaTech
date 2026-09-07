using System.Globalization;
using System.Text;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>The week in one page: the numbers a Monday morning should open with.</summary>
public sealed record DigestDto(
    string Date,
    string WeekStart,
    string Text,
    IReadOnlyList<string> Lines);

/// <summary>
/// Writes the week up as plain text, for the morning brief and for the
/// dashboard's "this week" card. One source, so the brief and the console
/// never disagree.
/// </summary>
internal static class DigestReports
{
    public static async Task<DigestDto> BuildAsync(FitnessDbContext database, LocalDate today, CancellationToken cancellationToken)
    {
        var summary = await FitnessReports.SummaryAsync(database, cancellationToken);
        var outlook = await OutlookReports.BuildAsync(database, null, 1.0, today, cancellationToken);
        return Compose(summary, outlook, today);
    }

    /// <summary>The page itself, from the two reports it is written from.</summary>
    internal static DigestDto Compose(SummaryDto summary, OutlookDto outlook, LocalDate today)
    {
        var weekStart = today.PlusDays(-(((int)today.DayOfWeek - 1 + 7) % 7));

        var lines = new List<string>();
        var settings = summary.Settings;

        lines.Add(Text($"abera.tech/fitness — week of {weekStart:yyyy-MM-dd}"));
        lines.Add("");

        // What was done.
        var thisWeek = summary.WeeklyVolume.LastOrDefault();
        var lastWeek = summary.WeeklyVolume.Count >= 2 ? summary.WeeklyVolume[^2] : null;
        lines.Add("TRAINING");
        lines.Add(Text($"  Endurance: {thisWeek?.Minutes ?? 0:0} min this week so far, {lastWeek?.Minutes ?? 0:0} min last week, plan {settings.PlanMinutesPerWeek:0}."));
        var dose = summary.MeasuredDose;
        lines.Add(Text($"  8-week average: {dose.RunningHours:0.0} h/wk running ({Format.Percent(dose.EasyShare)} easy), {dose.StrengthHours:0.0} h strength."));

        var d = summary.Durability;
        lines.Add(Text($"  Load: acute:chronic {(d.Acwr is { } a ? a.ToString("0.00", CultureInfo.InvariantCulture) : "n/a")}, monotony {(d.Monotony is { } m ? m.ToString("0.0", CultureInfo.InvariantCulture) : "n/a")}, {d.ImpactStreakDays} days of impact in a row, {d.RestDaysLast7} rest days in 7."));
        lines.Add("");

        // The engine.
        lines.Add("ENGINE");
        if (summary.AerobicTrend.Count > 0)
        {
            var latest = summary.AerobicTrend[^1];
            var previous = summary.AerobicTrend.Count >= 2 ? summary.AerobicTrend[^2] : null;
            var change = previous is null ? "" : Text($" ({Format.Percent((previous.MedianSecPerKm - latest.MedianSecPerKm) / previous.MedianSecPerKm, 1)} vs {previous.Month})");
            lines.Add(Text($"  AeT pace at {settings.ReferenceHr} bpm: {Format.Pace(latest.MedianSecPerKm)}/km over {latest.Runs} runs in {latest.Month}{change}{(latest.IndoorRuns > 0 ? Text($", {latest.IndoorRuns} on a treadmill") : "")}."));
        }
        lines.Add(Text($"  VDOT anchor {settings.StartVdot:0.0}{(settings.VdotMeasuredOn is { } on ? Text($" from {on}") : "")}; 2-mile now ≈ {Format.Clock(Vdot.MinutesFor(2 * Vdot.MileMeters, settings.StartVdot) * 60)}."));
        if (summary.DeficiencySpread is { } spread)
        {
            lines.Add(Text($"  AeT–LT spread {Format.Percent(spread)} ({(spread > AerobicAnalysis.DeficiencyThreshold ? "over the 10% line: base volume" : "inside the 10% line")})."));
        }
        if (summary.ThresholdSuggestion is { } suggestion)
        {
            lines.Add(Text($"  Thresholds: a test suggests {Describe(suggestion)}. Apply it on the dashboard."));
        }
        lines.Add("");

        // The gates.
        lines.Add(Text($"GATES{(outlook.SelectionDate is { } sd ? Text($" (selection {sd})") : " (no selection date set)")}"));
        foreach (var gate in outlook.Gates)
        {
            var scored = summary.Readiness.Gates.FirstOrDefault(g => g.Id == gate.Id);
            var chance = gate.Probability is { } p ? Text($"{Format.Percent(p)} by {gate.DueOn ?? "12 mo out"}") : "not forecast";
            lines.Add(Text($"  {gate.Name}: {scored?.Passed ?? 0}/{gate.Total} clear today; {chance} at {outlook.WeeklyHours:0.0} h/wk ({gate.Forecast}/{gate.Total} lines)."));
        }
        lines.Add(outlook.EarliestSelectionDate is { } earliest
            ? Text($"  Earliest selection every forecast gate is ready for: {earliest} (set by {outlook.BindingGate}).")
            : "  No selection date can be named yet: some forecast line never reaches 80% at this week.");
        lines.Add("");

        // What needs doing.
        var actions = summary.Highlights.Where(h => !h.Positive).ToArray();
        var wins = summary.Highlights.Where(h => h.Positive).ToArray();
        lines.Add("ATTENTION");
        if (actions.Length == 0) lines.Add("  Nothing flagged.");
        foreach (var h in actions) lines.Add(Text($"  - {h.Headline}. {h.Evidence}"));
        if (wins.Length > 0)
        {
            lines.Add("PROGRESS");
            foreach (var h in wins) lines.Add(Text($"  + {h.Headline}. {h.Evidence}"));
        }

        return new DigestDto(
            today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            string.Join("\n", lines),
            lines);
    }

    private static string Describe(ThresholdSuggestionDto s)
    {
        var parts = new List<string>();
        if (s.AetHr is { } aet) parts.Add(Text($"AeT {aet} bpm"));
        if (s.LtHr is { } lt) parts.Add(Text($"LT {lt} bpm"));
        if (s.LtSecPerKm is { } pace) parts.Add(Text($"LT pace {Format.Pace(pace)}/km"));
        return string.Join(", ", parts);
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
