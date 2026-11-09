using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using aberaTech.Scheduling.Admin;
using Microsoft.Extensions.Caching.Memory;

namespace aberaTech.Server.Training;

/// <summary>One problem of Mostafa Saad's supervision pool, as his sheet lists it.</summary>
/// <param name="Code">His code: "CF145-D1-D", "SPOJ METEORS", "Atcoder087-ARC-C".</param>
/// <param name="Level">His hand-set level, 1 to 10.</param>
/// <param name="Importance">5 for P5 (important) down to 3 for P3 (interesting).</param>
/// <param name="Url">The problem's page, where the code names a judge with one.</param>
/// <param name="Codeforces">The Codeforces problem id ("145D"), so a solve can be matched.</param>
/// <param name="Editorial">His sheet notes a solution or editorial.</param>
public sealed record PoolProblem(string Code, double Level, int Importance, string? Url, string? Codeforces, bool Editorial);

public sealed record PoolAnswer(bool Ok, string? Error, IReadOnlyList<PoolProblem> Problems, int Rows, DateTimeOffset ReadAt, string Source);

/// <summary>
/// Reads Mostafa Saad's supervision pool live from his public Google Sheet,
/// the General list sorted by level, as CSV. Keeps P5 to P3. The answer is
/// held in memory for at most <see cref="CacheFor"/> and never stored.
/// </summary>
public sealed partial class SaadPool(IHttpClientFactory clients, IMemoryCache cache, TimeProvider time)
{
    public const string ClientName = "saad-pool";

    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    /// <summary>The sheet's General list, sorted by level (tab gid 1918248438).</summary>
    public const string Sheet = "https://docs.google.com/spreadsheets/d/1-n9Fnvhsnvsqh-IerE_yyIshw5RUNer_7EjwF_GW-TA";

    public static readonly Uri Csv = new($"{Sheet}/export?format=csv&gid=1918248438");

    private const string CacheKey = "training:saad-pool";

    public async Task<PoolAnswer> ReadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PoolAnswer? cached) && cached is not null) return cached;
        PoolAnswer answer;
        try
        {
            using var response = await clients.CreateClient(ClientName).GetAsync(Csv, cancellationToken);
            if (!response.IsSuccessStatusCode)
                answer = Failed($"The sheet answered {(int)response.StatusCode}");
            else
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var (problems, rows) = Parse(text);
                answer = rows == 0
                    ? Failed("The sheet had no problem rows")
                    : new PoolAnswer(true, null, problems, rows, time.GetUtcNow(), Sheet);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            answer = Failed("The sheet did not answer");
        }
        // A failure is held for a minute, so a page left open does not hammer the sheet.
        cache.Set(CacheKey, answer, answer.Ok ? CacheFor : TimeSpan.FromMinutes(1));
        return answer;
    }

    private PoolAnswer Failed(string error) => new(false, error, [], 0, time.GetUtcNow(), Sheet);

    [GeneratedRegex(@"^p([345])\b", RegexOptions.IgnoreCase)]
    private static partial Regex Kept();

    [GeneratedRegex(@"^CF(\d+)-[A-Za-z0-9]+-([A-Z]\d?)$")]
    private static partial Regex CodeforcesCode();

    [GeneratedRegex(@"^AtCoder(\d+)-(AGC|ARC|ABC)-([A-Z])$", RegexOptions.IgnoreCase)]
    private static partial Regex AtCoderCode();

    [GeneratedRegex(@"^(SPOJ|CODECHEF)\s+([A-Z0-9_]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NamedCode();

    /// <summary>The rows kept (P5 to P3, with a level) and the number of problem rows read.</summary>
    public static (List<PoolProblem> Problems, int Rows) Parse(string csv)
    {
        var records = ReadCsv(csv);
        if (records.Count == 0) return ([], 0);
        var header = records[0];
        int Column(string name) => header.FindIndex(cell => cell.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
        int code = Column("Code"), level = Column("level"), sol = Column("Sol"), importance = Column("Importance");
        if (code < 0 || level < 0 || importance < 0) return ([], 0);
        var kept = new List<PoolProblem>();
        var rows = 0;
        foreach (var record in records.Skip(1))
        {
            string Cell(int i) => i >= 0 && i < record.Count ? record[i].Trim() : "";
            if (Cell(code) == "") continue;
            rows++;
            var match = Kept().Match(Cell(importance));
            if (!match.Success) continue;
            if (!double.TryParse(Cell(level), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
            var (url, codeforces) = Link(Cell(code));
            kept.Add(new PoolProblem(Cell(code), value, match.Groups[1].Value[0] - '0', url, codeforces, Cell(sol) != ""));
        }
        return (kept, rows);
    }

    /// <summary>A problem's page from its code, where the code names a judge with one.</summary>
    public static (string? Url, string? Codeforces) Link(string code)
    {
        if (CodeforcesCode().Match(code) is { Success: true } cf)
        {
            var contest = cf.Groups[1].Value;
            var index = cf.Groups[2].Value;
            var gym = contest.Length >= 6;
            return gym
                ? ($"https://codeforces.com/gym/{contest}/problem/{index}", null)
                : ($"https://codeforces.com/contest/{contest}/problem/{index}", contest + index);
        }
        if (AtCoderCode().Match(code) is { Success: true } ac)
        {
            var contest = $"{ac.Groups[2].Value.ToLowerInvariant()}{int.Parse(ac.Groups[1].Value, CultureInfo.InvariantCulture):000}";
            return ($"https://atcoder.jp/contests/{contest}/tasks/{contest}_{ac.Groups[3].Value.ToLowerInvariant()}", null);
        }
        if (NamedCode().Match(code) is { Success: true } named)
        {
            var name = named.Groups[2].Value.ToUpperInvariant();
            return named.Groups[1].Value.Equals("SPOJ", StringComparison.OrdinalIgnoreCase)
                ? ($"https://www.spoj.com/problems/{name}/", null)
                : ($"https://www.codechef.com/problems/{name}", null);
        }
        return (null, null);
    }

    /// <summary>RFC 4180: quoted fields may hold commas, quotes doubled, and line breaks.</summary>
    public static List<List<string>> ReadCsv(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case ',': record.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    record.Add(field.ToString()); field.Clear();
                    records.Add(record); record = [];
                    break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
        return records;
    }
}

/// <summary>/api/training/pool: the pool, owner only, like the rest of the owner's training page.</summary>
public static class PoolEndpoints
{
    public static IEndpointRouteBuilder MapTrainingPoolEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/training/pool", async (SaadPool pool, CancellationToken cancellationToken) =>
        {
            var answer = await pool.ReadAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = answer.Ok,
                error = answer.Error,
                rows = answer.Rows,
                readAt = answer.ReadAt,
                source = answer.Source,
                problems = answer.Problems.Select(problem => new
                {
                    code = problem.Code,
                    level = problem.Level,
                    importance = problem.Importance,
                    url = problem.Url,
                    codeforces = problem.Codeforces,
                    editorial = problem.Editorial,
                }),
            });
        })
        .RequireAuthorization(AdminAuth.PolicyName)
        .WithTags("Training");
        return routes;
    }
}
