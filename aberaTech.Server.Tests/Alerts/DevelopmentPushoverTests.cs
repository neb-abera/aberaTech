using System.Text.Json;
using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The development Pushover behind `make up` and the browser suite. The app
/// restarts and its database volume stays, so a receipt from an earlier start
/// is still stored, acknowledged, beside the new ones. Numbered from 1 at each
/// start, the second run's first receipt was the first run's, and Pushover's
/// callback for an alarm nobody acknowledged answered 200 where it is 403.
/// </summary>
public sealed class DevelopmentPushoverTests
{
    [Fact]
    public async Task An_emergency_message_gets_a_receipt_no_earlier_start_gave()
    {
        var first = await ReceiptsAsync(new FakeAlertServices(SystemClock.Instance), 3);
        var restarted = await ReceiptsAsync(new FakeAlertServices(SystemClock.Instance), 3);

        Assert.All(first.Concat(restarted), receipt => Assert.True(PushoverClient.IsReceipt(receipt), receipt));
        Assert.Equal(6, first.Concat(restarted).Distinct(StringComparer.Ordinal).Count());
    }

    private static async Task<List<string>> ReceiptsAsync(FakeAlertServices fake, int count)
    {
        using var client = new HttpClient(fake.PushoverHandler()) { BaseAddress = new Uri("https://api.pushover.net") };
        var receipts = new List<string>();
        for (var sent = 0; sent < count; sent++)
        {
            using var response = await client.PostAsync("/1/messages.json", new FormUrlEncodedContent(
            [
                new("title", "E2E drill"),
                new("message", "Now"),
                new("priority", "2"),
                new("retry", "30"),
                new("expire", "10800")
            ]));
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            receipts.Add(body.RootElement.GetProperty("receipt").GetString()!);
        }

        Assert.Equal(receipts, fake.Sent.Select(message => message.Receipt));
        return receipts;
    }
}
