using System.Net;
using System.Security.Claims;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// A session's start and end are events with the address and nothing else:
/// no account, no cookie. Sign-out goes through the route. Sign-in fires on
/// the cookie scheme's own event, so it is driven here the way the scheme
/// drives it, with the context the sign-in would carry.
/// </summary>
public sealed class SessionAuditTests : IDisposable
{
    private const string Visitor = "203.0.113.7";

    private readonly CapturedLogs _logs = new();
    private readonly TestApp _app;

    public SessionAuditTests()
    {
        _app = new TestApp(
            ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable),
            services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddSingleton<ILoggerProvider>(_logs);
            });
    }

    [Fact]
    public async Task Signing_out_is_an_event_with_the_address_and_no_account()
    {
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);
        owner.DefaultRequestHeaders.Add(RemoteAddressStartupFilter.Header, Visitor);

        using var response = await owner.PostAsync("/api/scheduling/admin/sign-out", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var logged = Assert.Single(_logs.InCategory(SecurityEvents.Category), e => e.EventId.Id == SecurityEvents.OwnerSignedOut);
        Assert.Equal(LogLevel.Information, logged.Level);
        Assert.Equal(Visitor, logged.Fields["ClientIp"]);
        Assert.DoesNotContain(AdminRouteTests.Owner, logged.Everything);
        Assert.DoesNotContain("abera.admin", logged.Everything);
    }

    [Fact]
    public async Task Signing_in_is_an_event_with_the_address_and_no_account()
    {
        var services = _app.Factory.Services;
        var options = services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Connection.RemoteIpAddress = IPAddress.Parse(Visitor);
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, AdminRouteTests.Owner)], CookieAuthenticationDefaults.AuthenticationScheme));

        await options.Events.SignedIn(new CookieSignedInContext(http, scheme, principal, new AuthenticationProperties(), options));

        var logged = Assert.Single(_logs.InCategory(SecurityEvents.Category), e => e.EventId.Id == SecurityEvents.OwnerSignedIn);
        Assert.Equal(LogLevel.Information, logged.Level);
        Assert.Equal(Visitor, logged.Fields["ClientIp"]);
        Assert.DoesNotContain(AdminRouteTests.Owner, logged.Everything);
    }

    public void Dispose() => _app.Dispose();
}
