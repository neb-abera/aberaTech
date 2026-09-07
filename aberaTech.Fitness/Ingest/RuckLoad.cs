using System.Globalization;
using System.Text.RegularExpressions;
using aberaTech.Fitness.Domain;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// The load a ruck was carried at, read out of the activity's name.
/// </summary>
/// <remarks>
/// No watch records what was in the pack. What athletes do instead is name
/// the activity — "Ruck 45lb", "12 mi @ 20 kg", "#35 ruck" — so the name is
/// read for a weight in either unit and stored in kilograms, the unit
/// everything else in this library keeps. A load the owner types on the page
/// afterwards outranks whatever was parsed here.
/// </remarks>
public static partial class RuckLoad
{
    /// <summary>The heaviest load anyone names on a training ruck, as a sanity bound.</summary>
    private const double MaxKg = 100;

    /// <summary>The load named in <paramref name="activityName"/>, in kilograms, or null when none is.</summary>
    public static double? Parse(string? activityName)
    {
        if (string.IsNullOrWhiteSpace(activityName)) return null;

        var pounds = Pounds().Match(activityName);
        if (pounds.Success && double.TryParse(pounds.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lb))
        {
            return Bound(BodyMass.PoundsToKg(lb));
        }

        var kilos = Kilograms().Match(activityName);
        if (kilos.Success && double.TryParse(kilos.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var kg))
        {
            return Bound(kg);
        }

        return null;
    }

    private static double? Bound(double kg) => kg > 0 && kg <= MaxKg ? kg : null;

    // "45lb", "45 lbs", "45#", "#45", "45-pound"
    [GeneratedRegex(@"(?:#\s*(?<n>\d+(?:\.\d+)?)|(?<n>\d+(?:\.\d+)?)\s*(?:#|lbs?\b|pounds?\b|-pound\b))", RegexOptions.IgnoreCase)]
    private static partial Regex Pounds();

    // "20kg", "20 kg", "20 kilos"
    [GeneratedRegex(@"(?<n>\d+(?:\.\d+)?)\s*(?:kg\b|kilos?\b|kilograms?\b)", RegexOptions.IgnoreCase)]
    private static partial Regex Kilograms();
}
