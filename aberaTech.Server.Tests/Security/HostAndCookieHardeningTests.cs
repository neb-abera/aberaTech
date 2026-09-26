using System.Net;
using System.Security.Claims;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace aberaTech.Server.Tests.Security;

public sealed class HostAllowlistTests
{
    private static TestApp App(params string[] hosts)
    {
        var settings = new Dictionary<string, string?>();
        for (var index = 0; index < hosts.Length; index++)
        {
            settings[$"HostAllowlist:Hosts:{index}"] = hosts[index];
        }

        return new TestApp(settings);
    }

    [Theory]
    [InlineData("abera.tech", HttpStatusCode.OK)]
    [InlineData("ABERA.TECH", HttpStatusCode.OK)]
    [InlineData("abera.tech:443", HttpStatusCode.OK)]
    [InlineData("app.kindhill-1a2b3c4d.eastus.azurecontainerapps.io", HttpStatusCode.OK)]
    [InlineData("evil.example", HttpStatusCode.BadRequest)]
    [InlineData("abera.tech.evil.example", HttpStatusCode.BadRequest)]
    [InlineData("notabera.tech", HttpStatusCode.BadRequest)]
    [InlineData("azurecontainerapps.io", HttpStatusCode.BadRequest)]
    [InlineData("10.0.0.12:8080", HttpStatusCode.BadRequest)]
    public async Task Pages_answer_only_to_the_names_the_site_goes_by(string host, HttpStatusCode expected)
    {
        using var app = App("abera.tech", "*.azurecontainerapps.io");
        using var client = app.CreateClient();

        Assert.Equal(expected, await GetAsync(client, "/", host));
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Probes_answer_whatever_they_are_called(string path)
    {
        // A platform HTTP probe addresses the container by its IP, and the
        // IP is not a name anybody can list in advance.
        using var app = App("abera.tech");
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, path, "10.0.0.12:8080"));
    }

    [Fact]
    public async Task With_no_list_every_name_is_served()
    {
        // Development and compose ship no list, so nothing is filtered.
        using var app = new TestApp(new Dictionary<string, string?>(), environment: "Development");
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/", "anything.example"));
    }

    [Fact]
    public void Production_lists_the_real_names()
    {
        var shipped = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json"))
            .Build()
            .GetSection("HostAllowlist:Hosts")
            .Get<string[]>();

        Assert.NotNull(shipped);
        Assert.Contains("abera.tech", shipped);
        Assert.DoesNotContain("*", shipped);
        // No wildcard at all: *.azurecontainerapps.io is every container app
        // on Azure, and the origin has exactly one default domain.
        Assert.DoesNotContain(shipped, entry => entry.StartsWith('*'));
    }

    [Fact]
    public void Outside_development_an_empty_list_refuses_to_start()
    {
        // Blank out what appsettings.Production.json ships, the way a lost
        // setting or a bad override would.
        var blanked = Enumerable.Range(0, 8).ToDictionary(index => $"HostAllowlist:Hosts:{index}", _ => (string?)"");
        using var app = new TestApp(blanked);

        var refusal = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("HostAllowlist:Hosts is empty", refusal.Message);
    }

    [Fact]
    public async Task Blank_entries_are_not_names()
    {
        // An entry blanked by an override must not let a request with an
        // empty or odd Host through, nor count as a list.
        using var app = new TestApp(new Dictionary<string, string?>
        {
            ["HostAllowlist:Hosts:0"] = "abera.tech",
            ["HostAllowlist:Hosts:1"] = " ",
            ["HostAllowlist:Hosts:2"] = "",
            ["HostAllowlist:Hosts:3"] = ""
        });
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/", "abera.tech"));
        Assert.Equal(HttpStatusCode.BadRequest, await GetAsync(client, "/", "localhost"));
    }

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, string path, string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        return (await client.SendAsync(request)).StatusCode;
    }
}

public sealed class AdminCookieTests : IDisposable
{
    private readonly TestApp _app = new(
        ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable),
        services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));

    [Fact]
    public void The_session_cookie_is_host_locked()
    {
        // __Host- makes the browser itself refuse the cookie unless it is
        // Secure, has Path=/ and names no Domain — so no sibling subdomain
        // and no plain-HTTP response can plant or overwrite an admin session.
        var cookie = Options().Cookie;

        Assert.Equal("__Host-abera.admin", cookie.Name);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.Equal("/", cookie.Path);
        Assert.Null(cookie.Domain);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
    }

    [Fact]
    public async Task A_session_under_the_old_name_is_signed_out_and_tidied_away()
    {
        // Graceful: the old cookie is no longer a session, the page is told
        // "not signed in" as it would be for anybody, and the leftover is
        // expired rather than sent on every request for another twelve hours.
        using var client = _app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scheduling/admin/me");
        request.Headers.Add("Cookie", $"abera.admin={Session()}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"signedIn\":false", await response.Content.ReadAsStringAsync());

        var expired = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("abera.admin=;", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", expired, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_session_under_the_new_name_still_works()
    {
        using var client = _app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scheduling/admin/me");
        request.Headers.Add("Cookie", $"__Host-abera.admin={Session()}");

        var response = await client.SendAsync(request);

        Assert.Contains("\"signedIn\":true", await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.Contains("Set-Cookie") && response.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith("abera.admin=", StringComparison.Ordinal)));
    }

    private CookieAuthenticationOptions Options() =>
        _app.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

    /// <summary>The owner's ticket, without the cookie name, so a test can send it under either name.</summary>
    private string Session()
    {
        var cookie = AdminSession.CookieFor(_app.Factory.Services, "owner@example.test");
        return cookie[(cookie.IndexOf('=') + 1)..];
    }

    public void Dispose() => _app.Dispose();
}
