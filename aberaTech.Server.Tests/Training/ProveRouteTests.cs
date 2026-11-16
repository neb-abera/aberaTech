using System.Net;
using System.Net.Http.Json;
using System.Text;
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
/// /api/training/drills/prove: a Lean proof goes to the Rust grader's
/// <c>POST /prove</c>, faked here, and the grader's outcome comes back as
/// it was. Without <c>Training:GraderUrl</c> there is no in-process path,
/// and the answer is <c>unavailable</c>.
/// </summary>
public sealed class ProveRouteTests
{
    private const string Path = "/api/training/drills/prove";
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

    private static TestApp App(FakeGrader grader, string? graderUrl = GraderUrl)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        if (graderUrl is not null) settings["Training:GraderUrl"] = graderUrl;
        return new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(GraderClient.ClientName).ConfigurePrimaryHttpMessageHandler(() => grader);
        });
    }

    private static HttpClient Owner(TestApp app) => app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

    private static readonly object Proof = new
    {
        source = "theorem answer : ∀ n : Nat, n = n := fun _ => rfl",
        statement = "∀ n : Nat, n = n",
    };

    private static async Task<JsonElement> Prove(TestApp app)
    {
        using var owner = Owner(app);
        using var response = await owner.PostAsJsonAsync(Path, Proof);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task The_graders_outcome_comes_back_and_the_proof_goes_to_prove()
    {
        var grader = new FakeGrader
        {
            Answer = """{"outcome":"sorry","message":"The proof uses `sorry`.","output":"<source>:1:8: warning: declaration uses `sorry`"}""",
        };
        using var app = App(grader);

        var body = await Prove(app);

        Assert.Equal("sorry", body.GetProperty("outcome").GetString());
        Assert.Equal("The proof uses `sorry`.", body.GetProperty("message").GetString());
        Assert.Equal("<source>:1:8: warning: declaration uses `sorry`", body.GetProperty("output").GetString());
        var (uri, sent) = Assert.Single(grader.Calls);
        Assert.Equal(new Uri("http://grader.test:8080/prove"), uri);
        var request = JsonDocument.Parse(sent).RootElement;
        Assert.Equal("theorem answer : ∀ n : Nat, n = n := fun _ => rfl", request.GetProperty("source").GetString());
        Assert.Equal("∀ n : Nat, n = n", request.GetProperty("statement").GetString());
        Assert.Equal(2, request.EnumerateObject().Count());
    }

    [Fact]
    public async Task A_proved_proof_has_no_message()
    {
        var grader = new FakeGrader { Answer = """{"outcome":"proved","message":null,"output":""}""" };
        using var app = App(grader);
        var body = await Prove(app);
        Assert.Equal("proved", body.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("message").ValueKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":"Compiler Explorer is busy. Try again in a minute."}""", "Compiler Explorer is busy. Try again in a minute.")]
    [InlineData(HttpStatusCode.GatewayTimeout, "not json", "The grader answered 504.")]
    [InlineData(HttpStatusCode.OK, "not json", "The grader sent an answer the site could not read.")]
    [InlineData(HttpStatusCode.OK, """{"outcome":""}""", "The grader sent an answer the site could not read.")]
    public async Task A_grader_refusal_is_unavailable_with_the_reason(HttpStatusCode status, string answer, string message)
    {
        var grader = new FakeGrader { Status = status, Answer = answer };
        using var app = App(grader);
        var body = await Prove(app);
        Assert.Equal("unavailable", body.GetProperty("outcome").GetString());
        Assert.Equal(message, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_silent_grader_is_unavailable()
    {
        using var app = App(new FakeGrader { Silent = true });
        var body = await Prove(app);
        Assert.Equal("unavailable", body.GetProperty("outcome").GetString());
        Assert.Equal("The grader did not answer.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Without_a_grader_a_proof_is_unavailable_and_nothing_is_called()
    {
        var grader = new FakeGrader();
        using var app = App(grader, graderUrl: null);
        var body = await Prove(app);
        Assert.Equal("unavailable", body.GetProperty("outcome").GetString());
        Assert.Empty(grader.Calls);
    }

    [Theory]
    [InlineData(" ", "∀ n : Nat, n = n")]
    [InlineData("theorem answer : True := trivial", "")]
    [InlineData("theorem answer : True := trivial", "True := trivial")]
    [InlineData("theorem answer : True := trivial", "True\ntheorem x : False")]
    public async Task A_malformed_proof_is_refused_before_the_grader(string source, string statement)
    {
        var grader = new FakeGrader();
        using var app = App(grader);
        using var owner = Owner(app);
        using var response = await owner.PostAsJsonAsync(Path, new { source, statement });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(grader.Calls);
    }

    [Fact]
    public async Task A_stranger_is_refused()
    {
        var grader = new FakeGrader();
        using var app = App(grader);
        using var stranger = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Stranger);
        using var refused = await stranger.PostAsJsonAsync(Path, Proof);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Empty(grader.Calls);
    }
}
