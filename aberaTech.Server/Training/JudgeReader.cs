using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace aberaTech.Server.Training;

/// <summary>What one judge says about one handle, read at request time.</summary>
public sealed record JudgeAnswer(
    string Handle,
    bool Ok,
    string? Error,
    int? Rating,
    int? MaxRating,
    string? Rank,
    int RatedContests,
    IReadOnlyDictionary<string, DateTimeOffset> Solved,
    DateTimeOffset ReadAt);

/// <summary>A Codeforces problemset problem: its id ("2263B"), name, rating and page. Never its tags.</summary>
public sealed record BandProblem(string Id, string Name, int Rating, string Url);

/// <summary>Codeforces problems rated <paramref name="From"/> to <paramref name="To"/>, newest contest first.</summary>
public sealed record BandAnswer(bool Ok, string? Error, int From, int To, IReadOnlyList<BandProblem> Problems, DateTimeOffset ReadAt);

/// <summary>
/// An AtCoder problem from AtCoder Problems: its id ("abc482_c"), contest,
/// letter, name, page, and difficulty as AtCoder Problems shows it.
/// </summary>
public sealed record AtCoderBandProblem(string Id, string Contest, string Index, string Name, int Difficulty, string Url);

/// <summary>AtCoder problems at shown difficulty <paramref name="From"/> to <paramref name="To"/>.</summary>
public sealed record AtCoderBandAnswer(bool Ok, string? Error, int From, int To, IReadOnlyList<AtCoderBandProblem> Problems, DateTimeOffset ReadAt);

/// <summary>A rated contest that has not started.</summary>
public sealed record UpcomingContest(string Name, DateTimeOffset Start, int DurationMinutes, string Url, string Judge);

/// <summary>
/// Reads the owner's solves from the Codeforces API and from AtCoder
/// Problems, and the upcoming Codeforces rounds. Nothing is stored: each
/// answer is kept in memory for <see cref="CacheFor"/> so a page that loads
/// twice asks once, and is gone when the process ends.
/// </summary>
/// <remarks>
/// Both APIs are public and need no key. Codeforces asks for at most one
/// call each two seconds (codeforces.com/apiHelp). AtCoder Problems asks
/// for a second between calls and pages submissions 500 at a time
/// (github.com/kenkoooo/AtCoderProblems, doc/api.md). A judge that does
/// not answer is reported as such, and the page falls back to manual ticks.
/// </remarks>
public sealed partial class JudgeReader(IHttpClientFactory clients, IMemoryCache cache, TimeProvider time)
{
    public const string ClientName = "judges";

    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    /// <summary>At most this many AtCoder pages, 10,000 submissions.</summary>
    public const int AtCoderPages = 20;

    public static readonly Uri Codeforces = new("https://codeforces.com/api/");

    public static readonly Uri AtCoderProblems = new("https://kenkoooo.com/atcoder/atcoder-api/v3/");

    /// <summary>Handles on both judges: letters, digits, underscore, dot, hyphen. 3 to 24 long.</summary>
    [GeneratedRegex("^[A-Za-z0-9_.-]{3,24}$")]
    private static partial Regex HandlePattern();

    public static bool IsHandle(string? handle) => handle is not null && HandlePattern().IsMatch(handle);

    private HttpClient Client => clients.CreateClient(ClientName);

    private DateTimeOffset Now => time.GetUtcNow();

    public Task<JudgeAnswer> CodeforcesAsync(string handle, CancellationToken cancellationToken) =>
        Cached($"judges:codeforces:{handle}", () => ReadCodeforcesAsync(handle, cancellationToken), cancellationToken);

    public Task<JudgeAnswer> AtCoderAsync(string handle, CancellationToken cancellationToken) =>
        Cached($"judges:atcoder:{handle}", () => ReadAtCoderAsync(handle, cancellationToken), cancellationToken);

    public static readonly Uri AtCoderContests = new("https://atcoder.jp/contests/?lang=en");

