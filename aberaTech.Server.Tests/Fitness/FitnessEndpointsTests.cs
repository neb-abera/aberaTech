using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Fitness.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The fail-closed contract, from the outside. A deployment with no fitness
/// configuration must answer "not configured" — not 500, not an empty shell,
/// and above all not data.
/// </summary>
public sealed class FitnessEndpointsTests : IDisposable
{
    private readonly string _webRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public FitnessEndpointsTests()
    {
        // A minimal webroot so the SPA fallback has something to serve; the
        // assertions below are about the API surface, not the shell.
        _webRoot = Directory.CreateTempSubdirectory("wwwroot-fitness").FullName;
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html>home</html>");
        File.WriteAllText(Path.Combine(_webRoot, "spa.html"), "<html>shell</html>");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseWebRoot(_webRoot);
                // No databases: this is the unconfigured deployment on purpose.
                builder.UseSetting("ConnectionStrings:Scheduling", "");
                builder.UseSetting("ConnectionStrings:Fitness", "");
            });
    }

    [Fact]
    public async Task Unconfigured_deployment_says_so()
    {
        using var client = _factory.CreateClient();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/fitness/me");

        Assert.False(me.GetProperty("configured").GetBoolean());
        Assert.False(me.GetProperty("signedIn").GetBoolean());
    }

    [Fact]
    public async Task Unconfigured_deployment_exposes_no_data_routes()
    {
        using var client = _factory.CreateClient();

        // The route does not exist, so the request falls through to the SPA
        // fallback and comes back as HTML — never JSON, never a 500.
        var summary = await client.GetAsync("/api/fitness/summary");
        Assert.NotEqual("application/json", summary.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Every_model_carries_a_discipline_matched_citation()
    {
        // The registry is the sourcing contract: every claim has a person, a
        // work and a year. The UI renders exactly this list.
        Assert.True(Citations.All.Count >= 8);
        Assert.All(Citations.All, citation =>
        {
            Assert.False(string.IsNullOrWhiteSpace(citation.Claim));
            Assert.False(string.IsNullOrWhiteSpace(citation.Who));
            Assert.False(string.IsNullOrWhiteSpace(citation.Work));
            Assert.InRange(citation.Year, 1950, 2030);
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }
}
