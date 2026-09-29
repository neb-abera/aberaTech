using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;

namespace aberaTech.Scheduling.Alerts;

/// <summary>One write the fake Google Calendar took: "patch" or "insert".</summary>
/// <param name="PopupMinutes">For an insert: the one popup reminder asked for.</param>
public sealed record FakeGoogleWrite(string Kind, string EventId, string? Summary, string? Description, int? PopupMinutes);

/// <summary>
/// Development and tests only: a Google Calendar in memory, answering the
/// three calls <see cref="GoogleAlertEvents"/> makes (events.list by
/// iCalUID, events.patch, events.insert), and a stored grant for it. The
/// real client runs on top. Only the network and the stored connection are
/// replaced, the way <see cref="FakeAlertServices"/> replaces Pushover's.
/// </summary>
public sealed class FakeGoogleCalendar : IAlertCalendarGrant
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, FakeEvent> _events = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<FakeGoogleWrite> _writes = new();
    private int _ids;

    /// <summary>The stored connection. Null is no calendar connected.</summary>
    public CalendarGrant? Grant { get; set; } = new("primary", "owner@example.test", true);

    /// <summary>Google refuses the stored refresh token.</summary>
    public bool TokenRefused { get; set; }

    /// <summary>Every call answers this status, with this reason word in the error body, until it is set back to null.</summary>
    public (HttpStatusCode Status, string? Reason)? Refusing { get; set; }

    /// <summary>Every call fails as if Google could not be reached.</summary>
    public bool Unreachable { get; set; }

    /// <summary>The writes Google took, oldest first.</summary>
    public IReadOnlyList<FakeGoogleWrite> Writes => [.. _writes];

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
    /// <param name="recurring">A series: events.list also returns one moved occurrence of it, as Google does.</param>
    public void Seed(string uid, string description = "", bool organisedHere = true, bool recurring = false)
    {
        lock (_lock)
        {
            _events[uid] = new FakeEvent(uid, $"seed{++_ids}", description, organisedHere, recurring, false, _ids);
        }
    }

    public string? Description(string uid)
    {
        lock (_lock) return _events.TryGetValue(uid, out var item) ? item.Description : null;
    }

    /// <summary>Forgets every event and write. The grant and the switches stay.</summary>
    public void Clear()
    {
        lock (_lock) _events.Clear();
        _writes.Clear();
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
        if (Refusing is { } refusal)
        {
            var reason = refusal.Reason is null ? "" : $",\"errors\":[{{\"reason\":\"{refusal.Reason}\"}}]";
            return Json(refusal.Status, $"{{\"error\":{{\"code\":{(int)refusal.Status}{reason}}}}}");
        }

        var path = request.RequestUri!.AbsolutePath;
        var events = path.IndexOf("/events", StringComparison.Ordinal);
        if (events < 0) return new HttpResponseMessage(HttpStatusCode.NotFound);
        var id = path[(events + "/events".Length)..].Trim('/');
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync());

        if (request.Method == HttpMethod.Get && id.Length == 0)
        {
            var uid = QueryHelpers.ParseQuery(request.RequestUri.Query).GetValueOrDefault("iCalUID").ToString();
            return Json(HttpStatusCode.OK, List(uid));
        }

        if (request.Method == HttpMethod.Patch && id.Length > 0)
        {
            var description = body?["description"]?.GetValue<string>();
            lock (_lock)
            {
                var item = _events.Values.FirstOrDefault(candidate => candidate.Id == Uri.UnescapeDataString(id));
                if (item is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
                if (!item.OrganisedHere)
                {
                    return Json(
                        HttpStatusCode.Forbidden, "{\"error\":{\"code\":403,\"errors\":[{\"reason\":\"forbiddenForNonOrganizer\"}]}}");
                }

                _events[item.Uid] = item with { Description = description ?? "" };
                _writes.Enqueue(new FakeGoogleWrite("patch", item.Uid, null, description, null));
                return Json(HttpStatusCode.OK, Item(_events[item.Uid]).ToJsonString());
            }
        }

        if (request.Method == HttpMethod.Post && id.Length == 0 && body is not null)
        {
            lock (_lock)
            {
                var n = ++_ids;
                var item = new FakeEvent($"created{n}@google.com", $"created{n}", body["description"]?.GetValue<string>() ?? "", true, false, true, n);
                _events[item.Uid] = item;
                var popup = body["reminders"]?["overrides"]?[0]?["minutes"]?.GetValue<int>();
                _writes.Enqueue(new FakeGoogleWrite(
                    "insert", item.Uid, body["summary"]?.GetValue<string>(), body["description"]?.GetValue<string>(), popup));
                return Json(HttpStatusCode.OK, Item(item).ToJsonString());
            }
        }

        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }

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

    private static JsonObject Item(FakeEvent item) => new()
    {
        ["id"] = item.Id,
        ["iCalUID"] = item.Uid,
        ["description"] = item.Description,
        ["organizer"] = item.OrganisedHere
            ? new JsonObject { ["email"] = "owner@example.test", ["self"] = true }
            : new JsonObject { ["email"] = "someone@example.test" }
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed record FakeEvent(
        string Uid, string Id, string Description, bool OrganisedHere, bool Recurring, bool Inserted, int Order);

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            answer(request);
    }
}
