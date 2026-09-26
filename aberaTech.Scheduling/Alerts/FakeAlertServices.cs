using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>One message the fake Pushover took.</summary>
public sealed record FakeMessage(string Title, string Message, string Priority);

/// <summary>
/// Development only: a calendar and a Pushover that live in this process,
/// so `make up` and the browser suite can drive /alerts without either
/// account. The real <see cref="CalendarFeed"/> and <see cref="PushoverClient"/>
/// run on top; only the network under them is replaced. Program.cs wires
/// this in Development with Alerts:Fake set, and a test proves Production
/// never does.
/// </summary>
/// <remarks>
/// The calendar's events are placed relative to an anchor minute, so their
/// keys hold still across reads and a Skip sticks. The anchor is the
/// process start until <see cref="Reanchor"/> moves it to the present. The
/// browser suite calls it first, so its events are hours ahead of the test
/// however long the app has been up. Anchored at the start alone, the
/// standup began 3 h after it and the suite failed from then on.
/// </remarks>
public sealed class FakeAlertServices(IClock clock)
{
    private readonly Lock _lock = new();
    private readonly ConcurrentQueue<FakeMessage> _sent = new();
    private Instant _anchor = Minute(clock.GetCurrentInstant());

    public IReadOnlyList<FakeMessage> Sent => [.. _sent];

    /// <summary>Places the calendar's events relative to the present minute.</summary>
    public void Reanchor()
    {
        lock (_lock) _anchor = Minute(clock.GetCurrentInstant());
    }

    public string Calendar()
    {
        static string Utc(Instant instant) => instant.ToDateTimeUtc().ToString("yyyyMMdd'T'HHmmss'Z'");

        Instant anchor;
        lock (_lock) anchor = _anchor;
        var tomorrow = anchor.InUtc().Date.PlusDays(1);
        return string.Join("\r\n",
        [
            "BEGIN:VCALENDAR",
            "PRODID:-//aberaTech//Development calendar//EN",
            "VERSION:2.0",
            "X-WR-CALNAME:owner@example.test",
            "X-WR-TIMEZONE:America/New_York",
            "BEGIN:VEVENT",
            "UID:e2e-standup",
            $"DTSTART:{Utc(anchor + Duration.FromHours(3))}",
            $"DTEND:{Utc(anchor + Duration.FromHours(3.5))}",
            "SUMMARY:E2E standup",
            "LOCATION:Room 4",
            "BEGIN:VALARM",
            "ACTION:DISPLAY",
            "TRIGGER:-PT15M",
            "END:VALARM",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:e2e-review",
            $"DTSTART:{Utc(anchor + Duration.FromHours(5))}",
            $"DTEND:{Utc(anchor + Duration.FromHours(6))}",
            "SUMMARY:E2E review",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:e2e-holiday",
            $"DTSTART;VALUE=DATE:{tomorrow:yyyyMMdd}",
            $"DTEND;VALUE=DATE:{tomorrow.PlusDays(1):yyyyMMdd}",
            "SUMMARY:E2E holiday",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:e2e-cancelled",
            $"DTSTART:{Utc(anchor + Duration.FromHours(6))}",
            "SUMMARY:E2E cancelled",
            "STATUS:CANCELLED",
            "END:VEVENT",
            "END:VCALENDAR",
            ""
        ]);
    }

    public HttpMessageHandler CalendarHandler() => new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(Calendar(), System.Text.Encoding.UTF8, "text/calendar")
    }));

    public HttpMessageHandler PushoverHandler() => new Handler(async request =>
    {
        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync());
        _sent.Enqueue(new FakeMessage(form["title"].ToString(), form["message"].ToString(), form["priority"].ToString()));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":1,\"request\":\"development\"}", System.Text.Encoding.UTF8, "application/json")
        };
    });

    private static Instant Minute(Instant instant) => Instant.FromUnixTimeSeconds(instant.ToUnixTimeSeconds() / 60 * 60);

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            answer(request);
    }
}
