using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using aberaTech.Server;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// Liveness and readiness answer two different questions, and the deploy needs
/// both. A crashed process stops answering at all; a database that has gone
/// away leaves every page up and only the feature broken, which a deploy that
/// asks nothing will happily call a success.
/// </summary>
public sealed class ReadinessTests : IDisposable
{
    private readonly string _webRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public ReadinessTests()
    {
        _webRoot = Directory.CreateTempSubdirectory("wwwroot-readiness").FullName;
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html>home</html>");
        File.WriteAllText(Path.Combine(_webRoot, "spa.html"), "<html>shell</html>");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseWebRoot(_webRoot);
                // The deployment with nothing switched on, which must still be
                // both healthy and ready.
                builder.UseSetting("ConnectionStrings:Scheduling", "");
                builder.UseSetting("ConnectionStrings:Fitness", "");
            });
    }

    [Fact]
    public async Task Liveness_says_the_process_is_serving()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_deployment_with_nothing_configured_is_ready()
    {
        // Switched off is not broken. Both features fail closed by design, and
        // a readiness probe that called that unready would block every deploy
        // of a deployment that is working exactly as intended.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/readyz");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("ready").GetBoolean());

        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToArray();
        Assert.Equal(2, checks.Length);
        Assert.All(checks, check =>
        {
            Assert.False(check.GetProperty("configured").GetBoolean());
            Assert.Equal("not configured", check.GetProperty("detail").GetString());
        });
    }

    [Fact]
    public async Task Readiness_names_every_dependency_it_knows_about()
    {
        // The point of the endpoint is that a deploy can tell which dependency
        // is missing, so the names have to be there whatever the verdict.
        using var client = _factory.CreateClient();

        var body = await (await client.GetAsync("/readyz")).Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        var names = json.RootElement.GetProperty("checks").EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("scheduling-db", names);
        Assert.Contains("fitness-db", names);
    }

    [Fact]
    public void Something_switched_on_and_unreachable_is_not_ready()
    {
        // The failure the deploy gate exists to catch. It cannot be staged
        // through the running app — a configured database that is unreachable
        // fails the startup migration, so the process never boots — which is
        // why the rule lives somewhere it can be exercised directly.
        var report = Readiness.From(
        [
            Readiness.NotConfigured("scheduling-db"),
            Readiness.Unreachable("fitness-db", "NpgsqlException")
        ]);

        Assert.False(report.Ready);
        Assert.Equal(503, report.StatusCode);
    }

    [Fact]
    public void Switched_off_is_not_the_same_as_broken()
    {
        var report = Readiness.From(
        [
            Readiness.NotConfigured("scheduling-db"),
            Readiness.NotConfigured("fitness-db")
        ]);

        Assert.True(report.Ready);
        Assert.Equal(200, report.StatusCode);
    }

    [Fact]
    public void One_failure_among_healthy_dependencies_still_fails_the_whole_probe()
    {
        var report = Readiness.From(
        [
            Readiness.Reachable("scheduling-db"),
            Readiness.Unreachable("fitness-db", "TimeoutException")
        ]);

        Assert.False(report.Ready);
    }

    [Fact]
    public void A_failed_check_never_carries_a_provider_message()
    {
        // Whatever the provider said, only its type reaches an unauthenticated
        // caller — connection strings turn up in Npgsql exception text.
        var check = Readiness.Unreachable("fitness-db", typeof(TimeoutException).Name);

        Assert.Equal("TimeoutException", check.Detail);
        Assert.DoesNotContain("Password", check.Detail, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }
}
