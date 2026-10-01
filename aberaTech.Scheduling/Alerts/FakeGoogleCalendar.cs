using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One write the fake Google Calendar took. Kind is "insert", "patch" (a
/// description alone, for a type change), "patch-master", "patch-instance",
/// "delete-master" or "delete-instance".
/// </summary>
/// <param name="PopupMinutes">The one popup reminder asked for. Null when the body set none.</param>
/// <param name="Target">For an edit or a deletion: the Google id it went to, the master's or an instance's.</param>
/// <param name="Start">For an edit: the start as sent, dateTime with its offset.</param>
/// <param name="End">For an edit: the end as sent.</param>
/// <param name="TimeZone">For an edit: the zone sent with the start, or null.</param>
/// <param name="Body">For a patch: the JSON body as sent, so a test can see what it left out.</param>
/// <param name="Recurrence">For a patch: the recurrence lines sent, or null when the body sent none.</param>
public sealed record FakeGoogleWrite(
    string Kind,
    string EventId,
    string? Summary,
    string? Description,
    int? PopupMinutes,
    string? Target = null,
    string? Start = null,
    string? End = null,
    string? TimeZone = null,
    string? Body = null,
    IReadOnlyList<string>? Recurrence = null);

/// <summary>One events.instances call: the master's id and the window asked for.</summary>
public sealed record FakeGoogleLookup(string MasterId, string TimeMin, string TimeMax);

/// <summary>
/// Development and tests only: a Google Calendar in memory, answering the
/// calls <see cref="GoogleAlertEvents"/> makes (events.list by iCalUID,
/// events.instances, events.patch, events.insert and events.delete), and a
/// stored grant for it. The real client runs on top. Only the network and
/// the stored connection are replaced, the way <see cref="FakeAlertServices"/>
/// replaces Pushover's.
/// </summary>
/// <remarks>
/// A recurring event repeats daily on its zone's wall clock, or by the
/// recurrence lines it was seeded or patched with, expanded by Ical.Net. Its instances
/// have the ids Google gives them, the master's id and the original start in
/// UTC. A patched instance moves on its own. A deleted one is cancelled and
/// no longer listed.
/// </remarks>
public sealed class FakeGoogleCalendar : IAlertCalendarGrant
{
    /// <summary>A seeded event with no start of its own starts here: 09:00 New York time on 28 October 2026.</summary>
    public static readonly Instant DefaultStart = Instant.FromUtc(2026, 10, 28, 13, 0);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, FakeEvent> _events = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<FakeGoogleWrite> _writes = new();
    private readonly ConcurrentQueue<FakeGoogleLookup> _lookups = new();
    private int _ids;

    /// <summary>The stored connection. Null is no calendar connected.</summary>
    public CalendarGrant? Grant { get; set; } = new("primary", "owner@example.test", true);

    /// <summary>Google refuses the stored refresh token.</summary>
    public bool TokenRefused { get; set; }

    /// <summary>Every call answers this status, with this reason word in the error body, until it is set back to null.</summary>
    public (HttpStatusCode Status, string? Reason)? Refusing { get; set; }

    /// <summary>Every write (patch, insert, delete) answers this, while reads still work, until it is set back to null.</summary>
    public (HttpStatusCode Status, string? Reason)? RefusingWrites { get; set; }

    /// <summary>Every call fails as if Google could not be reached.</summary>
    public bool Unreachable { get; set; }

    /// <summary>The writes Google took, oldest first.</summary>
    public IReadOnlyList<FakeGoogleWrite> Writes => [.. _writes];

    /// <summary>The events.instances calls, oldest first.</summary>
    public IReadOnlyList<FakeGoogleLookup> Lookups => [.. _lookups];

    /// <summary>How many calls reached the fake, of any kind.</summary>
    public int Calls { get; private set; }

    /// <summary>The UIDs of the events created through the API, oldest first.</summary>
    public IReadOnlyList<string> Inserted
    {
        get
        {
            lock (_lock) return [.. _events.Values.Where(item => item.Inserted).OrderBy(item => item.Order).Select(item => item.Uid)];
        }
    }

    /// <summary>An event the calendar already has, as the feed shows it.</summary>
    /// <param name="organisedHere">False for an invitation from someone else, which this calendar cannot edit.</param>
    /// <param name="recurring">
    /// A series, daily for <paramref name="count"/> days. events.list also
    /// returns one moved occurrence of it, as Google does.
    /// </param>
    /// <param name="start">The first start. <see cref="DefaultStart"/> when null.</param>
    /// <param name="minutes">The length.</param>
    /// <param name="timeZone">The zone Google names on its start and end.</param>
    /// <param name="recurrence">
    /// The series' recurrence lines, for a rule other than daily. Null is
    /// daily for <paramref name="count"/> days.
    /// </param>
    public void Seed(
        string uid,
        string description = "",
        bool organisedHere = true,
        bool recurring = false,
        Instant? start = null,
        int minutes = 30,
        string timeZone = "America/New_York",
        int count = 7,
        string? summary = null,
        IReadOnlyList<string>? recurrence = null)
    {
        lock (_lock)
        {
            var begins = start ?? DefaultStart;
            _events[uid] = new FakeEvent
            {
                Uid = uid,
                Id = $"seed{++_ids}",
                Description = description,
                OrganisedHere = organisedHere,
                Recurring = recurring,
                Order = _ids,
                Summary = summary,
                Start = begins,
                End = begins + Duration.FromMinutes(minutes),
                TimeZone = timeZone,
                Count = count,
                Recurrence = recurrence is null ? null : [.. recurrence]
            };
        }
    }

    public string? Description(string uid)
    {
        lock (_lock) return _events.TryGetValue(uid, out var item) ? item.Description : null;
    }

    /// <summary>The event's master as the fake holds it now: its summary, start, end and popup. Null when it is gone.</summary>
    public (string? Summary, Instant Start, Instant End, string? Location, int? Popup)? Master(string uid)
    {
        lock (_lock)
        {
            return _events.TryGetValue(uid, out var item) ? (item.Summary, item.Start, item.End, item.Location, item.Popup) : null;
        }
    }

    /// <summary>A series' recurrence lines as the fake holds them now. Null when it is gone.</summary>
    public IReadOnlyList<string>? Recurrence(string uid)
    {
        lock (_lock) return _events.TryGetValue(uid, out var item) ? Rules(item) : null;
    }

    /// <summary>The ids of a series' instances that are cancelled.</summary>
    public IReadOnlyList<string> Cancelled(string uid)
    {
        lock (_lock)
        {
            return _events.TryGetValue(uid, out var item)
                ? [.. item.Overrides.Where(pair => pair.Value.Cancelled).Select(pair => InstanceId(item, pair.Key)).Order(StringComparer.Ordinal)]
                : [];
        }
    }

    /// <summary>Forgets every event and write. The grant and the switches stay.</summary>
    public void Clear()
    {
        lock (_lock) _events.Clear();
        _writes.Clear();
        _lookups.Clear();
    }

    /// <summary>Forgets the events created through the API and the writes, and keeps the seeded ones.</summary>
    public IReadOnlyList<string> ForgetInserted()
    {
        List<string> gone;
        lock (_lock)
        {
            gone = [.. _events.Values.Where(item => item.Inserted).Select(item => item.Uid)];
            foreach (var uid in gone) _events.Remove(uid);
        }

        _writes.Clear();
        _lookups.Clear();
        return gone;
    }

    public Task<CalendarGrant?> CurrentAsync(CancellationToken cancellationToken) => Task.FromResult(Grant);

    public Task<string?> AccessTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult(TokenRefused ? null : "development-token");

    public HttpMessageHandler Handler() => new FakeHandler(AnswerAsync);

    private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request)
    {
        lock (_lock) Calls++;
        if (Unreachable) throw new HttpRequestException("development: unreachable");
        if (Refusing is { } refusal) return Refused(refusal);
        if (request.Method != HttpMethod.Get && RefusingWrites is { } writes) return Refused(writes);

        var path = request.RequestUri!.AbsolutePath;
        var events = path.IndexOf("/events", StringComparison.Ordinal);
        if (events < 0) return new HttpResponseMessage(HttpStatusCode.NotFound);
        var rest = path[(events + "/events".Length)..].Trim('/');
        var id = Uri.UnescapeDataString(rest.EndsWith("/instances", StringComparison.Ordinal) ? rest[..^"/instances".Length] : rest);
        var text = request.Content is null ? null : await request.Content.ReadAsStringAsync();
        var body = text is null ? null : JsonNode.Parse(text);
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);

        if (request.Method == HttpMethod.Get && id.Length == 0)
        {
            return Json(HttpStatusCode.OK, List(query.GetValueOrDefault("iCalUID").ToString()));
        }

        if (request.Method == HttpMethod.Get && rest.EndsWith("/instances", StringComparison.Ordinal))
        {
            var from = query.GetValueOrDefault("timeMin").ToString();
            var until = query.GetValueOrDefault("timeMax").ToString();
            _lookups.Enqueue(new FakeGoogleLookup(id, from, until));
            return Instances(id, from, until);
        }

        if (request.Method == HttpMethod.Patch && id.Length > 0 && body is not null) return Patch(id, body, text!);

        if (request.Method == HttpMethod.Delete && id.Length > 0) return Delete(id);

        if (request.Method == HttpMethod.Post && id.Length == 0 && body is not null)
        {
            lock (_lock)
            {
                var n = ++_ids;
                var start = Parse(body["start"]?["dateTime"]?.GetValue<string>()) ?? DefaultStart;
                var item = new FakeEvent
                {
                    Uid = $"created{n}@google.com",
                    Id = $"created{n}",
                    Description = body["description"]?.GetValue<string>() ?? "",
                    OrganisedHere = true,
                    Inserted = true,
                    Order = n,
                    Summary = body["summary"]?.GetValue<string>(),
                    Location = body["location"]?.GetValue<string>(),
                    Start = start,
                    End = Parse(body["end"]?["dateTime"]?.GetValue<string>()) ?? start,
                    Popup = body["reminders"]?["overrides"]?[0]?["minutes"]?.GetValue<int>()
                };
                _events[item.Uid] = item;
                _writes.Enqueue(new FakeGoogleWrite(
                    "insert", item.Uid, item.Summary, body["description"]?.GetValue<string>(), item.Popup));
                return Json(HttpStatusCode.OK, Item(item).ToJsonString());
            }
        }

        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }

    private HttpResponseMessage Patch(string id, JsonNode body, string text)
    {
        lock (_lock)
        {
            var (item, instance) = Find(id);
            if (item is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (!item.OrganisedHere) return NotOrganiser();

            var description = body["description"]?.GetValue<string>();
            var summary = body["summary"]?.GetValue<string>();
            var start = body["start"]?["dateTime"]?.GetValue<string>();
            var end = body["end"]?["dateTime"]?.GetValue<string>();
            var zone = body["start"]?["timeZone"]?.GetValue<string>();
            var popup = body["reminders"]?["overrides"]?[0]?["minutes"]?.GetValue<int>();
            var location = body["location"]?.GetValue<string>();
            IReadOnlyList<string>? recurrence = body["recurrence"] is JsonArray rules
                ? [.. rules.Select(rule => rule!.GetValue<string>())]
                : null;

            // A description alone is a type change: the kind every earlier
            // test and the browser suite read as "patch".
            var descriptionOnly = body is JsonObject { Count: 1 } && description is not null;
            if (instance is { } original)
            {
                var own = item.Overrides.GetValueOrDefault(original) ?? Fresh(item, original);
                item.Overrides[original] = own with
                {
                    Start = Parse(start) ?? own.Start,
                    End = Parse(end) ?? own.End,
                    Summary = summary ?? own.Summary
                };
            }
            else
            {
                if (description is not null) item.Description = description;
                if (summary is not null) item.Summary = summary;
                if (Parse(start) is { } begins) item.Start = begins;
                if (Parse(end) is { } ends) item.End = ends;
                if (zone is not null) item.TimeZone = zone;
                if (location is not null) item.Location = location.Length == 0 ? null : location;
                if (popup is not null) item.Popup = popup;
                if (recurrence is not null && item.Recurring) item.Recurrence = [.. recurrence];
            }

            _writes.Enqueue(descriptionOnly
                ? new FakeGoogleWrite("patch", item.Uid, null, description, null)
                : new FakeGoogleWrite(
                    instance is null ? "patch-master" : "patch-instance", item.Uid, summary, description, popup, id, start, end, zone, text,
                    recurrence));
            return Json(HttpStatusCode.OK, Item(item).ToJsonString());
        }
    }

    private HttpResponseMessage Delete(string id)
    {
        lock (_lock)
        {
            var (item, instance) = Find(id);
            if (item is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (!item.OrganisedHere) return NotOrganiser();

            if (instance is { } original)
            {
                var own = item.Overrides.GetValueOrDefault(original) ?? Fresh(item, original);
                item.Overrides[original] = own with { Cancelled = true };
            }
            else
            {
                _events.Remove(item.Uid);
            }

            _writes.Enqueue(new FakeGoogleWrite(instance is null ? "delete-master" : "delete-instance", item.Uid, null, null, null, id));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    /// <summary>The event a Google id names, and for an instance id, the instance's original start.</summary>
    private (FakeEvent? Event, Instant? Instance) Find(string id)
    {
        var underscore = id.LastIndexOf('_');
        if (underscore > 0
            && InstantPattern.Create("uuuuMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture)
                .Parse(id[(underscore + 1)..]) is { Success: true } parsed)
        {
            var master = _events.Values.FirstOrDefault(candidate => candidate.Id == id[..underscore] && candidate.Recurring);
            return master is null ? (null, null) : (master, parsed.Value);
        }

        return (_events.Values.FirstOrDefault(candidate => candidate.Id == id), null);
    }

    /// <summary>An instance as Google first lists it: at its original start, with the series' length.</summary>
    private static FakeInstance Fresh(FakeEvent item, Instant original) =>
        new(original, original) { End = original + (item.End - item.Start) };

    private HttpResponseMessage Instances(string masterId, string timeMin, string timeMax)
    {
        lock (_lock)
        {
            var item = _events.Values.FirstOrDefault(candidate => candidate.Id == masterId);
            if (item is null) return new HttpResponseMessage(HttpStatusCode.NotFound);

            var from = Parse(timeMin) ?? Instant.MinValue;
            var until = Parse(timeMax) ?? Instant.MaxValue;
            var length = item.End - item.Start;
            var items = new JsonArray();
            foreach (var original in Originals(item))
            {
                var own = item.Overrides.GetValueOrDefault(original);
                if (own is { Cancelled: true }) continue;
                var start = own?.Start ?? original;
                var end = own?.End ?? original + length;
                if (end < from || start >= until) continue;

                items.Add(new JsonObject
                {
                    ["id"] = InstanceId(item, original),
                    ["iCalUID"] = item.Uid,
                    ["recurringEventId"] = item.Id,
                    ["originalStartTime"] = new JsonObject { ["dateTime"] = Format(original), ["timeZone"] = item.TimeZone },
                    ["start"] = new JsonObject { ["dateTime"] = Format(start), ["timeZone"] = item.TimeZone },
                    ["end"] = new JsonObject { ["dateTime"] = Format(end), ["timeZone"] = item.TimeZone },
                    ["summary"] = own?.Summary ?? item.Summary,
                    ["organizer"] = new JsonObject { ["self"] = true }
                });
            }

            return Json(HttpStatusCode.OK, new JsonObject { ["items"] = items }.ToJsonString());
        }
    }

    /// <summary>The original starts of a series: daily on its zone's wall clock, or as its own lines expand.</summary>
    private static IEnumerable<Instant> Originals(FakeEvent item)
    {
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(item.TimeZone ?? "UTC") ?? DateTimeZone.Utc;
        var local = item.Start.InZone(zone).LocalDateTime;
        if (item.Recurring && item.Recurrence is { } lines)
        {
            foreach (var start in Expand(item.Start, zone, lines, MaxInstances)) yield return start;
            yield break;
        }

        for (var day = 0; day < (item.Recurring ? item.Count : 1); day++)
        {
            yield return zone.AtLeniently(local.PlusDays(day)).ToInstant();
        }
    }

    /// <summary>How many instances of a series with its own lines the fake lists at most.</summary>
    private const int MaxInstances = 100;

    /// <summary>
    /// The first starts of a series, at most <paramref name="limit"/>, as
    /// Ical.Net expands its lines from <paramref name="start"/> on the
    /// zone's wall clock.
    /// </summary>
    public static IReadOnlyList<Instant> Expand(Instant start, DateTimeZone zone, IEnumerable<string> recurrence, int limit)
    {
        var local = start.InZone(zone).LocalDateTime;
        var ics = string.Join("\r\n",
        [
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//aberaTech//Fake Google Calendar//EN",
            "BEGIN:VEVENT",
            "UID:expand",
            $"DTSTART;TZID={zone.Id}:{local:yyyyMMdd'T'HHmmss}",
            .. recurrence,
            "END:VEVENT",
            "END:VCALENDAR",
            ""
        ]);
        var calendar = Ical.Net.Calendar.Load(ics)!;
        return
        [
            .. calendar.GetOccurrences<Ical.Net.CalendarComponents.CalendarEvent>(
                    new Ical.Net.DataTypes.CalDateTime(start.ToDateTimeUtc(), "UTC"))
                .Take(limit)
                .Select(occurrence => Instant.FromDateTimeUtc(DateTime.SpecifyKind(occurrence.Period.StartTime.AsUtc, DateTimeKind.Utc)))
        ];
    }

    private static string InstanceId(FakeEvent item, Instant original) =>
        $"{item.Id}_{original.ToDateTimeUtc():yyyyMMdd'T'HHmmss'Z'}";

    private string List(string uid)
    {
        lock (_lock)
        {
            var items = new JsonArray();
            if (_events.TryGetValue(uid, out var item))
            {
                // Google lists a moved occurrence of a series as its own item,
                // first here, so a client that takes the first item is wrong.
                if (item.Recurring)
                {
                    items.Add(new JsonObject
                    {
                        ["id"] = $"{item.Id}_20261029T130000Z",
                        ["iCalUID"] = item.Uid,
                        ["recurringEventId"] = item.Id,
                        ["description"] = "one moved occurrence",
                        ["organizer"] = new JsonObject { ["self"] = true }
                    });
                }

                items.Add(Item(item));
            }

            return new JsonObject { ["items"] = items }.ToJsonString();
        }
    }

    private static JsonObject Item(FakeEvent item)
    {
        var json = new JsonObject
        {
            ["id"] = item.Id,
            ["iCalUID"] = item.Uid,
            ["summary"] = item.Summary,
            ["description"] = item.Description,
            ["start"] = new JsonObject { ["dateTime"] = Format(item.Start), ["timeZone"] = item.TimeZone },
            ["end"] = new JsonObject { ["dateTime"] = Format(item.End), ["timeZone"] = item.TimeZone },
            ["organizer"] = item.OrganisedHere
                ? new JsonObject { ["email"] = "owner@example.test", ["self"] = true }
                : new JsonObject { ["email"] = "someone@example.test" }
        };
        if (Rules(item) is { } rules) json["recurrence"] = new JsonArray([.. rules.Select(rule => JsonValue.Create(rule))]);
        if (item.Location is not null) json["location"] = item.Location;
        return json;
    }

    /// <summary>A series' lines: its own, or daily for its count. Null for an event that does not repeat.</summary>
    private static IReadOnlyList<string>? Rules(FakeEvent item) =>
        !item.Recurring ? null : item.Recurrence ?? [$"RRULE:FREQ=DAILY;COUNT={item.Count}"];

    private static string Format(Instant at) => InstantPattern.ExtendedIso.Format(at);

    private static Instant? Parse(string? text) =>
        text is not null && DateTimeOffset.TryParse(
            text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? Instant.FromDateTimeOffset(parsed)
            : null;

    private static HttpResponseMessage Refused((HttpStatusCode Status, string? Reason) refusal)
    {
        var reason = refusal.Reason is null ? "" : $",\"errors\":[{{\"reason\":\"{refusal.Reason}\"}}]";
        return Json(refusal.Status, $"{{\"error\":{{\"code\":{(int)refusal.Status}{reason}}}}}");
    }

    private static HttpResponseMessage NotOrganiser() =>
        Json(HttpStatusCode.Forbidden, "{\"error\":{\"code\":403,\"errors\":[{\"reason\":\"forbiddenForNonOrganizer\"}]}}");

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class FakeEvent
    {
        public required string Uid { get; init; }

        public required string Id { get; init; }

        public string Description { get; set; } = "";

        public bool OrganisedHere { get; init; }

        public bool Recurring { get; init; }

        public bool Inserted { get; init; }

        public int Order { get; init; }

        public string? Summary { get; set; }

        public string? Location { get; set; }

        public Instant Start { get; set; }

        public Instant End { get; set; }

        public string? TimeZone { get; set; }

        public int Count { get; init; } = 1;

        public int? Popup { get; set; }

        /// <summary>The series' own lines. Null is daily for <see cref="Count"/> days.</summary>
        public List<string>? Recurrence { get; set; }

        /// <summary>Patched or cancelled instances, by original start.</summary>
        public Dictionary<Instant, FakeInstance> Overrides { get; } = [];
    }

    private sealed record FakeInstance(Instant Original, Instant Start)
    {
        public Instant? End { get; init; }

        public string? Summary { get; init; }

        public bool Cancelled { get; init; }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            answer(request);
    }
}
