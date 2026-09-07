using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>A locked prediction as the nudge sees it.</summary>
public sealed record LedgerEntry(Guid Id, LocalDate MadeOn, LocalDate TargetDate, double DistanceMeters, double PredictedSeconds, double? ActualSeconds);

/// <summary>
/// The discipline of the prediction ledger: a prediction on the books before
/// every test, and a score after it.
/// </summary>
/// <remarks>
/// A model that is never held to a number is never wrong, and never
/// improves. The ledger exists so that the console's forecasts can be
/// scored; these two findings are what keeps it in use — an empty ledger
/// ahead of a known test, and a due prediction nobody has scored.
/// </remarks>
public static class Ledger
{
    /// <summary>A test this close with nothing locked is the nudge.</summary>
    public const int NudgeWindowDays = 120;

    public static IReadOnlyList<Highlight> Highlights(IReadOnlyList<LedgerEntry> entries, LocalDate? nextTest, LocalDate today)
    {
        var highlights = new List<Highlight>();

        var due = entries.Where(e => e.ActualSeconds is null && e.TargetDate <= today).ToArray();
        if (due.Length > 0)
        {
            highlights.Add(new Highlight("ledger-due",
                due.Length == 1 ? "A locked prediction is due to be scored" : Text($"{due.Length} locked predictions are due to be scored"),
                Text($"The earliest, for {Format.Distance(due[0].DistanceMeters)} on {due[0].TargetDate:yyyy-MM-dd}, said {Format.Clock(due[0].PredictedSeconds)}. Enter what happened on the Solve tab so the model's intervals can be checked."),
                Positive: false));
        }

        var pending = entries.Any(e => e.ActualSeconds is null && e.TargetDate > today);
        if (!pending)
        {
            var when = nextTest is { } test && test > today && test <= today.PlusDays(NudgeWindowDays)
                ? Text($"Your next dated test is {test:yyyy-MM-dd}; ")
                : "";
            highlights.Add(new Highlight("ledger-empty",
                "Nothing locked in the prediction ledger",
                when + "lock the model's 2-mile for the next test on the Solve tab, so it can be scored when the time comes. A forecast nobody wrote down was never a forecast.",
                Positive: false));
        }

        var scored = entries.Where(e => e.ActualSeconds is not null).ToArray();
        if (scored.Length >= 3)
        {
            var inside = scored.Count(e => Math.Abs(e.ActualSeconds!.Value - e.PredictedSeconds) / e.PredictedSeconds <= 0.03);
            highlights.Add(new Highlight("ledger-record",
                Text($"{scored.Length} predictions scored; {inside} within 3%"),
                Text($"Median miss {Median(scored.Select(e => Math.Abs(e.ActualSeconds!.Value - e.PredictedSeconds) / e.PredictedSeconds)):0.0%}. The intervals are what to judge the model on, not the point."),
                Positive: inside * 2 >= scored.Length));
        }

        return highlights;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var n = sorted.Length;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
