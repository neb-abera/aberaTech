using System.Net;
using aberaTech.Server.Tests.Fitness;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// The checker: an endpoint must say who may call it — AllowAnonymous, or an
/// authorization requirement. One that says nothing is reported by its name.
/// </summary>
internal static class EndpointAuthorization
{
    public static IReadOnlyList<string> Undeclared(IEnumerable<Endpoint> endpoints) =>
        [.. endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                && endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(endpoint => endpoint.DisplayName ?? "(unnamed)")];
}

/// <summary>
/// Endpoints a test needs that the application does not map. Program maps
/// the real ones and a test cannot add to them, but selecting an endpoint
/// before routing runs has the same effect — routing stands down when one is
/// already chosen — so these pass through the app's own limiter,
/// authentication and authorization exactly as a mapped endpoint would.
/// </summary>
internal sealed class ExtraEndpoints(params Endpoint[] endpoints) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, following) =>
        {
            var match = endpoints.OfType<RouteEndpoint>()
                .FirstOrDefault(endpoint => endpoint.RoutePattern.RawText == context.Request.Path);
            if (match is not null)
            {
                context.SetEndpoint(match);
            }

            return following(context);
        });
        next(app);
    };
}

/// <summary>
/// Authorisation is declared per endpoint and tested over the whole table.
/// Every route says who may call it, the check proves it can fail, and an
/// endpoint that somehow says nothing is closed at runtime as well.
/// </summary>
public sealed class RouteTableTests
{
    /// <summary>The one address on both allowlists in <see cref="ProbeSurfaceLimitsTests.Configured"/>.</summary>
    private const string Owner = "owner@example.test";

    private const string Stranger = "stranger@example.test";

    /// <summary>
    /// Production, with every optional surface switched on, so the table under
    /// test is the whole one: admin sign-in and its calendar, fitness with its
    /// digest key and both sync bridges, and Twilio's receipts.
    /// </summary>
    private static Dictionary<string, string?> Everything()
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["Fitness:HevyApiKey"] = "hevy-test-key";
        settings["IntervalsIcu:ApiKey"] = "icu-test-key";
        settings["DevBox:SubscriptionId"] = "00000000-0000-0000-0000-00000000dead";
        settings["DevBox:HeartbeatToken"] = "box-token-for-tests";
        settings["Admin:DevelopmentSignIn"] = "true";
        settings["DevBox:Fake"] = "true";
        return settings;
    }

    private static TestApp App(CapturedLogs? logs = null, params Endpoint[] extra) => new(Everything(), services =>
    {
        // The sign-in and admin cookies are protected with a key ring the app
        // keeps in the database, and this host has no database.
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        if (logs is not null) services.AddSingleton<ILoggerProvider>(logs);
        if (extra.Length > 0) services.AddSingleton<IStartupFilter>(new ExtraEndpoints(extra));
    });

    [Fact]
    public void Every_endpoint_declares_who_may_call_it()
    {
        using var app = App();
        var endpoints = app.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        // The shape under test is the whole table, not a deployment with the
        // optional surfaces switched off: one route from each.
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/scheduling/admin/queue/{entryId:guid}/start"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/scheduling/admin/calendar/connect"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/fitness/digest.txt"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/fitness/ingest/hevy/sync"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/fitness/ingest/intervals-icu/sync"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/scheduling/sms-status"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/scheduling/book/{appointmentId:guid}"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/devbox/start"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/devbox/heartbeat"));

        Assert.Empty(EndpointAuthorization.Undeclared(endpoints));
    }

    [Fact]
    public void The_development_bypass_declares_itself_too()
    {
        // `make up`: Development, no sign-in, the fitness surface open on the
        // loopback database and the API explorer mapped. Skipping sign-in is a
        // decision, and it is written on the endpoints rather than left off.
        using var app = new FitnessApp();
        var endpoints = app.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/fitness/summary"));
        Assert.Contains(endpoints, endpoint => Names(endpoint, "/api/progress/{key}"));
        Assert.Contains(endpoints, endpoint => endpoint.DisplayName?.Contains("/openapi/", StringComparison.Ordinal) == true);

        Assert.Empty(EndpointAuthorization.Undeclared(endpoints));
    }

    [Fact]
    public async Task The_check_reports_an_endpoint_that_declares_nothing()
    {
        // The checker's own negative test: an endpoint mapped the way one is
        // mapped when nobody thinks about authorization must be reported.
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/forgotten", () => "anyone?");
        app.MapGet("/public", () => "hello").AllowAnonymous();
        app.MapGet("/private", () => "secret").RequireAuthorization();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);

        Assert.Equal(new[] { "HTTP: GET /forgotten" }, EndpointAuthorization.Undeclared(endpoints));
    }

    [Fact]
    public async Task An_endpoint_that_declares_nothing_is_closed_at_runtime_too()
    {
        // The fallback policy: whatever the table test missed — an endpoint
        // mapped under a condition no test host reproduces, say — answers
        // like the admin surface does. A stranger's Google account is not
        // enough; only the host gets through.
        var logs = new CapturedLogs();
        using var app = App(logs, Endpoint("/_test/forgotten", Ok));

        using var anonymous = app.CreateClient();
        using var stranger = app.CreateClient().SignedInAs(app.Factory.Services, Stranger);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, Owner);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/_test/forgotten")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/_test/forgotten")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/_test/forgotten")).StatusCode);

        // Refused the way every other refusal is: under the ids alerts are
        // written against, naming the route pattern and never the caller.
        var security = logs.InCategory(SecurityEvents.Category);
        var signInRequired = Assert.Single(security, entry => entry.EventId.Id == SecurityEvents.SignInRequired);
        var allowlistRefused = Assert.Single(security, entry => entry.EventId.Id == SecurityEvents.AllowlistRefused);
        Assert.Equal("/_test/forgotten", signInRequired.Fields["Route"]);
        Assert.Equal("/_test/forgotten", allowlistRefused.Fields["Route"]);
        Assert.All(security, entry => Assert.DoesNotContain(Stranger, entry.Everything));
    }

    [Fact]
    public async Task An_endpoint_that_declares_itself_anonymous_stays_open()
    {
        // The other half: the fallback must not close what was opened on
        // purpose, or every public route would have gone dark with it.
        using var app = App(null, Endpoint("/_test/open", Ok, new AllowAnonymousAttribute()));
        using var anonymous = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/_test/open")).StatusCode);
    }

    [Theory]
    [InlineData("/sitemap-nobody-shipped.xml")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/.well-known/nothing.json")]
    public async Task A_file_that_is_not_there_is_a_404_and_not_a_sign_in(string path)
    {
        // The fallback policy closes endpoints that say nothing. A path that
        // matches no endpoint at all must not be closed by it: it is a 404,
        // to a stranger and to a crawler alike.
        using var app = App();
        using var anonymous = app.CreateClient();

        var response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Security_txt_is_open_in_production()
    {
        using var app = App();
        using var anonymous = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(SecurityTxt.Path)).StatusCode);
    }

    private static bool Names(Endpoint endpoint, string pattern) =>
        (endpoint as RouteEndpoint)?.RoutePattern.RawText == pattern;

    private static RouteEndpoint Endpoint(string path, RequestDelegate handler, params object[] metadata) =>
        new(handler, RoutePatternFactory.Parse(path), 0, new EndpointMetadataCollection(metadata), path);

    private static readonly RequestDelegate Ok = context =>
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        return Task.CompletedTask;
    };
}
