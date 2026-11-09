using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using aberaTech.Server.Training;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Training;

/// <summary>
/// /api/training/drills/run with <c>Training:GraderUrl</c> set: the drill
/// goes to the Rust grader, faked here, and Compiler Explorer hears nothing
/// from this process. The grader's verdicts become the page's results, and
/// its refusals and silences become a message. Without the setting the
/// in-process path runs and the grader hears nothing.
/// </summary>
public sealed class GraderRouteTests
{
    private const string Path = "/api/training/drills/run";
    private const string GraderUrl = "http://grader.test:8080";

    private sealed class FakeGrader : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Calls { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Answer { get; set; } = "{}";
        public bool Silent { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Calls) Calls.Add((request.RequestUri!, body));
            if (Silent) throw new HttpRequestException("connection refused");
            return new HttpResponseMessage(Status) { Content = new StringContent(Answer, Encoding.UTF8, "application/json") };
        }
    }

    private static TestApp App(FakeGrader grader, FakeCompilerExplorer compiler, string? graderUrl = GraderUrl)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        if (graderUrl is not null) settings["Training:GraderUrl"] = graderUrl;
        return new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(DrillRunner.ClientName).ConfigurePrimaryHttpMessageHandler(() => compiler);
            services.AddHttpClient(GraderClient.ClientName).ConfigurePrimaryHttpMessageHandler(() => grader);
        });
    }

    private static HttpClient Owner(TestApp app) => app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

    private static readonly object Drill = new
    {
        source = "int main() {}",
        tests = new[]
        {
            new { stdin = "2 3\n", stdout = "5\n" },
            new { stdin = "1 1\n", stdout = "2\n" },
            new { stdin = "9\n", stdout = "9\n" },
            new { stdin = "0\n", stdout = "0\n" },
            new { stdin = "-1\n", stdout = "-1\n" },
        },
    };

    private static async Task<JsonElement> Run(TestApp app)
    {
        using var owner = Owner(app);
        using var response = await owner.PostAsJsonAsync(Path, Drill);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Each_verdict_becomes_a_result_and_compiler_explorer_hears_nothing_from_here()
    {
        var grader = new FakeGrader
        {
            Answer = """
                {"compiled":true,"compileError":null,"tests":[
                  {"verdict":"pass","stdout":"5","message":null,"diff":null,"execMs":3},
                  {"verdict":"wrong_answer","stdout":"3","message":null,"diff":{"line":1,"expected":"2","actual":"3"},"execMs":3},
                  {"verdict":"runtime_error","stdout":"","message":"It exited with code 139. Segmentation fault","diff":null,"execMs":3},
                  {"verdict":"time_limit","stdout":"","message":"It ran past the 2000 ms limit.","diff":null,"execMs":2500},
                  {"verdict":"something_new","stdout":"","message":null,"diff":null,"execMs":null}]}
                """,
        };
        var compiler = new FakeCompilerExplorer();
        using var app = App(grader, compiler);

        var body = await Run(app);

        Assert.True(body.GetProperty("compiled").GetBoolean());
        var tests = body.GetProperty("tests").EnumerateArray().ToList();
        Assert.Equal([true, false, false, false, false], tests.Select(t => t.GetProperty("passed").GetBoolean()));
        Assert.Equal("3", tests[1].GetProperty("actual").GetString());
        Assert.Equal(JsonValueKind.Null, tests[1].GetProperty("error").ValueKind);
        Assert.Equal("It exited with code 139. Segmentation fault", tests[2].GetProperty("error").GetString());
        Assert.Equal("It ran past the 2000 ms limit.", tests[3].GetProperty("error").GetString());
        Assert.Equal("It did not run.", tests[4].GetProperty("error").GetString());
        Assert.Empty(compiler.Bodies);

        var (uri, sent) = Assert.Single(grader.Calls);
        Assert.Equal(new Uri("http://grader.test:8080/grade"), uri);
        var request = JsonDocument.Parse(sent).RootElement;
        Assert.Equal("int main() {}", request.GetProperty("source").GetString());
        Assert.Equal("c++", request.GetProperty("language").GetString());
        Assert.Equal(DrillRunner.Compiler, request.GetProperty("compiler").GetString());
        Assert.Equal(DrillRunner.Flags, request.GetProperty("flags").GetString());
        var first = request.GetProperty("tests")[0];
        Assert.Equal("2 3\n", first.GetProperty("stdin").GetString());
        Assert.Equal("5\n", first.GetProperty("expected").GetString());
        Assert.Equal(GraderClient.TimeLimitMs, first.GetProperty("timeLimitMs").GetInt32());
        Assert.Equal(3, first.EnumerateObject().Count());
    }

    [Fact]
    public async Task Code_that_does_not_build_says_why_and_runs_no_test()
    {
        var grader = new FakeGrader
        {
            Answer = """{"compiled":false,"compileError":"<source>:1:2: error: #error","tests":[{"verdict":"compile_error","stdout":"","message":"x","diff":null,"execMs":null}]}""",
        };
        using var app = App(grader, new FakeCompilerExplorer());
        var body = await Run(app);
        Assert.False(body.GetProperty("compiled").GetBoolean());
        Assert.Equal("<source>:1:2: error: #error", body.GetProperty("error").GetString());
        Assert.Empty(body.GetProperty("tests").EnumerateArray());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":"Compiler Explorer is busy. Try again in a minute."}""", "Compiler Explorer is busy. Try again in a minute.")]
    [InlineData(HttpStatusCode.BadGateway, """{"error":"Compiler Explorer answered 500."}""", "Compiler Explorer answered 500.")]
    [InlineData(HttpStatusCode.GatewayTimeout, "not json", "The grader answered 504.")]
    [InlineData(HttpStatusCode.OK, "not json", "The grader sent an answer the site could not read.")]
    public async Task A_grader_refusal_is_reported(HttpStatusCode status, string answer, string message)
    {
        var grader = new FakeGrader { Status = status, Answer = answer };
        using var app = App(grader, new FakeCompilerExplorer());
        var body = await Run(app);
        Assert.False(body.GetProperty("compiled").GetBoolean());
        Assert.Equal(message, body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_silent_grader_is_reported()
    {
        var grader = new FakeGrader { Silent = true };
        using var app = App(grader, new FakeCompilerExplorer());
        var body = await Run(app);
        Assert.False(body.GetProperty("compiled").GetBoolean());
        Assert.Equal("The grader did not answer.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Without_the_setting_the_in_process_path_runs_and_the_grader_hears_nothing()
    {
        var grader = new FakeGrader();
        var compiler = new FakeCompilerExplorer();
        using var app = App(grader, compiler, graderUrl: null);
        var body = await Run(app);
        Assert.True(body.GetProperty("compiled").GetBoolean());
        Assert.Equal(5, compiler.Bodies.Count);
        Assert.Empty(grader.Calls);
    }

    [Fact]
    public async Task A_refused_caller_reaches_neither()
    {
        var grader = new FakeGrader();
        var compiler = new FakeCompilerExplorer();
        using var app = App(grader, compiler);
        using var stranger = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Stranger);
        using var refused = await stranger.PostAsJsonAsync(Path, Drill);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        using var owner = Owner(app);
        using var empty = await owner.PostAsJsonAsync(Path, new { source = " ", tests = new[] { new { stdin = "", stdout = "" } } });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Empty(grader.Calls);
        Assert.Empty(compiler.Bodies);
    }

    [Fact]
    public void An_address_that_is_not_http_stops_the_server_from_starting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Training:GraderUrl"] = "file:///etc/passwd" })
            .Build();
        Assert.Throws<InvalidOperationException>(() => { _ = new GraderClient(null!, configuration); });
    }
}
