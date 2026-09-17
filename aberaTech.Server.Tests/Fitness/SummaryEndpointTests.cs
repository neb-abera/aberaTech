using System.Net;
using System.Text.Json;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// GET /api/fitness/summary, from the outside: what it answers for a known
/// athlete, and how many times it goes to the database to find out.
/// </summary>
public sealed class SummaryEndpointTests : IDisposable
{
    private readonly FitnessApp _app = new();

    // The digest and the outlook are written from the same log as the summary,
    // so they were restructured with it and are pinned with it.
    [Theory]
    [InlineData("/api/fitness/summary", "summary.json")]
    [InlineData("/api/fitness/digest", "digest.json")]
    [InlineData("/api/fitness/readiness/outlook", "outlook.json")]
    [InlineData("/api/fitness/readiness/outlook?weeklyHours=9&compliance=0.8", "outlook-named-week.json")]
    public async Task The_payload_for_a_known_athlete_is_the_recorded_one(string path, string golden)
    {
        await _app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = _app.Factory.CreateClient();

        var body = await client.GetStringAsync(path);

        // ABERA_WRITE_GOLDEN=<directory> records instead of comparing, for the
        // deliberate change. The recordings were made before the queries were
        // restructured, which is what makes them a statement about them.
        if (Environment.GetEnvironmentVariable("ABERA_WRITE_GOLDEN") is { Length: > 0 } directory)
        {
            using var parsed = JsonDocument.Parse(body);
            File.WriteAllText(
                Path.Combine(directory, golden),
                JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            return;
        }

        using var expected = JsonDocument.Parse(Golden(golden));
        using var actual = JsonDocument.Parse(body);

        Assert.Empty(JsonDifference.Between(expected.RootElement, actual.RootElement));
    }

    [Fact]
    public async Task The_recorded_payload_exercises_every_section()
    {
        // A recording of empty arrays would pass the comparison above and
        // prove nothing about the queries behind them.
        using var golden = JsonDocument.Parse(Golden("summary.json"));
        var root = golden.RootElement;

        foreach (var section in new[] { "aerobicTrend", "weeklyVolume", "strengthTrend", "highlights", "fieldTests" })
        {
            Assert.True(root.GetProperty(section).GetArrayLength() > 0, section);
        }

        Assert.Equal(156, root.GetProperty("activityCount").GetInt32());
        Assert.True(root.GetProperty("durability").GetProperty("days").GetArrayLength() > 0);

        var readiness = root.GetProperty("readiness");
        Assert.True(readiness.GetProperty("gates").GetArrayLength() > 0);
        Assert.True(readiness.GetProperty("ruck").GetProperty("marches").GetArrayLength() > 0);
        Assert.True(readiness.GetProperty("calisthenics").GetProperty("latest").GetArrayLength() > 0);
        Assert.True(readiness.GetProperty("body").GetProperty("points").GetArrayLength() > 0);
        Assert.Equal(2, readiness.GetProperty("aftResults").GetArrayLength());
    }

    [Fact]
    public void The_comparison_notices_a_changed_number()
    {
        using var expected = JsonDocument.Parse("""{"a":[1.0,{"b":2.5}],"c":"x"}""");
        using var drifted = JsonDocument.Parse("""{"a":[1.0,{"b":2.5001}],"c":"x"}""");
        using var rounding = JsonDocument.Parse("""{"a":[1.0,{"b":2.5000000000001}],"c":"x"}""");
        using var missing = JsonDocument.Parse("""{"a":[1.0,{}],"c":"x"}""");

        Assert.Equal(["$.a[1].b: 2.5 became 2.5001"], JsonDifference.Between(expected.RootElement, drifted.RootElement));
        Assert.Empty(JsonDifference.Between(expected.RootElement, rounding.RootElement));
        Assert.Single(JsonDifference.Between(expected.RootElement, missing.RootElement));
    }

    [Fact]
    public async Task One_summary_costs_a_fixed_handful_of_commands_however_long_the_log()
    {
        await _app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = _app.Factory.CreateClient();

        _app.Commands.Reset();
        var response = await client.GetAsync("/api/fitness/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Settings, the weigh-ins, the activities with their laps, the
        // strength sets, the fitness tests and the ledger: one each. It was
        // eighteen, most of them re-reading the same activities.
        Assert.Equal(6, _app.Commands.Count);
    }

    [Theory]
    [InlineData("/api/fitness/digest", 6)]
    [InlineData("/api/fitness/readiness/outlook", 5)]
    public async Task The_pages_written_from_the_same_log_read_it_once_too(string path, int commands)
    {
        await _app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = _app.Factory.CreateClient();

        _app.Commands.Reset();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(commands, _app.Commands.Count);
    }

    private static string Golden(string name)
    {
        var resource = typeof(SummaryEndpointTests).Assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith($"Golden.{name}", StringComparison.Ordinal));
        using var stream = typeof(SummaryEndpointTests).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose() => _app.Dispose();
}

/// <summary>
/// Two JSON documents, compared value by value. Numbers are equal within a
/// relative 1e-9: the payload is floating-point arithmetic, and the last bit
/// of an exponential is allowed to differ between an ARM laptop and an x64
/// runner without that being a change in behaviour.
/// </summary>
internal static class JsonDifference
{
    public static IReadOnlyList<string> Between(JsonElement expected, JsonElement actual)
    {
        var differences = new List<string>();
        Compare("$", expected, actual, differences);
        return differences;
    }

    private static void Compare(string path, JsonElement expected, JsonElement actual, List<string> differences)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            differences.Add($"{path}: {expected.ValueKind} became {actual.ValueKind}");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var names = expected.EnumerateObject().Select(p => p.Name)
                    .Union(actual.EnumerateObject().Select(p => p.Name));
                foreach (var name in names)
                {
                    var had = expected.TryGetProperty(name, out var before);
                    var has = actual.TryGetProperty(name, out var after);
                    if (had && has) Compare($"{path}.{name}", before, after, differences);
                    else differences.Add($"{path}.{name}: {(had ? "removed" : "added")}");
                }

                break;

            case JsonValueKind.Array:
                if (expected.GetArrayLength() != actual.GetArrayLength())
                {
                    differences.Add($"{path}: {expected.GetArrayLength()} items became {actual.GetArrayLength()}");
                    return;
                }

                for (var i = 0; i < expected.GetArrayLength(); i++)
                {
                    Compare($"{path}[{i}]", expected[i], actual[i], differences);
                }

                break;

            case JsonValueKind.Number:
                var (a, b) = (expected.GetDouble(), actual.GetDouble());
                if (Math.Abs(a - b) > 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b))))
                {
                    differences.Add($"{path}: {expected.GetRawText()} became {actual.GetRawText()}");
                }

                break;

            case JsonValueKind.String:
                if (expected.GetString() != actual.GetString())
                {
                    differences.Add($"{path}: \"{expected.GetString()}\" became \"{actual.GetString()}\"");
                }

                break;
        }
    }
}
