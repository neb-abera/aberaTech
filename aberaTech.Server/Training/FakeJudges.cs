using System.Net;
using System.Text;

namespace aberaTech.Server.Training;

/// <summary>
/// Codeforces and AtCoder Problems in memory, for `make up` and `make e2e`
/// (Training:Fake, Development only) and the tests. Codeforces says the
/// sample handle solved 2263B and was wrong on 2254B, has no rated contest,
/// and that one Div. 3 round starts in two days. AtCoder Problems says
/// Nebikinizer solved abc407_b and answers other handles with no submissions, and AtCoder lists one Beginner Contest in three days.
/// The problemset holds ten rated problems (<see cref="Problemset"/>). Every request is kept, so a test can say none was
/// made.
/// </summary>
public sealed class FakeJudges : HttpMessageHandler
{
    /// <summary>When 2263B was solved in the sample: 10 Nov 2026 09:40 UTC.</summary>
    public const long SolvedAt = 1_794_303_600;

    /// <summary>
    /// The problemset: 2263B (solved) and eight more rated 800 to 1000, one
    /// rated 1100, one unrated, all tagged so a test can see tags dropped.
    /// </summary>
    public const string Problemset = """
        {"status":"OK","result":{"problems":[
        {"contestId":2263,"index":"B","name":"Solved One","type":"PROGRAMMING","rating":800,"tags":["math"]},
        {"contestId":2262,"index":"A","name":"Band A","type":"PROGRAMMING","rating":800,"tags":["greedy"]},
        {"contestId":2261,"index":"A","name":"Band B","type":"PROGRAMMING","rating":800,"tags":["greedy"]},
        {"contestId":2260,"index":"B","name":"Band C","type":"PROGRAMMING","rating":900,"tags":["strings"]},
        {"contestId":2259,"index":"B","name":"Band D","type":"PROGRAMMING","rating":900,"tags":["sortings"]},
        {"contestId":2258,"index":"A","name":"Band E","type":"PROGRAMMING","rating":900,"tags":["math"]},
        {"contestId":2257,"index":"B","name":"Band F","type":"PROGRAMMING","rating":1000,"tags":["brute force"]},
        {"contestId":2256,"index":"A","name":"Band G","type":"PROGRAMMING","rating":1000,"tags":["implementation"]},
        {"contestId":2255,"index":"A","name":"Band H","type":"PROGRAMMING","rating":1000,"tags":["math"]},
        {"contestId":2254,"index":"C","name":"Too Hard","type":"PROGRAMMING","rating":1100,"tags":["dp"]},
        {"contestId":2253,"index":"A","name":"Unrated","type":"PROGRAMMING","tags":[]}
        ],"problemStatistics":[]}}
        """;

    /// <summary>
    /// AtCoder Problems' problem list. With <see cref="AtCoderModels"/>, five
    /// are in the Codeforces band 800 to 1000 (AtCoder 117 to 382 shown):
    /// abc400_b, abc401_b, abc402_b, abc403_c, and abc407_b, which
    /// Nebikinizer solved. abc404_a is too easy, abc405_c too hard, abc406_b's
    /// model is experimental, and abc408_b has none.
    /// </summary>
    public const string AtCoderProblems = """
        [
        {"id":"abc400_b","contest_id":"abc400","problem_index":"B","name":"Sum of Squares","title":"B. Sum of Squares"},
        {"id":"abc401_b","contest_id":"abc401","problem_index":"B","name":"Two Lamps","title":"B. Two Lamps"},
        {"id":"abc402_b","contest_id":"abc402","problem_index":"B","name":"Queue Line","title":"B. Queue Line"},
        {"id":"abc403_c","contest_id":"abc403","problem_index":"C","name":"Grid Walk","title":"C. Grid Walk"},
        {"id":"abc404_a","contest_id":"abc404","problem_index":"A","name":"Too Easy","title":"A. Too Easy"},
        {"id":"abc405_c","contest_id":"abc405","problem_index":"C","name":"Too Hard","title":"C. Too Hard"},
        {"id":"abc406_b","contest_id":"abc406","problem_index":"B","name":"Experimental","title":"B. Experimental"},
        {"id":"abc407_b","contest_id":"abc407","problem_index":"B","name":"Solved Already","title":"B. Solved Already"},
        {"id":"abc408_b","contest_id":"abc408","problem_index":"B","name":"No Model","title":"B. No Model"}
        ]
        """;

