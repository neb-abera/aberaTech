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
/// Mostafa Saad's supervision pool, read from his sheet in memory
/// (<see cref="FakeSaadSheet"/>): what is kept, how codes become links, the
/// hour in memory, and who may ask.
/// </summary>
public sealed class SaadPoolTests : IDisposable
{
    private const string Path = "/api/training/pool";

    private readonly FakeSaadSheet _sheet = new();
    private readonly TestApp _app;

    public SaadPoolTests()
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(SaadPool.ClientName).ConfigurePrimaryHttpMessageHandler(() => _sheet);
        }, "Production");
    }

    [Fact]
    public void Keeps_importance_P5_to_P3_with_a_level()
    {
        var (problems, rows) = SaadPool.Parse(FakeSaadSheet.Csv);
        Assert.Equal(16, rows);
        // P2 and the row with no importance are dropped.
        Assert.DoesNotContain(problems, problem => problem.Code is "CF12-D2-E" or "CF13-D2-E");
        Assert.Equal(14, problems.Count);
        Assert.Equal(5, problems.Single(problem => problem.Code == "CF1-D2-A").Importance);
        Assert.Equal(3, problems.Single(problem => problem.Code == "CF3-D2-B").Importance);
        Assert.Equal(4.5, problems.Single(problem => problem.Code == "CF10-D2-D").Level);
        Assert.True(problems.Single(problem => problem.Code == "CF1-D2-A").Editorial);
    }

    [Theory]
    [InlineData("CF145-D1-D", "https://codeforces.com/contest/145/problem/D", "145D")]
    [InlineData("CF513-D12-F2", "https://codeforces.com/contest/513/problem/F2", "513F2")]
    [InlineData("CF100829-GYM-A", "https://codeforces.com/gym/100829/problem/A", null)]
    [InlineData("Atcoder087-ARC-C", "https://atcoder.jp/contests/arc087/tasks/arc087_c", null)]
    [InlineData("AtCoder005-AGC-E", "https://atcoder.jp/contests/agc005/tasks/agc005_e", null)]
    [InlineData("SPOJ METEORS", "https://www.spoj.com/problems/METEORS/", null)]
    [InlineData("Codechef CLOSEFAR", "https://www.codechef.com/problems/CLOSEFAR", null)]
    [InlineData("UVA 11722", null, null)]
    public void Turns_a_code_into_its_page(string code, string? url, string? codeforces)
    {
        Assert.Equal((url, codeforces), SaadPool.Link(code));
    }

    [Fact]
    public void Reads_quoted_fields_with_commas_quotes_and_line_breaks()
    {
        var records = SaadPool.ReadCsv("a,\"b, \"\"c\"\"\",\"d\ne\"\n1,2,3\n");
        Assert.Equal(["a", "b, \"c\"", "d\ne"], records[0]);
        Assert.Equal(["1", "2", "3"], records[1]);
    }

    [Fact]
    public async Task A_visitor_and_a_stranger_are_refused_and_the_sheet_is_not_asked()
    {
        using var visitor = _app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(Path)).StatusCode);
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(Path)).StatusCode);
        Assert.Empty(_sheet.Requests);
    }

    [Fact]
    public async Task The_owner_reads_the_pool_and_a_second_load_within_the_hour_asks_nothing()
    {
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);
        var body = await owner.GetFromJsonAsync<JsonElement>(Path);
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal(16, body.GetProperty("rows").GetInt32());
        Assert.Equal(SaadPool.Sheet, body.GetProperty("source").GetString());
        var first = body.GetProperty("problems").EnumerateArray().First();
        Assert.Equal("CF1-D2-A", first.GetProperty("code").GetString());
        Assert.Equal("1A", first.GetProperty("codeforces").GetString());
        Assert.Single(_sheet.Requests);
        Assert.Equal(SaadPool.Csv, _sheet.Requests[0]);

        await owner.GetFromJsonAsync<JsonElement>(Path);
        Assert.Single(_sheet.Requests);
    }

    [Fact]
    public async Task A_sheet_that_is_down_is_reported()
    {
        _sheet.Down = HttpStatusCode.ServiceUnavailable;
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);
        var body = await owner.GetFromJsonAsync<JsonElement>(Path);
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal("The sheet answered 503", body.GetProperty("error").GetString());
        Assert.Empty(body.GetProperty("problems").EnumerateArray());
    }

    public void Dispose() => _app.Dispose();
}
