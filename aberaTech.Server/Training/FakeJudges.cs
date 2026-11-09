using System.Net;
using System.Text;

namespace aberaTech.Server.Training;

/// <summary>
/// Codeforces and AtCoder Problems in memory, for `make up` and `make e2e`
/// (Training:Fake, Development only) and the tests. Codeforces says the
/// sample handle solved 2263B and was wrong on 2254B, has no rated contest,
/// and that one Div. 3 round starts in two days. AtCoder Problems answers
/// with no submissions, and AtCoder lists one Beginner Contest in three days.
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

    public List<Uri> Requests { get; } = [];

    /// <summary>Answer every call with this status, to stand in for a judge that is down.</summary>
    public HttpStatusCode? Down { get; set; }

    public TimeProvider Time { get; set; } = TimeProvider.System;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (Requests) Requests.Add(uri);
        if (Down is { } down) return Task.FromResult(new HttpResponseMessage(down) { Content = new StringContent("down") });

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
        else if (uri.Host == "kenkoooo.com")
            body = "[]";
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
