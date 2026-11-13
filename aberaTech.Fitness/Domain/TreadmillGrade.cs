using System.Globalization;
using System.Text.RegularExpressions;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>
/// The incline a treadmill run was set to, and the flat pace that costs the
/// same. A watch cannot record a treadmill's grade, so the grade comes from
/// the athlete: a standing setting from a date on, or a percentage in the
/// run's title for a run set differently.
/// </summary>
/// <remarks>
/// The ACSM running equation prices a run at
/// VO2 = 0.2·v + 0.9·v·grade + 3.5 (mL/kg/min, v in m/min). The flat speed
/// with the same cost solves 0.2·v' = 0.2·v + 0.9·v·grade, so
/// v' = v·(1 + 4.5·grade). The aerobic trend reads pace at a fixed heart
/// rate, so a run at 1% is read as the flat run it equals: 4.5% faster.
/// The history before the setting was run at 0% on a treadmill, and that is
/// the baseline the conversion keeps. ACSM states the equation for steady
/// running faster than 134 m/min (7:28/km). A 1% grade is the training
/// setting because it matches level outdoor running between 2.92 and
/// 5.0 m/s. Citations: <see cref="Citations.AcsmRunningEquation"/>,
/// <see cref="Citations.JonesDoustTreadmillGrade"/>.
/// </remarks>
public static partial class TreadmillGrade
{
    /// <summary>The steepest grade the setting or a title may give, in percent.</summary>
    public const double MaxPercent = 15;

    /// <summary>The ACSM equation's vertical cost over its horizontal cost: 0.9 / 0.2.</summary>
    public const double CostRatio = 0.9 / 0.2;

    /// <summary>The distance a flat run of the same cost would have covered.</summary>
    public static double FlatEquivalentMeters(double meters, double gradePercent) =>
        meters * (1 + CostRatio * gradePercent / 100);

    /// <summary>
    /// A grade written in a run's title, such as "Treadmill 2%" or "Easy 0.5 %".
    /// Null when the title names none, or names one past <see cref="MaxPercent"/>.
    /// </summary>
    public static double? FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var match = PercentInTitle().Match(title);
        if (!match.Success) return null;

        var percent = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return percent <= MaxPercent ? percent : null;
    }

    /// <summary>
    /// The grade a run was set to: the title's when it names one, else the
    /// standing setting from its start date on, else 0. An outdoor run has no
    /// treadmill grade.
    /// </summary>
    public static double For(bool indoor, LocalDate date, string? title, double? settingPercent, LocalDate? since)
    {
        if (!indoor) return 0;
        if (FromTitle(title) is { } titled) return titled;
        if (settingPercent is { } percent && since is { } start && date >= start) return percent;
        return 0;
    }

    [GeneratedRegex(@"(?<![\d.])(\d{1,2}(?:\.\d+)?)\s*%", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PercentInTitle();
}
