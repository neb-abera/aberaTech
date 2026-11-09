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
/// /api/training/solves from three chairs, against the judges in memory
/// (<see cref="FakeJudges"/>). A visitor is told to sign in and a stranger
/// is refused, and neither causes a call to a judge. The owner gets the
/// solved problems with their times, the rated contest count, and the
/// upcoming rounds. A judge that is down is reported, not hidden.
/// </summary>
public sealed class TrainingRouteTests : IDisposable
{
    private const string Path = "/api/training/solves?codeforces=Nebikinizer&atcoder=someone";

    private readonly FakeJudges _judges = new();
    private readonly TestApp _app;

    public TrainingRouteTests() => _app = App(_judges);

    private static TestApp App(FakeJudges judges, string environment = "Production", Dictionary<string, string?>? extra = null)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        foreach (var (key, value) in extra ?? []) settings[key] = value;
        return new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(JudgeReader.ClientName).ConfigurePrimaryHttpMessageHandler(() => judges);
        }, environment);
    }

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    [Fact]
    public async Task A_visitor_with_no_session_is_told_to_sign_in_and_no_judge_is_asked()
    {
        using var visitor = _app.CreateClient();
        using var response = await visitor.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_judges.Requests);
    }

    [Fact]
    public async Task A_google_account_that_is_not_the_owner_is_refused()
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);
        using var response = await stranger.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_judges.Requests);
    }

    [Fact]
    public async Task The_owner_reads_solves_with_their_times_and_the_next_rounds()
    {
        using var owner = Owner();
        var body = await owner.GetFromJsonAsync<JsonElement>(Path);

        Assert.True(body.GetProperty("configured").GetBoolean());
        var codeforces = body.GetProperty("codeforces");
        Assert.True(codeforces.GetProperty("ok").GetBoolean());
        Assert.Equal("Nebikinizer", codeforces.GetProperty("handle").GetString());
        var solved = codeforces.GetProperty("solved");
        Assert.Equal(["2263B"], solved.EnumerateObject().Select(p => p.Name));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(FakeJudges.SolvedAt), solved.GetProperty("2263B").GetDateTimeOffset());
        Assert.Equal(0, codeforces.GetProperty("ratedContests").GetInt32());
        Assert.Equal(JsonValueKind.Null, codeforces.GetProperty("rating").ValueKind);

        var atcoder = body.GetProperty("atcoder");
        Assert.True(atcoder.GetProperty("ok").GetBoolean());
        Assert.Empty(atcoder.GetProperty("solved").EnumerateObject());

        var contest = body.GetProperty("contests").EnumerateArray().Single();
        Assert.Equal("Codeforces Round (Div. 3)", contest.GetProperty("name").GetString());
        Assert.Equal("https://codeforces.com/contests/2300", contest.GetProperty("url").GetString());
        Assert.Equal(135, contest.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task A_second_load_within_the_cache_asks_the_judges_nothing()
    {
        using var owner = Owner();
        await owner.GetFromJsonAsync<JsonElement>(Path);
        var asked = _judges.Requests.Count;
        await owner.GetFromJsonAsync<JsonElement>(Path);
        Assert.Equal(asked, _judges.Requests.Count);
    }

    [Fact]
    public async Task A_judge_that_is_down_is_reported_with_its_error()
    {
        _judges.Down = HttpStatusCode.ServiceUnavailable;
        using var owner = Owner();
        var body = await owner.GetFromJsonAsync<JsonElement>(Path);
        var codeforces = body.GetProperty("codeforces");
        Assert.False(codeforces.GetProperty("ok").GetBoolean());
        Assert.Equal("Codeforces answered 503", codeforces.GetProperty("error").GetString());
        Assert.False(body.GetProperty("atcoder").GetProperty("ok").GetBoolean());
        Assert.Empty(body.GetProperty("contests").EnumerateArray());
    }

    [Fact]
    public async Task An_unknown_handle_is_reported_in_the_judges_words()
    {
        using var owner = Owner();
        var body = await owner.GetFromJsonAsync<JsonElement>("/api/training/solves?codeforces=nobody_here");
        var codeforces = body.GetProperty("codeforces");
        Assert.False(codeforces.GetProperty("ok").GetBoolean());
        Assert.Contains("not found", codeforces.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("atcoder").ValueKind);
    }

    [Theory]
    [InlineData("/api/training/solves?codeforces=a")]
    [InlineData("/api/training/solves?codeforces=bad%2Fhandle")]
    [InlineData("/api/training/solves?atcoder=x%20y%20z")]
    public async Task A_handle_that_is_not_one_is_refused_before_any_call(string path)
    {
        using var owner = Owner();
        using var response = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_judges.Requests);
    }

    [Fact]
    public async Task Production_ignores_the_fake_judges_switch()
    {
        using var app = App(_judges, "Production", new() { ["Training:Fake"] = "true" });
        Assert.Null(app.Factory.Services.GetService<FakeJudges>());
        await Task.CompletedTask;
    }

    public void Dispose() => _app.Dispose();
}
