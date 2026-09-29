using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>One message the fake Pushover took.</summary>
/// <remarks>Retry, expire and sound are null when the request left them out.</remarks>
public sealed record FakeMessage(string Title, string Message, string Priority, string? Retry, string? Expire, string? Sound);

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
    private readonly ConcurrentQueue<string> _cancelled = new();
    private Instant _anchor = Minute(clock.GetCurrentInstant());
    private bool _failing;
    private Instant? _dueStart;
    private int _dueCount;
    private int _receipts;

    public IReadOnlyList<FakeMessage> Sent => [.. _sent];

    /// <summary>The receipts whose repeats were cancelled, oldest first.</summary>
    public IReadOnlyList<string> Cancelled => [.. _cancelled];

    /// <summary>
    /// Adds an alarm that is due now, until the next <see cref="Reanchor"/>:
    /// "E2E drill", marked #critical, starting in 30 minutes with a reminder
    /// an hour before. Each call is a new event, so an earlier engine's
    /// acknowledgement of the last one does not carry over.
    /// </summary>
    public void AddDue()
    {
        lock (_lock)
        {
            _dueStart = clock.GetCurrentInstant() + Duration.FromMinutes(30);
            _dueCount++;
        }
    }

    /// <summary>Places the calendar's events relative to the present minute, and ends <see cref="Fail"/>.</summary>
    public void Reanchor()
    {
        lock (_lock)
        {
            _anchor = Minute(clock.GetCurrentInstant());
            _failing = false;
            _dueStart = null;
        }
    }

    /// <summary>
    /// The calendar answers 404 until the next <see cref="Reanchor"/>, as
    /// Google does for a wrong secret address. For the page's failed-read
    /// banner in the browser suite.
    /// </summary>
    public void Fail()
    {
        lock (_lock) _failing = true;
    }

    public string Calendar()
    {
        static string Utc(Instant instant) => instant.ToDateTimeUtc().ToString("yyyyMMdd'T'HHmmss'Z'");

        Instant anchor;
        Instant? due;
        int dueCount;
        lock (_lock)
        {
            anchor = _anchor;
            due = _dueStart;
            dueCount = _dueCount;
        }

        var tomorrow = anchor.InUtc().Date.PlusDays(1);
        string[] drill = due is { } start
            ?
            [
                "BEGIN:VEVENT",
                $"UID:e2e-drill-{dueCount}",
                $"DTSTART:{Utc(start)}",
                $"DTEND:{Utc(start + Duration.FromMinutes(30))}",
                "SUMMARY:E2E drill #critical",
                "BEGIN:VALARM",
                "ACTION:DISPLAY",
                "TRIGGER:-PT1H",
                "END:VALARM",
                "END:VEVENT"
            ]
            : [];
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
            "SUMMARY:E2E review #critical",
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
            .. drill,
            "END:VCALENDAR",
            ""
        ]);
    }

    public HttpMessageHandler CalendarHandler() => new Handler(_ =>
    {
        bool failing;
        lock (_lock) failing = _failing;
        return Task.FromResult(failing
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Calendar(), System.Text.Encoding.UTF8, "text/calendar")
            });
    });

    /// <summary>The one sound the fake Pushover account has uploaded, beside the built-ins.</summary>
    public const string CustomSound = "aberaalarm";

    public HttpMessageHandler PushoverHandler() => new Handler(async request =>
    {
        if (request.RequestUri?.AbsolutePath == "/1/sounds.json")
        {
            // pushover.net/api#sounds: the built-ins and the account's own uploads.
            var sounds = PushoverSounds.BuiltIn.ToDictionary(sound => sound.Name, sound => sound.Description);
            sounds[CustomSound] = "Abera alarm (29.5 s)";
            return Json(System.Text.Json.JsonSerializer.Serialize(new { sounds, status = 1, request = "development" }));
        }

        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync());
        if (request.RequestUri?.AbsolutePath.StartsWith("/1/receipts/", StringComparison.Ordinal) == true)
        {
            // pushover.net/api/receipts#cancel: /1/receipts/{receipt}/cancel.json
            _cancelled.Enqueue(request.RequestUri.Segments[3].TrimEnd('/'));
            return Json("{\"status\":1,\"request\":\"development\"}");
        }

        static string? Field(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> form, string name) =>
            form.TryGetValue(name, out var value) ? value.ToString() : null;

        _sent.Enqueue(new FakeMessage(
            form["title"].ToString(),
            form["message"].ToString(),
            form["priority"].ToString(),
            Field(form, "retry"),
            Field(form, "expire"),
            Field(form, "sound")));

        // Pushover answers an emergency message with a receipt.
        return form["priority"].ToString() == "2"
            ? Json($"{{\"status\":1,\"request\":\"development\",\"receipt\":\"development{Interlocked.Increment(ref _receipts)}\"}}")
            : Json("{\"status\":1,\"request\":\"development\"}");
    });

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private static Instant Minute(Instant instant) => Instant.FromUnixTimeSeconds(instant.ToUnixTimeSeconds() / 60 * 60);

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            answer(request);
    }
}
