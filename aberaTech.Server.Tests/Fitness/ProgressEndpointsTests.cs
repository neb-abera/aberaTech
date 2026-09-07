using aberaTech.Fitness.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The owner's documents, from the outside: only the named keys exist, only a
/// JSON object is a document, and a deployment with no owner has no route.
/// </summary>
public sealed class ProgressEndpointsTests : IDisposable
{
    private readonly string _webRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public ProgressEndpointsTests()
    {
        _webRoot = Directory.CreateTempSubdirectory("wwwroot-progress").FullName;
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html>home</html>");
        File.WriteAllText(Path.Combine(_webRoot, "spa.html"), "<html>shell</html>");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseWebRoot(_webRoot);
                builder.UseSetting("ConnectionStrings:Scheduling", "");
                builder.UseSetting("ConnectionStrings:Fitness", "");
            });
    }

    [Theory]
    [InlineData("rf-training", true)]
    [InlineData("planner", true)]
    [InlineData("fitness", false)]
    [InlineData("RF-TRAINING", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_named_documents_exist(string? key, bool known)
    {
        Assert.Equal(known, ProgressEndpoints.IsKnownKey(key));
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"version\":1,\"done\":[\"a\"]}", true)]
    [InlineData("[]", false)]
    [InlineData("\"text\"", false)]
    [InlineData("42", false)]
    [InlineData("{not json", false)]
    [InlineData("", false)]
    public void A_document_is_a_json_object(string body, bool accepted)
    {
        Assert.Equal(accepted, ProgressEndpoints.IsJsonObject(body));
    }

    [Fact]
    public void The_cap_is_a_quarter_megabyte()
    {
        // Big enough for a year of drills and a course plan, small enough that
        // the one account that can write cannot fill the database by accident.
        Assert.Equal(256 * 1024, ProgressEndpoints.MaxBytes);
    }

    [Fact]
    public async Task Unconfigured_deployment_has_no_document_routes()
    {
        using var client = _factory.CreateClient();

        // No owner, no route: the request falls through to the SPA fallback
        // and comes back as HTML, never as a document and never as a 500.
        var response = await client.GetAsync("/api/progress/rf-training");
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        var put = await client.PutAsync("/api/progress/rf-training", new StringContent("{}"));
        Assert.NotEqual(System.Net.HttpStatusCode.NoContent, put.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }
}