    /// <summary>The next rounds on both judges, soonest first.</summary>
    public async Task<IReadOnlyList<UpcomingContest>> UpcomingAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue("judges:contests", out IReadOnlyList<UpcomingContest>? kept) && kept is not null) return kept;
        var codeforces = await UpcomingCodeforcesAsync(cancellationToken);
        var atcoder = await UpcomingAtCoderAsync(cancellationToken);
        var contests = codeforces.Concat(atcoder).OrderBy(contest => contest.Start).Take(10).ToList();
        // A request the browser left mid-call reads as an empty list. Keeping
        // it would hide every round for the next five minutes.
        if (!cancellationToken.IsCancellationRequested)
            cache.Set("judges:contests", (IReadOnlyList<UpcomingContest>)contests, CacheFor);
        return contests;
    }

    private async Task<IReadOnlyList<UpcomingContest>> UpcomingCodeforcesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var document = await CodeforcesCallAsync("contest.list?gym=false", cancellationToken);
            return document.RootElement.GetProperty("result").EnumerateArray()
                .Where(contest => contest.GetProperty("phase").GetString() == "BEFORE")
                .Select(contest => new UpcomingContest(
                    contest.GetProperty("name").GetString() ?? "",
                    DateTimeOffset.FromUnixTimeSeconds(contest.GetProperty("startTimeSeconds").GetInt64()),
                    contest.GetProperty("durationSeconds").GetInt32() / 60,
                    $"https://codeforces.com/contests/{contest.GetProperty("id").GetInt32()}",
                    "codeforces"))
                .OrderBy(contest => contest.Start)
                .Take(6)
                .ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or JudgeException or TaskCanceledException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"<time class='fixtime fixtime-full'>([^<]+)</time>.*?<a href=""/contests/([a-z0-9_]+)"">([^<]+)</a>.*?<td class=""text-center"">(\d+):(\d\d)</td>", RegexOptions.Singleline)]
    private static partial Regex AtCoderRow();

    /// <summary>
    /// AtCoder has no schedule API. Its contest list's "Upcoming Contests"
    /// table is read: each row's start time, contest link and duration.
    /// </summary>
    public static IReadOnlyList<UpcomingContest> ParseAtCoderUpcoming(string html)
    {
        var start = html.IndexOf("contest-table-upcoming", StringComparison.Ordinal);
        if (start < 0) return [];
        var end = html.IndexOf("contest-table-recent", start, StringComparison.Ordinal);
        var table = end < 0 ? html[start..] : html[start..end];
        var contests = new List<UpcomingContest>();
        foreach (Match row in AtCoderRow().Matches(table))
        {
            if (!DateTimeOffset.TryParseExact(row.Groups[1].Value, "yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) continue;
            var minutes = int.Parse(row.Groups[4].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(row.Groups[5].Value, CultureInfo.InvariantCulture);
            contests.Add(new UpcomingContest(
                WebUtility.HtmlDecode(row.Groups[3].Value), at, minutes,
                $"https://atcoder.jp/contests/{row.Groups[2].Value}", "atcoder"));
        }
        return contests;
    }

    private async Task<IReadOnlyList<UpcomingContest>> UpcomingAtCoderAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client.GetAsync(AtCoderContests, cancellationToken);
            if (!response.IsSuccessStatusCode) return [];
            return ParseAtCoderUpcoming(await response.Content.ReadAsStringAsync(cancellationToken)).Take(4).ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    /// <summary>The problemset is held this long: a new round adds a few problems a week.</summary>
    public static readonly TimeSpan ProblemsetFor = TimeSpan.FromHours(1);

    /// <summary>At most this many band problems, from the newest contests.</summary>
    public const int BandSize = 300;

    /// <summary>
    /// Codeforces problemset problems rated <paramref name="from"/> to
    /// <paramref name="to"/>, from problemset.problems. Tags are dropped here,
    /// so the page cannot show them: the plan's practice in a band is untagged.
    /// </summary>
    public async Task<BandAnswer> BandAsync(int from, int to, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue("judges:problemset-failed", out string? failed) && failed is not null)
            return new BandAnswer(false, failed, from, to, [], Now);
        if (!cache.TryGetValue("judges:problemset", out IReadOnlyList<BandProblem>? all) || all is null)
        {
            string error;
            try
            {
                using var document = await CodeforcesCallAsync("problemset.problems", cancellationToken);
                all = ParseProblemset(document.RootElement);
                cache.Set("judges:problemset", all, ProblemsetFor);
                error = "";
            }
            catch (JudgeException exception)
            {
                error = exception.Message;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
            {
                error = "Codeforces did not answer.";
            }
            if (all is null)
            {
                // Held for a minute, so a page left open does not ask again
                // on every load. A request the browser left is not held.
                if (!cancellationToken.IsCancellationRequested)
                    cache.Set("judges:problemset-failed", error, TimeSpan.FromMinutes(1));
                return new BandAnswer(false, error, from, to, [], Now);
            }
        }
        var band = all.Where(problem => problem.Rating >= from && problem.Rating <= to).Take(BandSize).ToList();
        return new BandAnswer(true, null, from, to, band, Now);
    }

    /// <summary>AtCoder Problems' problem list and difficulty models. Both change when a contest ends.</summary>
    public static readonly Uri AtCoderResources = new("https://kenkoooo.com/atcoder/resources/");

    /// <summary>The two AtCoder Problems files are held this long, at most.</summary>
    public static readonly TimeSpan AtCoderProblemsFor = TimeSpan.FromDays(1);

    /// <summary>
    /// A Codeforces rating on AtCoder's scale, by the straight line in
    /// silverfoxxxy's Rating converter (silverfoxxxy.github.io/rating-converter,
    /// converter.js, read 11 Nov 2026): Codeforces 0 is AtCoder -940, and
    /// Codeforces 3900 is AtCoder 4215.
    /// </summary>
    public static int AtCoderOf(int codeforces) =>
        (int)Math.Round(-940 + codeforces * 5155.0 / 3900, MidpointRounding.AwayFromZero);

    /// <summary>
    /// A difficulty as AtCoder Problems shows it. Below 400 its models run
    /// negative, and the site shows 400 / e^(1 - d/400), as AtCoder shows a
    /// low rating (clipDifficulty in atcoder-problems-frontend/src/utils).
    /// </summary>
    public static int ShownDifficulty(double difficulty) =>
        (int)Math.Round(difficulty >= 400 ? difficulty : 400 / Math.Exp(1.0 - difficulty / 400), MidpointRounding.AwayFromZero);

    /// <summary>
    /// AtCoder problems whose shown difficulty is the Codeforces band
    /// <paramref name="from"/> to <paramref name="to"/> on AtCoder's scale,
    /// from AtCoder Problems' problems.json and problem-models.json. A model
    /// AtCoder Problems marks experimental is left out.
    /// </summary>
    public async Task<AtCoderBandAnswer> AtCoderBandAsync(int from, int to, CancellationToken cancellationToken)
    {
        int low = AtCoderOf(from), high = AtCoderOf(to);
        if (cache.TryGetValue("judges:atcoder-problems-failed", out string? failed) && failed is not null)
            return new AtCoderBandAnswer(false, failed, low, high, [], Now);
        if (!cache.TryGetValue("judges:atcoder-problems", out IReadOnlyList<AtCoderBandProblem>? all) || all is null)
        {
            string error;
            try
            {
                using var problems = await AtCoderResourceAsync("problems.json", cancellationToken);
                using var models = await AtCoderResourceAsync("problem-models.json", cancellationToken);
                all = ParseAtCoderProblems(problems.RootElement, models.RootElement);
                cache.Set("judges:atcoder-problems", all, AtCoderProblemsFor);
                error = "";
            }
            catch (JudgeException exception)
            {
                error = exception.Message;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
            {
                error = "AtCoder Problems did not answer.";
            }
            if (all is null)
            {
                if (!cancellationToken.IsCancellationRequested)
                    cache.Set("judges:atcoder-problems-failed", error, TimeSpan.FromMinutes(1));
                return new AtCoderBandAnswer(false, error, low, high, [], Now);
            }
        }
        var band = all.Where(problem => problem.Difficulty >= low && problem.Difficulty <= high).Take(BandSize).ToList();
        return new AtCoderBandAnswer(true, null, low, high, band, Now);
    }

    /// <summary>
    /// Every problem with a difficulty model that is not experimental, with
    /// its shown difficulty, newest contest first within each kind.
    /// </summary>
    public static IReadOnlyList<AtCoderBandProblem> ParseAtCoderProblems(JsonElement problems, JsonElement models)
    {
        var list = new List<AtCoderBandProblem>();
        foreach (var problem in problems.EnumerateArray())
        {
            var id = problem.GetProperty("id").GetString() ?? "";
            if (!models.TryGetProperty(id, out var model)) continue;
            if (!model.TryGetProperty("difficulty", out var difficulty) || difficulty.ValueKind != JsonValueKind.Number) continue;
            if (model.TryGetProperty("is_experimental", out var experimental) && experimental.ValueKind == JsonValueKind.True) continue;
            var contest = problem.GetProperty("contest_id").GetString() ?? "";
            list.Add(new AtCoderBandProblem(
                id,
                contest,
                problem.TryGetProperty("problem_index", out var index) ? index.GetString() ?? "" : "",
                problem.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                ShownDifficulty(difficulty.GetDouble()),
                $"https://atcoder.jp/contests/{Uri.EscapeDataString(contest)}/tasks/{Uri.EscapeDataString(id)}"));
        }
        return list.OrderByDescending(problem => problem.Contest, StringComparer.Ordinal).ThenBy(problem => problem.Id, StringComparer.Ordinal).ToList();
    }

    private async Task<JsonDocument> AtCoderResourceAsync(string file, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(new Uri(AtCoderResources, file), cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new JudgeException($"AtCoder Problems answered {(int)response.StatusCode}");
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
    }

    /// <summary>Every rated problem with a contest, newest contest first. Tags are not read.</summary>
    public static IReadOnlyList<BandProblem> ParseProblemset(JsonElement root)
    {
        var problems = new List<(int Contest, BandProblem Problem)>();
        foreach (var problem in root.GetProperty("result").GetProperty("problems").EnumerateArray())
        {
            if (!problem.TryGetProperty("contestId", out var contest) || !problem.TryGetProperty("rating", out var rating)) continue;
            var id = contest.GetInt32();
            var index = problem.GetProperty("index").GetString() ?? "";
            problems.Add((id, new BandProblem(
                $"{id}{index}",
                problem.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                rating.GetInt32(),
                $"https://codeforces.com/problemset/problem/{id}/{Uri.EscapeDataString(index)}")));
        }
        return problems.OrderByDescending(each => each.Contest).Select(each => each.Problem).ToList();
    }

    private async Task<JudgeAnswer> Cached(string key, Func<Task<JudgeAnswer>> read, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(key, out JudgeAnswer? kept) && kept is not null) return kept;
        var answer = await read();
        // A request the browser left mid-call reads as a failure. It is not
        // kept. A real failure is kept for a minute only, so the next load
        // tries again soon.
        if (!cancellationToken.IsCancellationRequested)
            cache.Set(key, answer, answer.Ok ? CacheFor : TimeSpan.FromMinutes(1));
        return answer;
    }

    private JudgeAnswer Failed(string handle, string error) =>
        new(handle, false, error, null, null, null, 0, new Dictionary<string, DateTimeOffset>(), Now);

    private async Task<JsonDocument> CodeforcesCallAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(new Uri(Codeforces, path), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new JudgeException($"Codeforces answered {(int)response.StatusCode}");
        }
        if (document.RootElement.TryGetProperty("status", out var status) && status.GetString() == "OK") return document;
        var comment = document.RootElement.TryGetProperty("comment", out var c) ? c.GetString() : null;
        document.Dispose();
        throw new JudgeException(comment ?? $"Codeforces answered {(int)response.StatusCode}");
    }

    private async Task<JudgeAnswer> ReadCodeforcesAsync(string handle, CancellationToken cancellationToken)
    {
        try
        {
            var solved = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            using (var status = await CodeforcesCallAsync($"user.status?handle={Uri.EscapeDataString(handle)}", cancellationToken))
            {
                foreach (var submission in status.RootElement.GetProperty("result").EnumerateArray())
                {
                    if (!submission.TryGetProperty("verdict", out var verdict) || verdict.GetString() != "OK") continue;
                    var problem = submission.GetProperty("problem");
                    if (!problem.TryGetProperty("contestId", out var contestId)) continue;
                    var id = $"{contestId.GetInt32()}{problem.GetProperty("index").GetString()}";
                    var at = DateTimeOffset.FromUnixTimeSeconds(submission.GetProperty("creationTimeSeconds").GetInt64());
                    if (!solved.TryGetValue(id, out var first) || at < first) solved[id] = at;
                }
            }

            int ratedContests;
            using (var rating = await CodeforcesCallAsync($"user.rating?handle={Uri.EscapeDataString(handle)}", cancellationToken))
            {
                ratedContests = rating.RootElement.GetProperty("result").GetArrayLength();
            }

            int? current = null, max = null;
            string? rank = null;
            using (var info = await CodeforcesCallAsync($"user.info?handles={Uri.EscapeDataString(handle)}", cancellationToken))
            {
                var user = info.RootElement.GetProperty("result")[0];
                if (user.TryGetProperty("rating", out var r)) current = r.GetInt32();
                if (user.TryGetProperty("maxRating", out var m)) max = m.GetInt32();
                if (user.TryGetProperty("rank", out var k)) rank = k.GetString();
            }

            return new JudgeAnswer(handle, true, null, current, max, rank, ratedContests, solved, Now);
        }
        catch (JudgeException exception)
        {
            return Failed(handle, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
        {
            return Failed(handle, "Codeforces did not answer.");
        }
    }

    private async Task<JudgeAnswer> ReadAtCoderAsync(string handle, CancellationToken cancellationToken)
    {
        try
        {
            var solved = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            long from = 0;
            for (var page = 0; page < AtCoderPages; page++)
            {
                if (page > 0) await Task.Delay(TimeSpan.FromSeconds(1), time, cancellationToken);
                var path = $"user/submissions?user={Uri.EscapeDataString(handle)}&from_second={from.ToString(CultureInfo.InvariantCulture)}";
                using var response = await Client.GetAsync(new Uri(AtCoderProblems, path), cancellationToken);
                if (response.StatusCode != HttpStatusCode.OK)
                    return Failed(handle, $"AtCoder Problems answered {(int)response.StatusCode}");
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var submissions = document.RootElement.EnumerateArray().ToList();
                foreach (var submission in submissions)
                {
                    from = Math.Max(from, submission.GetProperty("epoch_second").GetInt64() + 1);
                    if (submission.GetProperty("result").GetString() != "AC") continue;
                    var id = submission.GetProperty("problem_id").GetString() ?? "";
                    var at = DateTimeOffset.FromUnixTimeSeconds(submission.GetProperty("epoch_second").GetInt64());
                    if (!solved.TryGetValue(id, out var first) || at < first) solved[id] = at;
                }
                if (submissions.Count < 500) break;
            }
            return new JudgeAnswer(handle, true, null, null, null, null, 0, solved, Now);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
        {
            return Failed(handle, "AtCoder Problems did not answer.");
        }
    }
}

/// <summary>A judge answered, and said no. Its comment is public data about a public handle.</summary>
public sealed class JudgeException(string message) : Exception(message);
