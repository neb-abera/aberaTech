using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using aberaTech.Server.Training;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Training;

/// <summary>
/// /api/training/drills/run against Compiler Explorer in memory
/// (<see cref="FakeCompilerExplorer"/>). Only the owner can run a drill, and
/// a refused caller costs Compiler Explorer nothing. A run passes or fails
/// each test on its output, says when the code did not build, waits out a
/// 429, and stops at the rate limit.
/// </summary>
public sealed class DrillRouteTests : IDisposable
{
    private const string Path = "/api/training/drills/run";

    private readonly FakeCompilerExplorer _compiler = new();
    private readonly TestApp _app;

    public DrillRouteTests() => _app = App(_compiler);

    private static TestApp App(FakeCompilerExplorer compiler, int? perMinute = null)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        if (perMinute is { } limit) settings["RateLimits:DrillsPerMinute"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(DrillRunner.ClientName).ConfigurePrimaryHttpMessageHandler(() => compiler);
        });
    }

    private HttpClient Owner(TestApp? app = null)
    {
        var target = app ?? _app;
        return target.CreateClient().SignedInAs(target.Factory.Services, AdminRouteTests.Owner);
    }

    private static object Sum(string source) => new
    {
        source,
        tests = new[]
        {
            new { stdin = "2 3\n", stdout = "5\n" },
            new { stdin = "-5 5\n", stdout = "0  \n\n" },
            new { stdin = "1 1\n", stdout = "3\n" },
        },
    };

    [Fact]
    public async Task A_visitor_and_a_stranger_are_refused_and_nothing_is_sent()
    {
        using var visitor = _app.CreateClient();
        using var anonymous = await visitor.PostAsJsonAsync(Path, Sum("// fake: sum"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);
        using var refused = await stranger.PostAsJsonAsync(Path, Sum("// fake: sum"));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Empty(_compiler.Bodies);
    }

    [Fact]
    public async Task Each_test_passes_or_fails_on_its_output()
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync(Path, Sum("int main() {} // fake: sum"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("compiled").GetBoolean());
        // The second expects "0" with trailing spaces and a blank line: judges ignore both.
        Assert.Equal([true, true, false], body.GetProperty("tests").EnumerateArray().Select(t => t.GetProperty("passed").GetBoolean()));
        Assert.Equal("2", body.GetProperty("tests")[2].GetProperty("actual").GetString());

        var sent = JsonDocument.Parse(_compiler.Bodies[0]).RootElement;
        Assert.Equal(DrillRunner.Compiler, sent.GetProperty("compiler").GetString());
        Assert.True(sent.GetProperty("options").GetProperty("filters").GetProperty("execute").GetBoolean());
        Assert.True(sent.GetProperty("options").GetProperty("compilerOptions").GetProperty("executorRequest").GetBoolean());
        Assert.Equal("2 3\n", sent.GetProperty("options").GetProperty("executeParameters").GetProperty("stdin").GetString());
        Assert.Equal(3, _compiler.Bodies.Count);
    }

    [Fact]
    public async Task Code_that_does_not_build_runs_no_test_and_says_why()
    {
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync(Path, Sum("#error nope"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("compiled").GetBoolean());
        Assert.Contains("error", body.GetProperty("error").GetString());
        Assert.Empty(body.GetProperty("tests").EnumerateArray());
        Assert.Single(_compiler.Bodies);
    }

    [Fact]
    public async Task A_busy_compiler_explorer_is_waited_out()
    {
        _compiler.Busy = 1;
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync(Path, Sum("// fake: sum"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("compiled").GetBoolean());
        Assert.Equal(4, _compiler.Bodies.Count);
    }

    [Fact]
    public async Task A_compiler_explorer_that_stays_busy_is_reported()
    {
        _compiler.Busy = DrillRunner.Attempts;
        using var owner = Owner();
        using var response = await owner.PostAsJsonAsync(Path, Sum("// fake: sum"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("compiled").GetBoolean());
        Assert.Contains("busy", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_run_that_is_not_one_is_refused_before_any_call()
    {
        using var owner = Owner();
        using var empty = await owner.PostAsJsonAsync(Path, new { source = " ", tests = new[] { new { stdin = "", stdout = "" } } });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        var many = Enumerable.Range(0, DrillRunner.MaxTests + 1).Select(_ => new { stdin = "1", stdout = "1" }).ToArray();
        using var tooMany = await owner.PostAsJsonAsync(Path, new { source = "int main(){}", tests = many });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Empty(_compiler.Bodies);
    }

    [Fact]
    public async Task Runs_stop_at_the_rate_limit()
    {
        var compiler = new FakeCompilerExplorer();
        using var app = App(compiler, perMinute: 2);
        using var owner = Owner(app);
        for (var i = 0; i < 2; i++)
        {
            using var ok = await owner.PostAsJsonAsync(Path, Sum("// fake: sum"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        using var limited = await owner.PostAsJsonAsync(Path, Sum("// fake: sum"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Theory]
    [InlineData("5\n", "5")]
    [InlineData("1 2  \n3\n\n\n", "1 2\n3")]
    [InlineData("a\r\nb\r\n", "a\nb")]
    public void Output_is_compared_as_a_judge_compares_it(string raw, string normal) =>
        Assert.Equal(normal, DrillRunner.Normalize(raw));

    public void Dispose() => _app.Dispose();
}
