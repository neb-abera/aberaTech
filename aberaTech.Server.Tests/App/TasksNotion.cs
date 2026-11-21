using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace aberaTech.Server.Tests.App;

/// <summary>
/// Notion's Tasks and Wish list items, in memory, as the API answers the
/// four calls the app's endpoints make: a data source query with a select
/// filter, a page, a new page, and a page update.
/// </summary>
internal sealed class TasksNotion : HttpMessageHandler
{
    public const string Tasks = "6a9730a6-1735-4b02-871c-0cf8f1231cc4";
    public const string WishList = "f6d72fbc-6038-42d3-93a8-dcfd0bc5160e";

    private readonly Dictionary<string, JsonObject> _pages = new();
    public List<string> Calls { get; } = [];

    /// <summary>The body of the last page create or update.</summary>
    public JsonObject? LastWrite { get; private set; }

    public string AddTask(string title, string? requestedBy, string status = "Not started", string? notes = null,
        string? gmailThread = null, string created = "2026-11-20T12:00:00.000Z", string edited = "2026-11-20T12:00:00.000Z")
    {
        var id = Guid.NewGuid().ToString();
        _pages[id] = new JsonObject
        {
            ["object"] = "page",
            ["id"] = id,
            ["created_time"] = created,
            ["last_edited_time"] = edited,
            ["parent"] = new JsonObject { ["type"] = "data_source_id", ["data_source_id"] = Tasks },
            ["properties"] = new JsonObject
            {
                ["Task name"] = RichText("title", title),
                ["Notes"] = RichText("rich_text", notes),
                ["Gmail thread"] = RichText("rich_text", gmailThread),
                ["Status"] = new JsonObject { ["type"] = "status", ["status"] = new JsonObject { ["name"] = status } },
                ["Requested by"] = Select(requestedBy),
                ["Due"] = new JsonObject { ["type"] = "date", ["date"] = null },
                ["Source"] = new JsonObject { ["type"] = "url", ["url"] = null }
            }
        };
        return id;
    }

    public string AddWish(string item, string status, string? buyWindow, double score, double? price = null)
    {
        var id = Guid.NewGuid().ToString();
        _pages[id] = new JsonObject
        {
            ["object"] = "page",
            ["id"] = id,
            ["parent"] = new JsonObject { ["type"] = "data_source_id", ["data_source_id"] = WishList },
            ["properties"] = new JsonObject
            {
                ["Item"] = RichText("title", item),
                ["Status"] = Select(status),
                ["Buy window"] = Select(buyWindow),
                ["Price"] = new JsonObject { ["type"] = "number", ["number"] = price },
                ["Score"] = new JsonObject { ["type"] = "formula", ["formula"] = new JsonObject { ["type"] = "number", ["number"] = score } }
            }
        };
        return id;
    }

    public JsonObject Page(string id) => _pages[id];

    public string? Status(string id) => _pages[id]["properties"]!["Status"]!["status"]!["name"]!.GetValue<string>();

    private static JsonObject RichText(string type, string? text) => new()
    {
        ["type"] = type,
        [type] = text is null ? new JsonArray() : new JsonArray(new JsonObject { ["plain_text"] = text, ["text"] = new JsonObject { ["content"] = text } })
    };

    private static JsonObject Select(string? name) => new()
    {
        ["type"] = "select",
        ["select"] = name is null ? null : new JsonObject { ["name"] = name }
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))?.AsObject();
        Calls.Add($"{request.Method} {path}");

        if (request.Method == HttpMethod.Post && path.StartsWith("/v1/data_sources/", StringComparison.Ordinal))
        {
            var source = path.Split('/')[3];
            var filter = body?["filter"];
            var results = _pages.Values
                .Where(p => Normalize(p["parent"]!["data_source_id"]!.GetValue<string>()) == Normalize(source))
                .Where(p => filter is null
                            || p["properties"]![filter["property"]!.GetValue<string>()]?["select"]?["name"]?.GetValue<string>()
                            == filter["select"]!["equals"]!.GetValue<string>())
                .Select(p => (JsonNode)p.DeepClone())
                .ToArray();
            return Json(new JsonObject { ["results"] = new JsonArray(results), ["has_more"] = false });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/v1/pages/", StringComparison.Ordinal))
        {
            var id = Find(path.Split('/')[3]);
            return id is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(_pages[id].DeepClone());
        }

        if (request.Method == HttpMethod.Post && path == "/v1/pages")
        {
            LastWrite = body;
            var properties = body!["properties"]!.AsObject();
            var id = AddTask(
                properties["Task name"]!["title"]![0]!["text"]!["content"]!.GetValue<string>(),
                properties["Requested by"]!["select"]!["name"]!.GetValue<string>());
            Apply(id, properties);
            return Json(_pages[id].DeepClone());
        }

        if (request.Method == HttpMethod.Patch && path.StartsWith("/v1/pages/", StringComparison.Ordinal))
        {
            LastWrite = body;
            var id = Find(path.Split('/')[3]);
            if (id is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            Apply(id, body!["properties"]!.AsObject());
            return Json(_pages[id].DeepClone());
        }

        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }

    private void Apply(string id, JsonObject properties)
    {
        var target = _pages[id]["properties"]!.AsObject();
        foreach (var (name, value) in properties)
        {
            if (value is null) continue;
            var copy = value.DeepClone().AsObject();
            // The API answers rich text with plain_text beside the content it was sent.
            foreach (var key in new[] { "title", "rich_text" })
            {
                if (copy[key] is JsonArray parts)
                {
                    copy["type"] = key;
                    foreach (var part in parts) part!["plain_text"] = part["text"]!["content"]!.GetValue<string>();
                }
            }

            if (copy.ContainsKey("status")) copy["type"] = "status";
            if (copy.ContainsKey("select")) copy["type"] = "select";
            if (copy.ContainsKey("date")) copy["type"] = "date";
            if (copy.ContainsKey("url")) copy["type"] = "url";
            target[name] = copy;
        }
    }

    private string? Find(string raw) => _pages.Keys.FirstOrDefault(k => Normalize(k) == Normalize(raw));

    private static string Normalize(string id) => id.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    private static HttpResponseMessage Json(JsonNode node) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json")
    };
}
