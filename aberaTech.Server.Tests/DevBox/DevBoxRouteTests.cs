using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Server.DevBox;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Azure.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.DevBox;

/// <summary>
/// The dev box page from three chairs. A visitor is told to sign in, a
/// stranger with a Google account is refused, and the owner reads the power
/// state and presses Start. Azure is a fake that records what it was asked
/// and answers as Resource Manager does, so the test pins the request shape
/// the real one must accept: the URL, the bearer token, the scope.
/// </summary>
public sealed class DevBoxRouteTests : IDisposable
{
    private const string Subscription = "00000000-0000-0000-0000-00000000dead";

    private readonly FakeResourceManager _azure = new();
    private readonly TestApp _app;

    public DevBoxRouteTests()
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["DevBox:SubscriptionId"] = Subscription;

        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<TokenCredential>(new FakeCredential());
            services.AddHttpClient<DevBoxClient>().ConfigurePrimaryHttpMessageHandler(() => _azure);
        });
    }

    public static IEnumerable<object[]> Routes =>
    [
        ["GET", "/api/devbox/status"],
        ["POST", "/api/devbox/start"]
    ];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_visitor_with_no_session_is_told_to_sign_in(string method, string path)
    {
        using var visitor = _app.CreateClient();

        using var response = await AdminRouteTests.Send(visitor, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_azure.Requests);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_google_account_that_is_not_the_owner_is_refused(string method, string path)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);

        using var response = await AdminRouteTests.Send(stranger, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_azure.Requests);
    }

    [Fact]
    public async Task The_owner_reads_the_power_state()
    {
        _azure.PowerState = "deallocated";
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/devbox/status");

        Assert.True(status.GetProperty("configured").GetBoolean());
        Assert.Equal("deallocated", status.GetProperty("power").GetString());

        var request = Assert.Single(_azure.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{Subscription}/resourceGroups/devbox-rg"
            + "/providers/Microsoft.Compute/virtualMachines/devbox/instanceView?api-version=2024-07-01",
            request.Url);
        Assert.Equal("Bearer " + FakeCredential.Token, request.Authorization);
    }

    [Fact]
    public async Task The_owner_starts_the_box()
    {
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

        using var response = await owner.PostAsync("/api/devbox/start", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("starting", body.GetProperty("power").GetString());

        var request = Assert.Single(_azure.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/virtualMachines/devbox/start?api-version=2024-07-01", request.Url);
        Assert.Equal(DevBoxClient.ManagementScope, FakeCredential.LastScope);
    }

    [Fact]
    public async Task Azure_refusing_is_a_bad_gateway_that_names_only_the_exception_type()
    {
        _azure.Answer = HttpStatusCode.Forbidden;
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

        using var response = await owner.PostAsync("/api/devbox/start", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(nameof(HttpRequestException), text);
        Assert.DoesNotContain(Subscription, text);
    }

    [Fact]
    public async Task Without_a_subscription_the_status_says_not_configured_and_start_is_never_mapped()
    {
        using var bare = new TestApp(
            ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable),
            services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        using var owner = bare.CreateClient().SignedInAs(bare.Factory.Services, AdminRouteTests.Owner);

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/devbox/status");
        var endpoints = bare.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        Assert.False(status.GetProperty("configured").GetBoolean());
        Assert.DoesNotContain(endpoints, endpoint =>
            (endpoint as RouteEndpoint)?.RoutePattern.RawText == "/api/devbox/start");
    }

    public void Dispose() => _app.Dispose();

    private sealed class FakeCredential : TokenCredential
    {
        public const string Token = "fake-management-token";

        public static string? LastScope { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            LastScope = requestContext.Scopes.Single();
            return new AccessToken(Token, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    private sealed class FakeResourceManager : HttpMessageHandler
    {
        public sealed record Seen(HttpMethod Method, string Url, string? Authorization);

        public List<Seen> Requests { get; } = [];

        public string PowerState { get; set; } = "running";

        public HttpStatusCode? Answer { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Seen(request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));

            if (Answer is { } forced)
            {
                return Task.FromResult(new HttpResponseMessage(forced));
            }

            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            }

            var body = JsonSerializer.Serialize(new
            {
                statuses = new object[]
                {
                    new { code = "ProvisioningState/succeeded" },
                    new { code = "PowerState/" + PowerState }
                }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
