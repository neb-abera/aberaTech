using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace aberaTech.Server.Training;

/// <summary>One test of a drill: what goes in on stdin, and what must come out.</summary>
public sealed record DrillTest(string Stdin, string Stdout);

/// <summary>A drill to run: the owner's C++ source and the drill's tests.</summary>
public sealed record DrillRun(string Source, IReadOnlyList<DrillTest> Tests);

/// <summary>What one test did.</summary>
public sealed record DrillTestResult(bool Passed, string Actual, string? Error);

/// <summary>The whole run. <c>Compiled</c> false means no test ran, and <c>Error</c> says why.</summary>
public sealed record DrillResult(bool Compiled, string? Error, IReadOnlyList<DrillTestResult> Tests);

/// <summary>
/// Runs a drill's C++ on Compiler Explorer and checks its output. No code
/// runs on this server: Compiler Explorer's public API compiles and
/// executes it (github.com/compiler-explorer/compiler-explorer,
/// docs/API.md), and this compares what came back with each test.
/// </summary>
/// <remarks>
/// One request per test. Compiler Explorer caches the build, so only the
/// first compiles. A 429 is waited out as Retry-After says, or 2 then 4
/// seconds, at most <see cref="Attempts"/> times.
/// </remarks>
public sealed class DrillRunner(IHttpClientFactory clients, TimeProvider time)
{
    public const string ClientName = "compiler-explorer";

    /// <summary>gcc 16.2 for x86-64, from /api/compilers/c++ on 9 Nov 2026.</summary>
    public const string Compiler = "g162";

    public const string Flags = "-O2 -std=c++23";

    public static readonly Uri Api = new("https://godbolt.org/api/");

    public const int MaxSourceBytes = 16 * 1024;
    public const int MaxTests = 10;
    public const int MaxStdinBytes = 4 * 1024;
    public const int Attempts = 3;

    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    public static string? Invalid(DrillRun run)
    {
        if (string.IsNullOrWhiteSpace(run.Source)) return "The source is empty.";
        if (Encoding.UTF8.GetByteCount(run.Source) > MaxSourceBytes) return $"The source is over {MaxSourceBytes / 1024} KB.";
        if (run.Tests is null || run.Tests.Count is 0 or > MaxTests) return $"A drill has 1 to {MaxTests} tests.";
        if (run.Tests.Any(test => test is null || test.Stdin is null || test.Stdout is null
                                  || Encoding.UTF8.GetByteCount(test.Stdin) > MaxStdinBytes
                                  || Encoding.UTF8.GetByteCount(test.Stdout) > MaxStdinBytes))
            return $"Each test's input and output is at most {MaxStdinBytes / 1024} KB.";
        return null;
    }

    /// <summary>Lines compared with trailing spaces and trailing blank lines removed, as judges do.</summary>
    public static string Normalize(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd())).TrimEnd('\n');

    public async Task<DrillResult> RunAsync(DrillRun run, CancellationToken cancellationToken)
    {
        var results = new List<DrillTestResult>();
        foreach (var test in run.Tests)
        {
            JsonElement answer;
            try
            {
                answer = await CompileAsync(run.Source, test.Stdin, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or DrillServiceException)
            {
                var message = exception is DrillServiceException ? exception.Message : "Compiler Explorer did not answer.";
                return new DrillResult(false, message, results);
            }

            if (answer.TryGetProperty("buildResult", out var build)
                && build.TryGetProperty("code", out var buildCode) && buildCode.GetInt32() != 0)
            {
                return new DrillResult(false, Lines(build, "stderr") is { Length: > 0 } errors ? errors : "It did not compile.", results);
            }

            var didExecute = answer.TryGetProperty("didExecute", out var executed) && executed.GetBoolean();
            var actual = Lines(answer, "stdout");
            string? error = null;
            if (!didExecute) error = "It did not run.";
            else if (answer.TryGetProperty("timedOut", out var timedOut) && timedOut.ValueKind == JsonValueKind.True) error = "It ran out of time.";
            else if (answer.TryGetProperty("code", out var code) && code.GetInt32() != 0)
                error = $"It exited with code {code.GetInt32()}. {Lines(answer, "stderr")}".Trim();

            results.Add(new DrillTestResult(error is null && Normalize(actual) == Normalize(test.Stdout), actual, error));
        }
        return new DrillResult(true, null, results);
    }

    private static string Lines(JsonElement element, string name) =>
        element.TryGetProperty(name, out var lines) && lines.ValueKind == JsonValueKind.Array
            ? string.Join('\n', lines.EnumerateArray()
                .Select(line => line.TryGetProperty("text", out var text) ? text.GetString() ?? "" : ""))
            : "";

    private async Task<JsonElement> CompileAsync(string source, string stdin, CancellationToken cancellationToken)
    {
        var body = new
        {
            source,
            compiler = Compiler,
            options = new
            {
                userArguments = Flags,
                executeParameters = new { args = Array.Empty<string>(), stdin },
                compilerOptions = new { executorRequest = true },
                filters = new { execute = true },
                tools = Array.Empty<object>(),
                libraries = Array.Empty<object>(),
            },
            lang = "c++",
            allowStoreCodeDebug = false,
        };

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Api, $"compiler/{Compiler}/compile"))
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt >= Attempts) throw new DrillServiceException("Compiler Explorer is busy. Try again in a minute.");
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * attempt);
                if (wait > MaxWait) throw new DrillServiceException("Compiler Explorer is busy. Try again in a minute.");
                await Task.Delay(wait, time, cancellationToken);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new DrillServiceException($"Compiler Explorer answered {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.Clone();
        }
    }
}

/// <summary>Compiler Explorer answered, and not with a run.</summary>
public sealed class DrillServiceException(string message) : Exception(message);
