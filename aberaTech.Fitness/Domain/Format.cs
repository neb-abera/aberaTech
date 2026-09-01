using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>Number-to-words helpers the calculation traces share.</summary>
internal static class Format
{
    /// <summary>Seconds as m:ss, or h:mm:ss once an hour is involved.</summary>
    public static string Clock(double totalSeconds)
    {
        var seconds = (int)Math.Round(totalSeconds);
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var rest = seconds % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{rest:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{rest:00}");
    }

    /// <summary>
    /// A fraction as a percentage. Written out rather than using the "P"
    /// format, which under the invariant culture renders "3 %".
    /// </summary>
    public static string Percent(double fraction, int decimals = 0) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Round(fraction * 100, decimals).ToString($"0.{new string('0', decimals)}".TrimEnd('.'), CultureInfo.InvariantCulture)}%");

    /// <summary>Metres as the unit an athlete would say it in.</summary>
    public static string Distance(double meters)
    {
        var miles = meters / Vdot.MileMeters;
        if (Math.Abs(miles - Math.Round(miles, 2)) < 0.005 && miles >= 0.5)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(miles, 2):0.##}-mile");
        }

        return meters >= 1000
            ? string.Create(CultureInfo.InvariantCulture, $"{meters / 1000:0.##} km")
            : string.Create(CultureInfo.InvariantCulture, $"{meters:0} m");
    }
}
