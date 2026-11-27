using System.Security.Cryptography;
using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The rings of routine alarms against the golden vectors the iPhone app
/// tests too. Golden/routine-ring-vectors.json is the same file, byte for
/// byte, as Packages/AlarmCore/Tests/AlarmCoreTests/Resources/routine-ring-vectors.json
/// in abera-alarms and as the page's copy beside routineRingVectors.test.ts.
/// Each vector was worked out from the written contract, by neither side's code.
/// </summary>
public sealed class RoutineRingVectorsTests
{
    /// <summary>
    /// The SHA-256 of the file. The app's test and the page's test hold the
    /// same value. Change the file in all three places and this with it.
    /// </summary>
    private const string Sha256 = "2e8564b13518f7d131cc051fdc0e097fc180a08a398985e08c17fe3dd8d48c88";

    private static readonly Guid Id = Guid.Parse("0D8C6F1E-3F6E-4A53-9D53-8F1B2A7C4D10");
    private static readonly Instant LongAgo = Instant.FromUtc(1980, 1, 1, 0, 0);
    private static readonly Duration ThreeHours = Duration.FromHours(3);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record VectorFile(List<Vector> Vectors);

    private sealed record Vector(
        string Name, string Zone, VectorRoutine Routine, string From, string Until, string Summary, List<VectorRing> Rings);

    private sealed record VectorRoutine(
        int Hour, int Minute, int[] Days, string Repeat, int Every, string Unit, string? StartsOn, string? UpdatedAt);

    private sealed record VectorRing(string Stamp, string At);

    private static byte[] Bytes()
    {
        var assembly = typeof(RoutineRingVectorsTests).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith("Golden.routine-ring-vectors.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static List<Vector> Vectors() => JsonSerializer.Deserialize<VectorFile>(Bytes(), Web)!.Vectors;

    public static TheoryData<string> Names() => [.. Vectors().Select(vector => vector.Name)];

    private static Instant At(string text) => InstantPattern.General.Parse(text).Value;

    [Fact]
    public void The_file_is_the_one_the_app_and_the_page_test()
    {
        Assert.Equal(Sha256, Convert.ToHexStringLower(SHA256.HashData(Bytes())));
        Assert.True(Vectors().Count >= 50);
        Assert.Equal(Vectors().Count, Vectors().Select(vector => vector.Name).Distinct().Count());
    }

    /// <summary>
    /// <see cref="RoutineRings.Plan"/> keeps a ring from <c>from</c> to
    /// <c>until</c>, both ends included, and after the save. The vectors
    /// list the same rings.
    /// </summary>
    [Theory]
    [MemberData(nameof(Names))]
    public void The_plan_lists_exactly_the_rings_of_the_vector(string name)
    {
        var vector = Vectors().Single(candidate => candidate.Name == name);
        var given = vector.Routine;
        var routine = new AlertRoutine(
            Id, "Alarm", given.Hour, given.Minute, given.Days, true, 9, given.UpdatedAt is { } saved ? At(saved) : LongAgo)
        {
            Repeat = given.Repeat,
            Every = given.Every,
            Unit = given.Unit,
            StartsOn = RoutineRepeats.Parse(given.StartsOn),
        };
        Assert.Equal(given.StartsOn is null, routine.StartsOn is null);

        var rings = RoutineRings.Plan(
            [routine], DateTimeZoneProviders.Tzdb[vector.Zone], At(vector.From), At(vector.Until), ThreeHours);

        var prefix = RoutineRings.EventId(Id) + ":";
        Assert.All(rings, ring => Assert.StartsWith(prefix, ring.Key, StringComparison.Ordinal));
        Assert.Equal(
            vector.Rings.Select(ring => $"{ring.Stamp} {ring.At}"),
            rings.Select(ring => $"{ring.Key[prefix.Length..]} {InstantPattern.General.Format(ring.AlertAt)}"));
    }
}
