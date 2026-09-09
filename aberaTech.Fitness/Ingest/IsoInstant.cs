using System.Globalization;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// An ISO-8601 timestamp as an instant, whichever of the forms a service
/// writes it in: a trailing Z, a numeric offset, fractional seconds or none.
/// </summary>
/// <remarks>
/// NodaTime's <see cref="InstantPattern.ExtendedIso"/> accepts only the Z
/// form. Hevy and intervals.icu both write offsets on occasion, and a parser
/// that returns null for "+00:00" would turn a whole sync into "0 fetched"
/// with no error. So: the strict instant pattern first,
/// the offset pattern second, and the BCL's round-trip parser as the last
/// word, since it reads every ISO shape the others do not.
/// </remarks>
public static class IsoInstant
{
    public static Instant? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var instant = InstantPattern.ExtendedIso.Parse(text);
        if (instant.Success) return instant.Value;

        var offset = OffsetDateTimePattern.ExtendedIso.Parse(text);
        if (offset.Success) return offset.Value.ToInstant();

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? Instant.FromDateTimeOffset(parsed)
            : null;
    }
}