    /// <summary>Raw difficulties: 0, 200, 350, 380, -800, 600, 250 (experimental), and 100.</summary>
    public const string AtCoderModels = """
        {
        "abc400_b":{"slope":-0.001,"intercept":6.0,"variance":0.5,"difficulty":0,"discrimination":0.0045,"irt_loglikelihood":-100.0,"irt_users":4000,"is_experimental":false},
        "abc401_b":{"difficulty":200,"is_experimental":false},
        "abc402_b":{"difficulty":350,"is_experimental":false},
        "abc403_c":{"difficulty":380,"is_experimental":false},
        "abc404_a":{"difficulty":-800,"is_experimental":false},
        "abc405_c":{"difficulty":600,"is_experimental":false},
        "abc406_b":{"difficulty":250,"is_experimental":true},
        "abc407_b":{"difficulty":100,"is_experimental":false},
        "abc999_z":{"slope":-0.001}
        }
        """;

    public List<Uri> Requests { get; } = [];

    /// <summary>Answer every call with this status, to stand in for a judge that is down.</summary>
    public HttpStatusCode? Down { get; set; }

    /// <summary>Answer this host alone with 503, to stand in for one judge that is down.</summary>
    public string? DownHost { get; set; }

    public TimeProvider Time { get; set; } = TimeProvider.System;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (Requests) Requests.Add(uri);
        if (Down is { } down) return Task.FromResult(new HttpResponseMessage(down) { Content = new StringContent("down") });
        if (DownHost == uri.Host) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("down") });

        var path = uri.AbsolutePath;
        var query = uri.Query;
        string body;
        if (uri.Host == "codeforces.com" && path.EndsWith("/user.status", StringComparison.Ordinal))
            body = query.Contains("handle=Nebikinizer", StringComparison.Ordinal)
                ? $$"""{"status":"OK","result":[{"creationTimeSeconds":{{SolvedAt}},"problem":{"contestId":2263,"index":"B"},"verdict":"OK"},{"creationTimeSeconds":{{SolvedAt - 600}},"problem":{"contestId":2254,"index":"B"},"verdict":"WRONG_ANSWER"}]}"""
                : """{"status":"FAILED","comment":"handle: User with handle not found"}""";
        else if (uri.Host == "codeforces.com" && path.EndsWith("/user.rating", StringComparison.Ordinal))
            body = """{"status":"OK","result":[]}""";
        else if (uri.Host == "codeforces.com" && path.EndsWith("/user.info", StringComparison.Ordinal))
            body = """{"status":"OK","result":[{"handle":"Nebikinizer"}]}""";
        else if (uri.Host == "codeforces.com" && path.EndsWith("/contest.list", StringComparison.Ordinal))
            body = $$"""{"status":"OK","result":[{"id":2300,"name":"Codeforces Round (Div. 3)","phase":"BEFORE","durationSeconds":8100,"startTimeSeconds":{{Time.GetUtcNow().AddDays(2).ToUnixTimeSeconds()}}},{"id":2299,"name":"Educational Codeforces Round","phase":"FINISHED","durationSeconds":7200,"startTimeSeconds":1700000000}]}""";
        else if (uri.Host == "codeforces.com" && path.EndsWith("/problemset.problems", StringComparison.Ordinal))
            body = Problemset;
        else if (uri.Host == "kenkoooo.com" && path.EndsWith("/resources/problems.json", StringComparison.Ordinal))
            body = AtCoderProblems;
        else if (uri.Host == "kenkoooo.com" && path.EndsWith("/resources/problem-models.json", StringComparison.Ordinal))
            body = AtCoderModels;
        else if (uri.Host == "kenkoooo.com")
            body = query.Contains("user=Nebikinizer", StringComparison.Ordinal) && query.Contains("from_second=0", StringComparison.Ordinal)
                ? $$"""[{"id":1,"epoch_second":{{SolvedAt}},"problem_id":"abc407_b","contest_id":"abc407","user_id":"Nebikinizer","language":"C++","point":200.0,"length":300,"result":"AC","execution_time":1}]"""
                : "[]";
        else if (uri.Host == "atcoder.jp")
        {
            var at = Time.GetUtcNow().AddDays(3).ToOffset(TimeSpan.FromHours(9));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"<div id=\"contest-table-upcoming\"><table><tbody><tr><td class=\"text-center\"><a href='x'><time class='fixtime fixtime-full'>{at:yyyy-MM-dd} 21:00:00+0900</time></a></td><td ><a href=\"/contests/abc999\">AtCoder Beginner Contest 999</a></td><td class=\"text-center\">01:40</td><td class=\"text-center\"> - 1999</td></tr></tbody></table></div><div id=\"contest-table-recent\"></div>",
                    Encoding.UTF8, "text/html"),
            });
        }
        else
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
