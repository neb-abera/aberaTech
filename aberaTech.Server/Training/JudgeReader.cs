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

/// <summary>A rated contest that has not started.</summary>
public sealed record UpcomingContest(string Name, DateTimeOffset Start, int DurationMinutes, string Url);

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
        Cached($"judges:codeforces:{handle}", () => ReadCodeforcesAsync(handle, cancellationToken));

    public Task<JudgeAnswer> AtCoderAsync(string handle, CancellationToken cancellationToken) =>
        Cached($"judges:atcoder:{handle}", () => ReadAtCoderAsync(handle, cancellationToken));

    public async Task<IReadOnlyList<UpcomingContest>> UpcomingAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue("judges:contests", out IReadOnlyList<UpcomingContest>? kept) && kept is not null) return kept;
        try
        {
            using var document = await CodeforcesCallAsync("contest.list?gym=false", cancellationToken);
            var contests = document.RootElement.GetProperty("result").EnumerateArray()
                .Where(contest => contest.GetProperty("phase").GetString() == "BEFORE")
                .Select(contest => new UpcomingContest(
                    contest.GetProperty("name").GetString() ?? "",
                    DateTimeOffset.FromUnixTimeSeconds(contest.GetProperty("startTimeSeconds").GetInt64()),
                    contest.GetProperty("durationSeconds").GetInt32() / 60,
                    $"https://codeforces.com/contests/{contest.GetProperty("id").GetInt32()}"))
                .OrderBy(contest => contest.Start)
                .Take(8)
                .ToList();
            cache.Set("judges:contests", (IReadOnlyList<UpcomingContest>)contests, CacheFor);
            return contests;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or JudgeException or TaskCanceledException)
        {
            return [];
        }
    }

    private async Task<JudgeAnswer> Cached(string key, Func<Task<JudgeAnswer>> read)
    {
        if (cache.TryGetValue(key, out JudgeAnswer? kept) && kept is not null) return kept;
        var answer = await read();
        // A failure is kept for a minute only, so the next load tries again soon.
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
