using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using aberaTech.Fitness.IntervalsIcu;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// An activity renamed on intervals.icu after its first sync takes the new
/// name on the next sync, from the sync button to the summary.
/// </summary>
/// <remarks>
/// A test is named after the run: the watch saves "Treadmill Running", and the
/// athlete renames it "HR drift test" once it is uploaded. On 2026-11-23 the
/// sync skipped every activity it had seen, so the rename never arrived and the
/// summary never read the test. A run whose recording was already read
/// (halves, best efforts) is never fetched again, so only the listing can
/// carry the new name.
/// </remarks>
public sealed class IntervalsIcuRenameTests : IAsyncLifetime
{
    private readonly Listing _listing = new();
    private readonly FitnessApp _app;

    public IntervalsIcuRenameTests()
    {
        _app = new FitnessApp(builder =>
        {
            builder.UseSetting("IntervalsIcu:ApiKey", "icu-test-key");
            builder.ConfigureTestServices(services => services.ConfigureHttpClientDefaults(http =>
                http.ConfigurePrimaryHttpMessageHandler(() => _listing)));
        });
    }

    public async ValueTask InitializeAsync() => await _app.SeedAsync(FitnessSeed.AthleteAsync);

    public ValueTask DisposeAsync()
    {
        _app.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_run_renamed_as_a_drift_test_after_its_first_sync_is_read_as_one()
    {
        using var owner = _app.Factory.CreateClient();
        await SeedSyncedAsync(testName: "Treadmill Running", ruckName: "Walk");
        Assert.DoesNotContain(await DriftTestDatesAsync(owner), date => date == "2026-06-10");

        _listing.TestName = "HR drift test";
        _listing.RuckName = "Ruck 35 lb";
        Assert.Equal(0, await SyncAsync(owner));

        var activities = (await owner.GetFromJsonAsync<JsonElement>("/api/fitness/activities"))
            .GetProperty("activities").EnumerateArray().ToArray();
        Assert.Contains(activities, a => a.GetProperty("name").GetString() == "HR drift test");
        var ruck = Assert.Single(activities, a => a.GetProperty("name").GetString() == "Ruck 35 lb");
        Assert.Equal("ruck", ruck.GetProperty("sport").GetString());
        Assert.Equal(15.9, ruck.GetProperty("loadKg").GetDouble(), 1);
        Assert.Contains(await DriftTestDatesAsync(owner), date => date == "2026-06-10");

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/ingest");
        Assert.Equal("2 seen, 0 new, 2 renamed", status.GetProperty("intervalsIcu").GetProperty("lastOutcome").GetString());
    }

    [Fact]
    public async Task A_sync_with_no_rename_changes_nothing()
    {
        using var owner = _app.Factory.CreateClient();
        await SeedSyncedAsync(testName: "HR drift test", ruckName: "Walk");

        _listing.TestName = "HR drift test";
        _listing.RuckName = "Walk";
        Assert.Equal(0, await SyncAsync(owner));

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/ingest");
        Assert.Equal("2 seen, 0 new", status.GetProperty("intervalsIcu").GetProperty("lastOutcome").GetString());
    }

    /// <summary>The two activities as a sync that read their files left them.</summary>
    private Task SeedSyncedAsync(string testName, string ruckName) => _app.SeedAsync(database =>
    {
        database.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            Source = IntervalsIcuMapping.Source,
            ExternalId = IntervalsIcuMapping.ExternalId("i100"),
            StartedAt = Instant.FromUtc(2026, 6, 10, 13, 20),
            Sport = "run",
            Name = testName,
            DistanceMeters = 7240,
            DurationSeconds = 3600,
            AverageHr = 158,
            MaxHr = 164,
            Indoor = true,
            FirstHalfHr = 157,
            SecondHalfHr = 160.5,
            FirstHalfMetersPerSecond = 2.01,
            SecondHalfMetersPerSecond = 2.01,
            RecordsVersion = FitImport.CurrentRecordsVersion
        });
        database.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            Source = IntervalsIcuMapping.Source,
            ExternalId = IntervalsIcuMapping.ExternalId("i101"),
            StartedAt = Instant.FromUtc(2026, 6, 12, 12, 0),
            Sport = "ruck",
            Name = ruckName,
            DistanceMeters = 6000,
            DurationSeconds = 4000,
            AverageHr = 120,
            MaxHr = 135,
            Indoor = false,
            FirstHalfHr = 119,
            SecondHalfHr = 121,
            RecordsVersion = FitImport.CurrentRecordsVersion
        });
        return Task.CompletedTask;
    });

    private static async Task<int> SyncAsync(HttpClient owner)
    {
        using var response = await owner.PostAsync("/api/fitness/ingest/intervals-icu/sync", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("added").GetInt32();
    }

    private static async Task<string?[]> DriftTestDatesAsync(HttpClient owner) =>
        (await owner.GetFromJsonAsync<JsonElement>("/api/fitness/summary"))
            .GetProperty("fieldTests").EnumerateArray()
            .Where(t => t.GetProperty("kind").GetString() == "aet")
            .Select(t => t.GetProperty("date").GetString())
            .ToArray();

    /// <summary>intervals.icu with two activities whose names the test changes between syncs, and no files.</summary>
    private sealed class Listing : HttpMessageHandler
    {
        public string TestName { get; set; } = "";
        public string RuckName { get; set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/activities", StringComparison.Ordinal) != true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var body = JsonSerializer.Serialize(new object[]
            {
                new
                {
                    id = "i100", name = TestName, type = "Run",
                    start_date = "2026-06-10T13:20:00Z", start_date_local = "2026-06-10T09:20:00",
                    distance = 7240.0, moving_time = 3600.0, elapsed_time = 3600.0,
                    average_heartrate = 158.0, max_heartrate = 164.0, trainer = true
                },
                new
                {
                    id = "i101", name = RuckName, type = "Walk",
                    start_date = "2026-06-12T12:00:00Z", start_date_local = "2026-06-12T08:00:00",
                    distance = 6000.0, moving_time = 4000.0, elapsed_time = 4000.0,
                    average_heartrate = 120.0, max_heartrate = 135.0, trainer = false
                }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
