using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One logged carry: a load walked a distance.</summary>
/// <param name="Date">The day it was done.</param>
/// <param name="Exercise">The name the logging app gave it.</param>
/// <param name="WeightKg">The weight as logged — per hand, the way lifting apps record a pair of implements.</param>
/// <param name="DistanceMeters">How far it was carried, when the app recorded it.</param>
public sealed record LoggedCarry(LocalDate Date, string Exercise, double WeightKg, double? DistanceMeters);

/// <summary>The longest carry at or above a load, on one day.</summary>
public sealed record BestCarry(LocalDate Date, double DistanceMeters, double TotalLoadKg);

/// <summary>
/// Grip, as selection actually tests it: holding a heavy thing and walking.
/// </summary>
/// <remarks>
/// No selection publishes a grip standard, and the one tactical study that
/// went looking (Mountain Tactical Institute) found the farmer's carry the
/// most practical grip-endurance test and no benchmark to hold it to. The
/// standard here is the athlete's own — a farmer's carry at one and a half
/// times bodyweight for a hundred metres — read from the strength log.
///
/// One convention, stated because it decides the number: lifting apps log a
/// pair of implements as the weight of one of them, so the total carried is
/// twice what was written down. A set logged at 60 kg is a 120 kg carry.
///
/// Citation: <see cref="Citations.MtiGrip"/>.
/// </remarks>
public static class Carries
{
    /// <summary>The load the athlete's own standard is set at, as a multiple of bodyweight.</summary>
    public const double StandardBodyweightMultiple = 1.5;

    /// <summary>Whether a logged movement is a farmer's carry.</summary>
    public static bool IsFarmersCarry(string exercise)
    {
        var name = exercise.Trim().ToLowerInvariant();
        return name.Contains("farmer");
    }

    /// <summary>
    /// The longest carry at or above <paramref name="bodyweightMultiple"/>
    /// times <paramref name="bodyKg"/>, on or after <paramref name="since"/>.
    /// </summary>
    public static BestCarry? Longest(
        IEnumerable<LoggedCarry> carries, double bodyKg, double bodyweightMultiple, LocalDate since)
    {
        if (bodyKg <= 0) throw new ArgumentOutOfRangeException(nameof(bodyKg));

        var minimum = bodyKg * bodyweightMultiple;

        return carries
            .Where(c => c.Date >= since && IsFarmersCarry(c.Exercise)
                        && c.DistanceMeters is > 0 && c.WeightKg * 2 >= minimum - 1e-9)
            .Select(c => new BestCarry(c.Date, c.DistanceMeters!.Value, c.WeightKg * 2))
            .OrderByDescending(c => c.DistanceMeters)
            .ThenByDescending(c => c.Date)
            .FirstOrDefault();
    }
}
