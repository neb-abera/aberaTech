using System.Net;
using System.Text;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using aberaTech.Fitness.IntervalsIcu;
using aberaTech.Fitness.Sync;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>Every way a pull can fail reaches the page, and neither source stops the other.</summary>
public sealed class SyncFailureTests : IDisposable
{
    private readonly Answer _hevy = new();
    private readonly Answer _icu = new();
    private readonly FitnessApp _app;

    public SyncFailureTests()
    {
        _app = new FitnessApp(builder =>
        {
            builder.UseSetting("Fitness:HevyApiKey", "hevy-key");
            builder.UseSetting("IntervalsIcu:ApiKey", "icu-key");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<HevyApiClient>().ConfigurePrimaryHttpMessageHandler(() => _hevy);
                services.AddHttpClient<IntervalsIcuClient>().ConfigurePrimaryHttpMessageHandler(() => _icu);
            });
        });
    }

    public void Dispose() => _app.Dispose();

    public static TheoryData<string, string> Failures => new()
    {
        { "timeout", "Hevy timed out; will try again." },
        { "refused", "Hevy refused the API key; replace it." },
        { "outage", "Hevy did not answer; will try again." },
        { "garbage", "Hevy sent something unreadable; will try again." }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_failed_pull_says_why_and_the_other_source_still_runs(string failure, string expected)
    {
        _hevy.Respond = failure switch
        {
            "timeout" => () => throw new TaskCanceledException("HttpClient.Timeout"),
            "refused" => () => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            "outage" => () => new HttpResponseMessage(HttpStatusCode.BadGateway),
            _ => () => Json("<html>")
        };
        _icu.Respond = () => Json("[]");

        await TickAsync();

        var hevy = await StateAsync(SyncSchedule.HevySource);
        Assert.Equal(expected, hevy.LastOutcome);
        Assert.Equal(FitnessApp.Now, hevy.LastRunAt);
        Assert.Null(hevy.LastSuccessAt);

        var icu = await StateAsync(SyncSchedule.IntervalsIcuSource);
        Assert.Equal("0 seen, 0 new", icu.LastOutcome);
        Assert.Equal(FitnessApp.Now, icu.LastSuccessAt);
    }

    [Fact]
    public async Task A_timed_out_listing_is_recorded_and_keeps_the_last_success()
    {
        var earlier = FitnessApp.Now - Duration.FromHours(2);
        await _app.SeedAsync(database =>
        {
            database.SyncStates.Add(new SyncState
            {
                Source = SyncSchedule.IntervalsIcuSource,
                LastRunAt = earlier,
                LastSuccessAt = earlier,
                LastOutcome = "3 seen, 0 new"
            });
            return database.SaveChangesAsync();
        });
        _hevy.Respond = () => Json("{\"page\":1,\"page_count\":1,\"workouts\":[]}");
        _icu.Respond = () => throw new TaskCanceledException("HttpClient.Timeout");

        await TickAsync();

        var icu = await StateAsync(SyncSchedule.IntervalsIcuSource);
        Assert.Equal("intervals.icu timed out; will try again.", icu.LastOutcome);
        Assert.Equal(earlier, icu.LastSuccessAt);
        Assert.Equal(FitnessApp.Now, icu.LastRunAt);
    }

    private async Task TickAsync()
    {
        var services = _app.Factory.Services;
        var worker = new FitnessSyncWorker(
            services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<IClock>(), NullLogger<FitnessSyncWorker>.Instance);
        await worker.TickAsync(CancellationToken.None);
    }

    private async Task<SyncState> StateAsync(string source)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        return await database.SyncStates.AsNoTracking().SingleAsync(s => s.Source == source);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Answer : HttpMessageHandler
    {
        public Func<HttpResponseMessage> Respond { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Respond());
    }
}
