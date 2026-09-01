using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// The middleware pipeline, exercised end to end against a fixture wwwroot.
/// </summary>
/// <remarks>
/// This exists because of a real failure the unit tests could not see. With
/// WebApplication's implicit routing at the front of the pipeline, the SPA
/// fallback endpoint matched every extensionless request before the static
/// file middleware ran — which then stands down when an endpoint has already
/// matched — so the rewrite to prerendered pages and the default-files
/// behaviour were both dead code, and every page served the empty shell. The
/// pipeline order is only testable as a pipeline, so these boot the real one.
/// </remarks>
public sealed class StaticPipelineTests : IDisposable
{
    private readonly string _webRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public StaticPipelineTests()
    {
        _webRoot = Directory.CreateTempSubdirectory("wwwroot-fixture").FullName;
        Directory.CreateDirectory(Path.Combine(_webRoot, "transition"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            "<html><script>bootstrap()</script>prerendered home</html>");
        File.WriteAllText(Path.Combine(_webRoot, "spa.html"), "<html>empty shell</html>");
        File.WriteAllText(Path.Combine(_webRoot, "transition", "index.html"), "<html>prerendered guide</html>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app')");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseWebRoot(_webRoot);
                // No database: the app is designed to boot without one, and
                // these tests are about the static pipeline alone.
                builder.UseSetting("ConnectionStrings:Scheduling", "");
                builder.UseSetting("ConnectionStrings:Fitness", "");
            });
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task The_root_serves_the_prerendered_home_page()
    {
        var response = await _factory.CreateClient().GetAsync("/");

        Assert.Contains("prerendered home", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_prerendered_route_serves_its_own_page()
    {
        var response = await _factory.CreateClient().GetAsync("/transition");

        Assert.Contains("prerendered guide", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_route_that_is_not_prerendered_falls_back_to_the_empty_shell()
    {
        var response = await _factory.CreateClient().GetAsync("/schedule");

        Assert.Contains("empty shell", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task An_unknown_path_is_a_404_when_the_build_says_what_the_pages_are()
    {
        // The manifest the client build writes from site/routes.ts. With it, a
        // path the app cannot render answers 404 — to a reader and to a
        // crawler — instead of 200 and a blank shell.
        File.WriteAllText(
            Path.Combine(_webRoot, "app-routes.json"),
            """["/", "/guides", "/schedule"]""");

        using var factory = _factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
        using var client = factory.CreateClient();

        var known = await client.GetAsync("/schedule");
        var gone = await client.GetAsync("/marketing");

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        // Still the shell, so the app renders its own not-found page rather
        // than the browser's.
        Assert.Contains("empty shell", await gone.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", gone.Headers.CacheControl?.ToString());

        File.Delete(Path.Combine(_webRoot, "app-routes.json"));
    }

    [Fact]
    public async Task Healthz_answers_for_itself_rather_than_through_the_fallback()
    {
        // It never existed: /healthz was answered by the SPA fallback with 200
        // and the HTML shell, so the boot gate that waits on it passed whatever
        // the app was doing.
        var response = await _factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_deployment_without_the_manifest_serves_the_shell_as_before()
    {
        // An older build, or a host with a bare webroot: no manifest means
        // "answer as before", never "nothing is a real page".
        var response = await _factory.CreateClient().GetAsync("/anything-at-all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("empty shell", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_csp_allows_the_inline_script_the_pages_were_baked_with()
    {
        // sha256 of "bootstrap()", the inline script in the fixture's
        // index.html. The startup scan of the shipped HTML is what puts it in
        // the header; a page with a different script would be blocked.
        var response = await _factory.CreateClient().GetAsync("/");

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var scriptSrc = Assert.Single(
            csp.Split("; "),
            directive => directive.StartsWith("script-src "));
        Assert.Contains("'sha256-", scriptSrc);
    }

    [Fact]
    public async Task The_csp_allows_the_rum_beacon_cloudflare_injects()
    {
        // Real-user metrics are opted into deliberately: Cloudflare injects
        // its beacon into every HTML response, and without these two hosts
        // the policy blocks it — a console error on every page load, and no
        // data. The beacon loads from static.cloudflareinsights.com and
        // reports to cloudflareinsights.com.
        var response = await _factory.CreateClient().GetAsync("/");

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("https://static.cloudflareinsights.com", csp);
        Assert.Contains("connect-src 'self' https://cloudflareinsights.com", csp);
    }

    [Fact]
    public async Task The_csp_allows_both_hops_of_the_affiliate_image_redirect()
    {
        // The transition guide's DITY-calculator banner is a CJ Affiliate
        // image: lduhtrp.net 302-redirects to yceml.net, which serves the
        // bytes. CSP checks every hop, so dropping either host breaks the
        // image — which happened once when the policy moved from the meta
        // tag to this header.
        var response = await _factory.CreateClient().GetAsync("/");

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var imgSrc = Assert.Single(
            csp.Split("; "),
            directive => directive.StartsWith("img-src "));
        Assert.Contains("https://www.lduhtrp.net", imgSrc);
        Assert.Contains("https://www.yceml.net", imgSrc);
    }

    [Fact]
    public async Task A_head_request_for_a_prerendered_route_is_served_without_a_redirect()
    {
        // Link checkers and crawlers probe with HEAD; a 301 hop to the
        // trailing-slash form is what the default-files middleware does when
        // the rewrite skips the request.
        using var request = new HttpRequestMessage(HttpMethod.Head, "/transition");
        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_leave_the_origin_compressed()
    {
        // Cloudflare compresses edge-to-browser regardless; this pins the
        // origin-to-edge leg, which used to ship uncompressed on cache MISS.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.AcceptEncoding.ParseAdd("gzip");

        var response = await client.SendAsync(request);

        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task A_hashed_asset_is_served_immutable()
    {
        var response = await _factory.CreateClient().GetAsync("/assets/index-abc123.js");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            response.Headers.CacheControl?.ToString());
    }
}
