using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace aberaTech.Server.Training;

/// <summary>
/// Compiler Explorer in memory, for `make e2e` (Training:Fake, Development
/// only) and the tests. It runs nothing. A source with <c>#error</c> fails
/// to build. A source with the comment <c>// fake: sum</c> prints the sum of
/// the integers on stdin. Any other source prints nothing. Set
/// <see cref="Busy"/> to answer 429 that many times first.
/// </summary>
public sealed partial class FakeCompilerExplorer : HttpMessageHandler
{
    public List<string> Bodies { get; } = [];

    public int Busy { get; set; }

    [GeneratedRegex(@"-?\d+")]
    private static partial Regex Integers();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var text = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Bodies) Bodies.Add(text);
        if (Busy > 0)
        {
            Busy--;
            var busy = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return busy;
        }

        using var document = JsonDocument.Parse(text);
        var source = document.RootElement.GetProperty("source").GetString() ?? "";
        var stdin = document.RootElement.GetProperty("options").GetProperty("executeParameters").GetProperty("stdin").GetString() ?? "";

        string body;
        if (source.Contains("#error", StringComparison.Ordinal))
            body = """{"code":-1,"didExecute":false,"buildResult":{"code":1,"stderr":[{"text":"<source>:1:2: error: #error"}]},"stdout":[],"stderr":[]}""";
        else
        {
            var output = source.Contains("// fake: sum", StringComparison.Ordinal)
                ? Integers().Matches(stdin).Sum(match => long.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "";
            body = JsonSerializer.Serialize(new
            {
                code = 0,
                didExecute = true,
                buildResult = new { code = 0, stderr = Array.Empty<object>() },
                stdout = output.Length == 0 ? Array.Empty<object>() : new object[] { new { text = output } },
                stderr = Array.Empty<object>(),
            });
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
