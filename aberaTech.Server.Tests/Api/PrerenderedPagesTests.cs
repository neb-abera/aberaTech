using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// How a route URL finds its prerendered HTML.
/// </summary>
/// <remarks>
/// The client build writes /transition/index.html and friends into wwwroot,
/// but a visitor asks for /transition. Static file middleware has no opinion
/// about that mapping, so this decides it: an extensionless GET whose
/// prerendered file exists is rewritten to that file, and everything else is
/// left for the middleware behind it — real assets, API routes, and the SPA
/// fallback for pages that are not prerendered.
/// </remarks>
public class PrerenderedPagesTests
{
    private static bool Exists(string path) =>
        path is "/transition/index.html" or "/guides/index.html";

    [Theory]
    [InlineData("/transition", "/transition/index.html")]
    [InlineData("/transition/", "/transition/index.html")]
    [InlineData("/guides", "/guides/index.html")]
    public void A_prerendered_route_is_rewritten_to_its_file(string requested, string expected)
    {
        Assert.Equal(expected, PrerenderedPages.RewriteFor(requested, Exists));
    }

    [Fact]
    public void A_route_without_a_prerendered_file_is_left_alone()
    {
        Assert.Null(PrerenderedPages.RewriteFor("/schedule", Exists));
    }

    [Fact]
    public void The_root_is_left_to_the_default_files_middleware()
    {
        Assert.Null(PrerenderedPages.RewriteFor("/", Exists));
    }

    [Fact]
    public void A_file_request_is_never_rewritten()
    {
        // Anything with an extension is an asset, whatever directories exist.
        Assert.Null(PrerenderedPages.RewriteFor("/assets/index-abc.js", Exists));
    }

    [Fact]
    public void An_api_route_is_never_rewritten()
    {
        Assert.Null(PrerenderedPages.RewriteFor("/api/scheduling/admin/me", Exists));
    }
}
