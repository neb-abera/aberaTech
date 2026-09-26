using System.Net;
using System.Security.Claims;
using aberaTech.Scheduling.Admin;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// Sign-out ends the session on the server, not only in the browser that
/// pressed the button. A copy of the cookie taken before sign-out is refused
/// after it.
/// </summary>
/// <remarks>
/// Driven from the entry point: the Development sign-in issues the cookie
/// through the real cookie scheme, the same way the Google sign-in does, and
/// the version lives in the compose Postgres, as it does in production.
/// </remarks>
public sealed class SessionRevocationTests : IDisposable
{
    private const string Queue = "/api/scheduling/admin/queue";
    private const string SignIn = "/api/scheduling/admin/sign-in?returnUrl=/schedule/admin";
    private const string SignOut = "/api/scheduling/admin/sign-out";

    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public SessionRevocationTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("revoke");
        _app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = _database.ConnectionString,
                ["ClientAddress:ForwardedHops"] = "0",
                ["Admin:GoogleClientId"] = "test-client",
                ["Admin:GoogleClientSecret"] = "test-secret",
                ["Admin:AllowedEmails:0"] = AdminRouteTests.Owner,
                ["Admin:DevelopmentSignIn"] = "true"
            },
            services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()),
            environment: "Development");
    }

    [PostgresFact]
    public async Task A_cookie_copied_before_sign_out_is_refused_after_it()
    {
        var cookie = await SignInAsync();
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(HttpMethod.Get, Queue, cookie));

        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(HttpMethod.Post, SignOut, cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(HttpMethod.Get, Queue, cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(HttpMethod.Post, SignOut, cookie));
    }

    [PostgresFact]
    public async Task Signing_in_again_works_and_does_not_bring_the_old_cookie_back()
    {
        var before = await SignInAsync();
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(HttpMethod.Post, SignOut, before));

        var after = await SignInAsync();

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(HttpMethod.Get, Queue, after));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(HttpMethod.Get, Queue, before));
    }

    [PostgresFact]
    public async Task Sign_out_on_one_device_ends_the_session_on_the_other()
    {
        var laptop = await SignInAsync();
        var phone = await SignInAsync();

        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(HttpMethod.Post, SignOut, laptop));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(HttpMethod.Get, Queue, phone));
    }

    [PostgresFact]
    public async Task A_cookie_with_no_session_version_is_refused()
    {
        // What every cookie issued before this check looked like: the right
        // account, a valid signature, and no version.
        var options = _app!.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, AdminRouteTests.Owner)],
                CookieAuthenticationDefaults.AuthenticationScheme)),
            CookieAuthenticationDefaults.AuthenticationScheme);
        var cookie = $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(HttpMethod.Get, Queue, cookie));
    }

    [PostgresFact]
    public async Task A_refused_cookie_is_deleted_from_the_browser()
    {
        var cookie = await SignInAsync();
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(HttpMethod.Post, SignOut, cookie));

        using var client = _app!.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await client.GetAsync(Queue);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var name = cookie[..cookie.IndexOf('=')];
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith($"{name}=;", StringComparison.Ordinal) && value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Presses the sign-in button and keeps the cookie it sets, as an attacker copying it would.</summary>
    private async Task<string> SignInAsync()
    {
        using var client = _app!.CreateClient();
        using var response = await client.GetAsync(SignIn);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var setCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(AdminAuth.DevelopmentCookieName + "=", StringComparison.Ordinal));
        return setCookie[..setCookie.IndexOf(';')];
    }

    private async Task<HttpStatusCode> StatusAsync(HttpMethod method, string path, string cookie)
    {
        using var client = _app!.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}

/// <summary>When the version cannot be read, the request is anonymous and the cookie is kept.</summary>
public sealed class SessionVersionOutageTests : IDisposable
{
    private readonly TestApp _app = new(
        ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable),
        services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<IAdminSessionVersions, Unreachable>();
        });

    [Fact]
    public async Task An_unreadable_version_refuses_the_request_and_keeps_the_cookie()
    {
        var options = _app.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, AdminRouteTests.Owner), new Claim(AdminSessions.VersionClaim, "0")],
                CookieAuthenticationDefaults.AuthenticationScheme)),
            CookieAuthenticationDefaults.AuthenticationScheme);

        using var client = _app.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        using var response = await client.GetAsync("/api/scheduling/admin/queue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private sealed class Unreachable : IAdminSessionVersions
    {
        public Task<int> CurrentAsync(string email, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("database unreachable");

        public Task RevokeAsync(string email, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("database unreachable");
    }

    public void Dispose() => _app.Dispose();
}
