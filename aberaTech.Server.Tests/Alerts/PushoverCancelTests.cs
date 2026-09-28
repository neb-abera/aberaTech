using System.Net;
using aberaTech.Scheduling.Alerts;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// Cancelling an emergency message's repeats: one request with the app
/// token, a receipt that is not letters and digits never reaches a URL, and
/// every failure comes back as a short reason the caller logs.
/// </summary>
public sealed class PushoverCancelTests
{
    private static readonly AlertsOptions Options = new() { PushoverAppToken = "app-token-cancel-tests" };

    private static (PushoverClient Client, RecordingHandler Handler) Client(Func<HttpResponseMessage> answer)
    {
        var handler = new RecordingHandler(answer);
        return (new PushoverClient(new HttpClient(handler), Options), handler);
    }

    [Fact]
    public async Task A_cancel_posts_the_app_token_to_the_receipts_address()
    {
        var (client, handler) = Client(() => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1}"));

        var result = await client.CancelAsync("abc123", CancellationToken.None);

        Assert.True(result.Ok);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("https://api.pushover.net/1/receipts/abc123/cancel.json", sent.Url.ToString());
        Assert.Equal(new Dictionary<string, string> { ["token"] = "app-token-cancel-tests" }, sent.Form);
    }

    [Theory]
    [InlineData("../messages")]
    [InlineData("a b")]
    [InlineData("")]
    public async Task A_receipt_that_is_not_letters_and_digits_is_never_sent(string receipt)
    {
        var (client, handler) = Client(() => RecordingHandler.Text(HttpStatusCode.OK, "{}"));

        var result = await client.CancelAsync(receipt, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_refusal_a_lost_connection_and_a_timeout_each_come_back_as_a_reason()
    {
        var (refused, _) = Client(() => RecordingHandler.Text(HttpStatusCode.BadRequest, "{\"status\":0}"));
        var (offline, _) = Client(() => throw new HttpRequestException(HttpRequestError.ConnectionError));
        var (unknown, _) = Client(() => throw new HttpRequestException("boom"));
        var (slow, _) = Client(() => throw new TaskCanceledException());

        Assert.Equal("HTTP 400", (await refused.CancelAsync("abc", CancellationToken.None)).Error);
        Assert.Equal("ConnectionError", (await offline.CancelAsync("abc", CancellationToken.None)).Error);
        Assert.Equal(nameof(HttpRequestException), (await unknown.CancelAsync("abc", CancellationToken.None)).Error);
        Assert.Equal("Timeout", (await slow.CancelAsync("abc", CancellationToken.None)).Error);
    }
}
