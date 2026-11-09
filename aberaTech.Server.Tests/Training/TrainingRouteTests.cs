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

        var contests = body.GetProperty("contests").EnumerateArray().ToList();
        Assert.Equal(["codeforces", "atcoder"], contests.Select(c => c.GetProperty("judge").GetString()));
        Assert.Equal("Codeforces Round (Div. 3)", contests[0].GetProperty("name").GetString());
        Assert.Equal("https://codeforces.com/contests/2300", contests[0].GetProperty("url").GetString());
        Assert.Equal(135, contests[0].GetProperty("durationMinutes").GetInt32());
        Assert.Equal("AtCoder Beginner Contest 999", contests[1].GetProperty("name").GetString());
        Assert.Equal(100, contests[1].GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public void AtCoders_upcoming_table_is_read_row_by_row()
    {
        // Two rows of atcoder.jp/contests/?lang=en as it was on 9 Nov 2026.
        const string html = """
            <div id="contest-table-upcoming"> <h3>Upcoming Contests</h3> <table> <tbody> <tr> <td class="text-center"><a href='http://www.timeanddate.com/worldclock/fixedtime.html?iso=20261114T2100&p1=248' target='blank'><time class='fixtime fixtime-full'>2026-11-14 21:00:00+0900</time></a></td> <td ><span aria-hidden='true' data-toggle='tooltip' data-placement='top' title="Algorithm">Ⓐ</span><span class="user-blue">◉</span> <a href="/contests/abc484">AtCoder Beginner Contest 484</a></td> <td class="text-center">01:40</td> <td class="text-center"> - 1999</td> </tr> <tr> <td class="text-center"><a href='http://www.timeanddate.com/worldclock/fixedtime.html?iso=20261122T2100&p1=248' target='blank'><time class='fixtime fixtime-full'>2026-11-22 21:00:00+0900</time></a></td> <td ><span aria-hidden='true' data-toggle='tooltip' data-placement='top' title="Algorithm">Ⓐ</span><span class="div1-target">◉</span> <a href="/contests/arc234">AtCoder Regular Contest++ 234</a></td> <td class="text-center">02:30</td> <td class="text-center">1600 - 2999</td> </tr> </tbody> </table> </div>
            <div id="contest-table-recent"><a href="/contests/abc483">AtCoder Beginner Contest 483</a></div>
            """;

        var contests = JudgeReader.ParseAtCoderUpcoming(html);

        Assert.Equal(["AtCoder Beginner Contest 484", "AtCoder Regular Contest++ 234"], contests.Select(c => c.Name));
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero), contests[0].Start);
        Assert.Equal([100, 150], contests.Select(c => c.DurationMinutes));
        Assert.Equal("https://atcoder.jp/contests/arc234", contests[1].Url);
        Assert.Empty(JudgeReader.ParseAtCoderUpcoming("<html>no table</html>"));
    }

    [Fact]
    public async Task The_band_is_codeforces_problems_rated_the_rating_to_200_above_newest_first_with_no_tags()
    {
        using var owner = Owner();
        using var response = await owner.GetAsync("/api/training/band?rating=800");
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(text).RootElement;

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal(800, body.GetProperty("from").GetInt32());
        Assert.Equal(1000, body.GetProperty("to").GetInt32());
        var problems = body.GetProperty("problems").EnumerateArray().ToList();
        Assert.Equal(
            ["2263B", "2262A", "2261A", "2260B", "2259B", "2258A", "2257B", "2256A", "2255A"],
            problems.Select(p => p.GetProperty("id").GetString()));
        Assert.Equal("https://codeforces.com/problemset/problem/2260/B", problems[3].GetProperty("url").GetString());
        Assert.Equal(900, problems[3].GetProperty("rating").GetInt32());
        Assert.DoesNotContain("tags", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("greedy", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_problemset_is_read_once_an_hour()
    {
        using var owner = Owner();
        await owner.GetFromJsonAsync<JsonElement>("/api/training/band?rating=800");
        await owner.GetFromJsonAsync<JsonElement>("/api/training/band?rating=900");
        Assert.Single(_judges.Requests, uri => uri.AbsolutePath.EndsWith("/problemset.problems", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?rating=-1")]
    [InlineData("?rating=4001")]
    [InlineData("?rating=abc")]
    public async Task A_band_without_a_sound_rating_is_refused_and_no_judge_is_asked(string query)
    {
        using var owner = Owner();
        using var response = await owner.GetAsync($"/api/training/band{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_judges.Requests);
    }

    [Fact]
    public async Task The_band_is_the_owners_alone()
    {
        using var visitor = _app.CreateClient();
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/training/band?rating=800")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/training/band?rating=800")).StatusCode);
        Assert.Empty(_judges.Requests);
    }

    [Fact]
    public async Task A_band_codeforces_cannot_answer_is_reported_not_hidden()
    {
        _judges.Down = HttpStatusCode.ServiceUnavailable;
        using var owner = Owner();
        var body = await owner.GetFromJsonAsync<JsonElement>("/api/training/band?rating=800");
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal("Codeforces answered 503", body.GetProperty("error").GetString());
        Assert.Empty(body.GetProperty("problems").EnumerateArray());
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

    [Fact]
    public async Task Production_sends_no_sample_questions_even_with_the_fake_switch()
    {
        using var app = App(_judges, "Production", new() { ["Training:Fake"] = "true" });
        Assert.Null(app.Factory.Services.GetService<FakeAssessment>());
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);
        var body = await owner.GetFromJsonAsync<JsonElement>(Path);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("fixture").ValueKind);
    }

    [Fact]
    public async Task The_sample_questions_pass_through_when_registered()
    {
        // Development with Training:Fake registers FakeAssessment
        // (Program.cs). Development also migrates on boot, so this registers
        // it by hand and checks what the endpoint sends.
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        using var app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(JudgeReader.ClientName).ConfigurePrimaryHttpMessageHandler(() => _judges);
            services.AddSingleton<FakeAssessment>();
        }, "Production");
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);
        var body = await owner.GetFromJsonAsync<JsonElement>(Path);
        var mastery = body.GetProperty("fixture").GetProperty("units").GetProperty("refresh-cpp").GetProperty("mastery");
        Assert.Equal(["fixture-overflow", "fixture-power"], mastery.EnumerateArray().Select(id => id.GetString()));
    }

    [Fact]
    public async Task A_read_the_caller_abandoned_is_not_kept()
    {
        var reader = _app.Factory.Services.GetRequiredService<JudgeReader>();
        using var gone = new CancellationTokenSource();
        await gone.CancelAsync();
        Assert.Empty(await reader.UpcomingAsync(gone.Token));
        Assert.False((await reader.CodeforcesAsync("Nebikinizer", gone.Token)).Ok);

        // The next caller still gets the rounds and the solves.
        var contests = await reader.UpcomingAsync(CancellationToken.None);
        Assert.Equal(["Codeforces Round (Div. 3)", "AtCoder Beginner Contest 999"], contests.Select(contest => contest.Name));
        Assert.True((await reader.CodeforcesAsync("Nebikinizer", CancellationToken.None)).Ok);
    }

    public void Dispose() => _app.Dispose();
}
