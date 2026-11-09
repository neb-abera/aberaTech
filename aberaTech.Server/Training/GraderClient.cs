using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace aberaTech.Server.Training;

/// <summary>
/// The Rust grader beside the site (grader/ in this repository). When
/// <c>Training:GraderUrl</c> is set, a drill goes to the grader's
/// <c>POST /grade</c>, and the grader sends each test to Compiler Explorer.
/// When it is not set, <see cref="DrillRunner"/> calls Compiler Explorer
/// from this process as before. The browser never calls the grader: it
/// takes internal traffic only.
/// </summary>
public sealed class GraderClient(IHttpClientFactory clients, IConfiguration configuration)
{
    public const string ClientName = "grader";

    /// <summary>Each test's time limit. The basics drills run in milliseconds.</summary>
    public const int TimeLimitMs = 2000;

    /// <summary>The grader's address, or null when the in-process path runs.</summary>
    public Uri? Address { get; } = Parse(configuration["Training:GraderUrl"]);

    public bool IsConfigured => Address is not null;

    private static Uri? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            return uri;
        throw new InvalidOperationException("Training:GraderUrl is not an http or https address.");
    }

    private sealed record GradeTest(string Stdin, string Expected, int TimeLimitMs);

    private sealed record GradeRequest(string Source, string Language, string Compiler, string Flags, IReadOnlyList<GradeTest> Tests);

    private sealed record GradeVerdict(string Verdict, string? Stdout, string? Message);

    private sealed record GradeResponse(bool Compiled, string? CompileError, IReadOnlyList<GradeVerdict>? Tests);

    private sealed record GradeError(string? Error);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public async Task<DrillResult> RunAsync(DrillRun run, CancellationToken cancellationToken)
    {
        var request = new GradeRequest(
            run.Source, "c++", DrillRunner.Compiler, DrillRunner.Flags,
            [.. run.Tests.Select(test => new GradeTest(test.Stdin, test.Stdout, TimeLimitMs))]);
        try
        {
            using var response = await clients.CreateClient(ClientName)
                .PostAsJsonAsync(new Uri(Address!, "grade"), request, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var refusal = await ReadAsync<GradeError>(response, cancellationToken);
                return new DrillResult(false, refusal?.Error ?? $"The grader answered {(int)response.StatusCode}.", []);
            }
            var answer = await ReadAsync<GradeResponse>(response, cancellationToken);
            if (answer?.Tests is null) return new DrillResult(false, "The grader sent an answer the site could not read.", []);
            if (!answer.Compiled) return new DrillResult(false, answer.CompileError ?? "It did not compile.", []);
            return new DrillResult(true, null, [.. answer.Tests.Select(Result)]);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new DrillResult(false, "The grader did not answer.", []);
        }
    }

    private static DrillTestResult Result(GradeVerdict test) => test.Verdict switch
    {
        "pass" => new DrillTestResult(true, test.Stdout ?? "", null),
        "wrong_answer" => new DrillTestResult(false, test.Stdout ?? "", null),
        _ => new DrillTestResult(false, test.Stdout ?? "", test.Message ?? "It did not run."),
    };

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return default;
        }
    }
}
