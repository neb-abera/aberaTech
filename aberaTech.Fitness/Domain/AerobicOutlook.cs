using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One week's median pace at the reference heart rate, from its steady runs.</summary>
public sealed record WeekPace(LocalDate WeekStart, double MedianSecPerKm, int Runs);

/// <summary>A projected pace, with the paces the study's slower and faster groups give.</summary>
public sealed record PacePoint(int WeeksAhead, double SecPerKm, double LowSecPerKm, double HighSecPerKm);

/// <summary>The short-term outlook, or what it still needs.</summary>
/// <param name="Anchor">The drift test it starts from.</param>
/// <param name="Needs">What it is waiting for, when it is not ready.</param>
/// <param name="Weeks">Each week's median pace at the reference heart rate, for the record. They move no forecast.</param>
public sealed record AerobicOutlookResult(
    bool Ready,
    FieldTest? Anchor,
    string? Needs,
    IReadOnlyList<WeekPace> Weeks,
    IReadOnlyList<PacePoint> Ahead);

/// <summary>
/// Pace at the AeT heart rate 4 and 8 weeks ahead: the latest drift test,
/// carried forward at the rate a training study measured.
/// </summary>
/// <remarks>
/// The outlook starts from a test he ran and named, and applies a published
/// rate. Recreational runners gained 4.0% in speed at the first ventilatory
/// threshold in 8 weeks of mostly easy running, and 3.2% on a
/// threshold-focused plan (Festa 2019). The center is the mean of the two
/// groups, 3.6%, and the range is the two groups. Inside the 8 weeks the
/// gain is taken as even.
///
/// Until 2026-11-24 this was a straight line through the weekly medians of
/// his easy runs. His October runs were 20 to 45 minutes on one treadmill.
/// His November runs were 45 to 60 minutes on three others, one of them at
/// 1,200 m, in a calorie deficit. The medians stepped from 6:08 to 7:01 a
/// kilometer, and the line carried the step ahead as 7.3 s/km slower every
/// week: 13:00 a mile in 8 weeks, for a man 3 weeks into a training plan.
/// Runs on different machines, at different lengths and altitudes, are not
/// one series. The weekly medians are still reported, and move nothing.
///
/// A test older than 8 weeks is past the study's length, so the outlook
/// asks for a new one.
///
/// Citation: <see cref="Citations.FestaThresholdSpeed"/>.
/// </remarks>
public static class AerobicOutlook
{
    /// <summary>Weeks of medians reported.</summary>
    public const int WindowWeeks = 12;

    /// <summary>Festa 2019: the gain in threshold speed over 8 weeks, in its two groups.</summary>
    public const double SlowerGroupGain = 0.032;

    public const double FasterGroupGain = 0.040;

    /// <summary>The length of the study the rate comes from.</summary>
    public const int StudyWeeks = 8;

    public static readonly int[] Horizons = [4, 8];

    public const string NeedsTest =
        "A heart-rate drift test: 60 minutes at a steady pace, named \"HR drift test\". The outlook starts from a test you ran.";

    public static AerobicOutlookResult Build(IEnumerable<SteadyRun> runs, int referenceHr, LocalDate today, FieldTest? anchor = null)
    {
        var thisWeek = today.PlusDays(-(((int)today.DayOfWeek - 1 + 7) % 7));
        var first = thisWeek.PlusWeeks(-(WindowWeeks - 1));

        var weeks = runs
            .Where(r => r.Seconds >= AerobicAnalysis.MinimumSeconds && r.AverageHr > 0 && r.DistanceMeters > 0 && r.Date >= first && r.Date <= today)
            .GroupBy(r => r.Date.PlusDays(-(((int)r.Date.DayOfWeek - 1 + 7) % 7)))
            .Select(g =>
            {
                var paces = g.Select(r => AerobicAnalysis.NormalizedSecPerKm(r, referenceHr)).OrderBy(x => x).ToArray();
                var n = paces.Length;
                var median = n % 2 == 1 ? paces[n / 2] : (paces[n / 2 - 1] + paces[n / 2]) / 2;
                return new WeekPace(g.Key, median, n);
            })
            .OrderBy(w => w.WeekStart)
            .ToArray();

        if (anchor is null) return new AerobicOutlookResult(false, null, NeedsTest, weeks, []);

        var since = Math.Max(0, Period.Between(anchor.Date, today, PeriodUnits.Days).Days) / 7.0;
        if (since > StudyWeeks)
        {
            return new AerobicOutlookResult(false, anchor,
                string.Create(CultureInfo.InvariantCulture, $"A new drift test: the last was {since:0} weeks ago, and the rate comes from an {StudyWeeks}-week study."),
                weeks, []);
        }

        // Speed gains, so pace divides. Weeks count from today; the weeks
        // already trained since the test are not added, so the outlook
        // never claims a gain no test has shown.
        PacePoint At(int ahead)
        {
            double Pace(double gain) => anchor.SecPerKm / (1 + gain * ahead / StudyWeeks);
            return new PacePoint(ahead, Pace((SlowerGroupGain + FasterGroupGain) / 2), Pace(FasterGroupGain), Pace(SlowerGroupGain));
        }

        return new AerobicOutlookResult(true, anchor, null, weeks, Horizons.Select(At).ToArray());
    }
}
