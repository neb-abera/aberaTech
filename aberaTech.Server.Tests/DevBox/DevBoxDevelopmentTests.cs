using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using aberaTech.Server.DevBox;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.DevBox;

/// <summary>
/// The two Development conveniences the browser suite relies on, proven to
/// exist only there: the sign-in that skips Google, and the VM in memory.
/// And the heartbeat fed random bodies: whatever arrives, the answer is 200,
/// 400 or 401, never a 500.
/// </summary>
public sealed class DevBoxDevelopmentTests
{
    private static Dictionary<string, string?> Settings(bool withDatabase)
    {
        // Development seeds the scheduling database on start, so the
        // Development host runs with none: the dev box surface does not
        // need one. The sign-in route lives behind the database block, so
        // its Development form is proven by the Playwright suite against
        // the compose app, and its Production form below.
        var settings = withDatabase
            ? ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable)
            : new Dictionary<string, string?>
            {
                ["ClientAddress:ForwardedHops"] = "0",
                ["Admin:GoogleClientId"] = "test-client",
                ["Admin:GoogleClientSecret"] = "test-secret",
                ["Admin:AllowedEmails:0"] = AdminRouteTests.Owner
            };
        settings["Admin:DevelopmentSignIn"] = "true";
        settings["DevBox:SubscriptionId"] = "dev";
        settings["DevBox:Fake"] = "true";
        settings["DevBox:HeartbeatToken"] = "box-token-for-tests";
        return settings;
    }

    private static TestApp App(string environment) => new(
        Settings(withDatabase: environment == "Production"),
        services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()),
        environment);

    [Fact]
    public async Task In_development_the_fake_vm_starts_and_parks_on_the_owner_s_orders()
    {
        using var app = App("Development");
        using var client = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/devbox/status");
        Assert.Equal("deallocated", before.GetProperty("power").GetString());

        using var start = await client.PostAsync("/api/devbox/start", null);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var starting = await client.GetFromJsonAsync<JsonElement>("/api/devbox/status");
        Assert.Equal("starting", starting.GetProperty("power").GetString());

        using var park = await client.PostAsync("/api/devbox/park", null);
        Assert.Equal(HttpStatusCode.Accepted, park.StatusCode);
        var parked = await client.GetFromJsonAsync<JsonElement>("/api/devbox/status");
        Assert.Equal("deallocated", parked.GetProperty("power").GetString());
    }

    [Fact]
    public async Task The_fake_vm_runs_after_its_start_takes()
    {
        var clock = new SettableClock();
        var vm = new FakeDevBoxClient(clock);
        await vm.StartAsync(CancellationToken.None);
        Assert.Equal("starting", await vm.GetPowerStateAsync(CancellationToken.None));
        clock.Now += FakeDevBoxClient.StartTakes;
        Assert.Equal("running", await vm.GetPowerStateAsync(CancellationToken.None));
    }

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Outside_development_the_flags_change_nothing()
    {
        using var app = App("Production");
        using var client = app.CreateClient();

        using var signIn = await client.GetAsync("/api/scheduling/admin/sign-in?returnUrl=/devbox");
        // The real route: a challenge that sends the visitor to Google.
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.StartsWith("https://accounts.google.com/", signIn.Headers.Location?.ToString());
        Assert.False(signIn.Headers.Contains("Set-Cookie") && signIn.Headers.GetValues("Set-Cookie").Any(v => v.StartsWith("__Host-abera.admin=", StringComparison.Ordinal)));

        var services = app.Factory.Services;
        Assert.IsType<DevBoxClient>(services.GetRequiredService<IDevBoxClient>());

        // And the cookie keeps its production shape: __Host-, Secure always.
        var cookie = services
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(aberaTech.Scheduling.Admin.AdminAuth.CookieName, cookie.Cookie.Name);
        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
    }

    [Fact]
    public void In_development_the_cookie_is_the_relaxed_one()
    {
        using var app = App("Development");
        var cookie = app.Factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(aberaTech.Scheduling.Admin.AdminAuth.DevelopmentCookieName, cookie.Cookie.Name);
        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest, cookie.Cookie.SecurePolicy);
    }

    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 40).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task The_heartbeat_never_answers_500_whatever_the_body(int seed)
    {
        using var app = App("Production");
        using var box = app.CreateClient();
        var random = new Random(seed);
        var body = RandomJson(random, depth: 0);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/devbox/heartbeat")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "box-token-for-tests");
        using var response = await box.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType,
            $"seed {seed}: {(int)response.StatusCode} for {body}");
    }

    /// <summary>A JSON value from a seed: objects with the real field names and wrong types, arrays, junk.</summary>
    private static string RandomJson(Random random, int depth)
    {
        string Scalar() => random.Next(7) switch
        {
            0 => "null",
            1 => random.Next(2) == 0 ? "true" : "false",
            2 => random.Next(-5, 1_000_000).ToString(),
            3 => (random.NextDouble() * 1e9).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            4 => JsonSerializer.Serialize(new string(Enumerable.Range(0, random.Next(0, 300)).Select(_ => (char)random.Next(32, 0x2FFF)).ToArray())),
            5 => "\"2026-09-22T00:00:00Z\"",
            _ => "\"https://claude.ai/code?environment=env_" + random.Next() + "\""
        };
        if (depth > 2 || random.Next(3) == 0) return Scalar();
        if (random.Next(2) == 0)
        {
            return "[" + string.Join(",", Enumerable.Range(0, random.Next(0, 4)).Select(_ => RandomJson(random, depth + 1))) + "]";
        }
        string[] names = ["remoteControl", "sessions", "load", "uptimeSeconds", "holdUntil", "environmentUrl", "park", "extra"];
        var fields = Enumerable.Range(0, random.Next(0, 8))
            .Select(_ => JsonSerializer.Serialize(names[random.Next(names.Length)]) + ":" + RandomJson(random, depth + 1));
        return "{" + string.Join(",", fields) + "}";
    }
}
