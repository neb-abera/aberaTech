using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Api;

/// <summary>One day's weigh-in as Apple Health holds it: the last sample of that day on the phone.</summary>
public sealed record HealthWeight(string Date, double WeightKg);

/// <summary>What the phone sends: the weigh-ins of its recent days.</summary>
public sealed record HealthWeightsUpload(IReadOnlyList<HealthWeight>? Weights);

/// <summary>How many days were stored or changed, and how many already matched.</summary>
public sealed record HealthWeightsResult(int Saved, int Unchanged);

/// <summary>
/// Weigh-ins that reach abera.tech from Apple Health, through the paired
/// abera-alarms phone.
/// </summary>
/// <remarks>
/// Neb logs his weight in Lose It. Lose It writes it to Apple Health and
/// sends nothing to Garmin, so it never reached intervals.icu, and on
/// 2026-11-21 the newest weigh-in here was 83 days old. Apple Health has no
/// server API: only an app on the phone can read it, so the phone sends
/// what it reads.
///
/// One weigh-in a day, as the Log tab keeps them. A day already held takes
/// the phone's weight and keeps any body fat entered by hand. The phone
/// sends its last 30 days on every sync, so a repeat is the normal case and
/// costs nothing.
/// </remarks>
public static class HealthWeights
{
    /// <summary>More than a year of days in one request is not a sync.</summary>
    public const int MaxWeights = 400;

    /// <summary>The Log tab's own bounds for a weigh-in, in kilograms.</summary>
    public const double MinKg = 30;
    public const double MaxKg = 250;

    /// <summary>Checks the upload; null when it is good, the reason when it is not.</summary>
    public static string? Problem(HealthWeightsUpload upload, LocalDate today)
    {
        var weights = upload.Weights;
        if (weights is null || weights.Count == 0) return "No weigh-ins sent.";
        if (weights.Count > MaxWeights) return $"At most {MaxWeights} weigh-ins a request.";
        foreach (var weight in weights)
        {
            if (Parse(weight.Date) is not { } date) return $"Not a date: {weight.Date}.";
            // A day ahead allows for a phone in a zone east of the server's clock.
            if (date > today.PlusDays(1)) return $"A weigh-in from the future: {weight.Date}.";
            if (weight.WeightKg is <= MinKg or > MaxKg || double.IsNaN(weight.WeightKg)) return $"Implausible weigh-in on {weight.Date}.";
        }

        return weights.Select(w => Parse(w.Date)).Distinct().Count() == weights.Count ? null : "One weigh-in a day.";
    }

    /// <summary>Stores the upload, which <see cref="Problem"/> passed.</summary>
    public static async Task<HealthWeightsResult> ApplyAsync(
        FitnessDbContext database, HealthWeightsUpload upload, CancellationToken cancellationToken)
    {
        var byDate = upload.Weights!.ToDictionary(w => Parse(w.Date)!.Value, w => w.WeightKg);
        var dates = byDate.Keys.ToList();
        var held = await database.BodyMetrics.Where(m => dates.Contains(m.Date)).ToDictionaryAsync(m => m.Date, cancellationToken);

        var saved = 0;
        foreach (var (date, kg) in byDate)
        {
            if (held.TryGetValue(date, out var existing))
            {
                if (Math.Abs(existing.WeightKg - kg) < 0.005) continue;
                existing.WeightKg = kg;
            }
            else
            {
                database.BodyMetrics.Add(new BodyMetric { Id = Guid.NewGuid(), Date = date, WeightKg = kg });
            }

            saved++;
        }

        if (saved > 0) await database.SaveChangesAsync(cancellationToken);
        return new HealthWeightsResult(saved, byDate.Count - saved);
    }

    private static LocalDate? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = LocalDatePattern.Iso.Parse(value.Trim());
        return parsed.Success ? parsed.Value : null;
    }
}
