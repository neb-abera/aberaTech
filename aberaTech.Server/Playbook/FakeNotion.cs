using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace aberaTech.Server.Playbook;

/// <summary>
/// Notion's API in memory, answering the calls <see cref="NotionClient"/>
/// makes the way Notion does: pages, blocks, children in pages of
/// <see cref="PageSize"/> with <c>has_more</c>, databases with data
/// sources, data source queries, and the signed file addresses.
/// </summary>
/// <remarks>
/// Development uses <see cref="Sample"/> under `make up` and `make e2e`
/// (Notion__Fake). The server tests build their own. Everything in it is
/// made up. Ignored outside Development, which a test proves.
/// </remarks>
public sealed class FakeNotion : HttpMessageHandler
{
    public const string FileHost = "prod-files-secure.s3.us-west-2.amazonaws.com";

    private readonly Dictionary<string, JsonObject> _pages = [];
    private readonly Dictionary<string, JsonObject> _blocks = [];
    private readonly Dictionary<string, List<string>> _children = [];
    private readonly Dictionary<string, JsonObject> _databases = [];
    private readonly Dictionary<string, List<string>> _sources = [];
    private readonly Dictionary<string, (byte[] Body, string ContentType)> _files = [];
    private readonly ConcurrentQueue<TimeSpan> _throttles = new();
    private int _ids;

    /// <summary>How many results one list answer holds. Small, so every list pages.</summary>
    public int PageSize { get; set; } = 2;

    /// <summary>Every request, as "METHOD /path?query", with the token and version it carried.</summary>
    public ConcurrentQueue<(string Request, string? Authorization, string? Version)> Requests { get; } = new();

    /// <summary>The next answer is a 429 with this Retry-After, once per call.</summary>
    public void Throttle(TimeSpan retryAfter) => _throttles.Enqueue(retryAfter);

    public string Page(string title, string? parentBlockId = null)
    {
        var id = NextId();
        _pages[id] = new JsonObject
        {
            ["object"] = "page",
            ["id"] = id,
            ["in_trash"] = false,
            ["parent"] = parentBlockId is null
                ? new JsonObject { ["type"] = "workspace", ["workspace"] = true }
                : new JsonObject { ["type"] = "page_id", ["page_id"] = parentBlockId },
            ["properties"] = new JsonObject
            {
                ["title"] = new JsonObject { ["id"] = "title", ["type"] = "title", ["title"] = new JsonArray(Run(title)) }
            }
        };
        _children[id] = [];
        if (parentBlockId is not null) Block(parentBlockId, "child_page", new JsonObject { ["title"] = title }, id);
        return id;
    }

    /// <summary>A database under a page, with one data source, and its rows as pages.</summary>
    public (string Database, string Source) Database(string parentBlockId, string title)
    {
        var id = NextId();
        var source = NextId();
        Block(parentBlockId, "child_database", new JsonObject { ["title"] = title }, id);
        _databases[id] = new JsonObject
        {
            ["object"] = "database",
            ["id"] = id,
            ["title"] = new JsonArray(Run(title)),
            ["data_sources"] = new JsonArray(new JsonObject { ["id"] = source, ["name"] = title })
        };
        _sources[source] = [];
        return (id, source);
    }

    public string Row(string databaseId, string sourceId, string title)
    {
        var id = NextId();
        _pages[id] = new JsonObject
        {
            ["object"] = "page",
            ["id"] = id,
            ["in_trash"] = false,
            ["parent"] = new JsonObject { ["type"] = "data_source_id", ["data_source_id"] = sourceId, ["database_id"] = databaseId },
            ["properties"] = new JsonObject
            {
                ["Name"] = new JsonObject { ["id"] = "title", ["type"] = "title", ["title"] = new JsonArray(Run(title)) }
            }
        };
        _children[id] = [];
        _sources[sourceId].Add(id);
        return id;
    }

    /// <summary>A block appended to a page or block. Returns its id.</summary>
    public string Block(string parentId, string type, JsonObject payload, string? id = null)
    {
        id ??= NextId();
        var parent = _pages.ContainsKey(parentId)
            ? new JsonObject { ["type"] = "page_id", ["page_id"] = parentId }
            : new JsonObject { ["type"] = "block_id", ["block_id"] = parentId };
        _blocks[id] = new JsonObject
        {
            ["object"] = "block",
            ["id"] = id,
            ["parent"] = parent,
            ["type"] = type,
            ["has_children"] = type is "child_page" or "child_database",
            ["in_trash"] = false,
            [type] = payload
        };
        if (!_children.TryGetValue(parentId, out var list)) _children[parentId] = list = [];
        list.Add(id);
        if (_blocks.TryGetValue(parentId, out var parentBlock)) parentBlock["has_children"] = true;
        _children.TryAdd(id, []);
        return id;
    }

    public string Text(string parentId, string type, params JsonNode[] runs) =>
        Block(parentId, type, new JsonObject { ["rich_text"] = new JsonArray(runs), ["color"] = "default" });

    /// <summary>A file Notion holds, under a block or page. Returns the block's id.</summary>
    public string File(string parentId, string type, string name, string contentType, byte[] body)
    {
        var path = $"/secure/{NextId()}/{Uri.EscapeDataString(name)}";
        _files[path] = (body, contentType);
        return Block(parentId, type, new JsonObject
        {
            ["caption"] = new JsonArray(),
            ["type"] = "file",
            ["name"] = name,
            ["file"] = new JsonObject
            {
                ["url"] = $"https://{FileHost}{path}?X-Amz-Signature=fake",
                ["expiry_time"] = "2099-01-01T00:00:00.000Z"
            }
        });
    }

    public static JsonObject Run(string text, bool bold = false, bool italic = false, bool code = false, string? href = null) => new()
    {
        ["type"] = "text",
        ["text"] = new JsonObject
        {
            ["content"] = text,
            ["link"] = href is null ? null : new JsonObject { ["url"] = href }
        },
        ["annotations"] = new JsonObject
        {
            ["bold"] = bold,
            ["italic"] = italic,
            ["strikethrough"] = false,
            ["underline"] = false,
            ["code"] = code,
            ["color"] = "default"
        },
        ["plain_text"] = text,
        ["href"] = href
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requests.Enqueue(($"{request.Method} {uri.PathAndQuery}",
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("Notion-Version", out var version) ? version.Single() : null));

        if (uri.Host == FileHost)
        {
            return _files.TryGetValue(uri.AbsolutePath, out var file)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(file.Body) { Headers = { { "Content-Type", file.ContentType } } }
                }
                : new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        if (_throttles.TryDequeue(out var wait))
        {
            var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
            return throttled;
        }

        if (request.Headers.Authorization is null) return Error(HttpStatusCode.Unauthorized);

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        return (request.Method.Method, segments) switch
        {
            ("GET", ["v1", "pages", var id]) => Found(_pages, id),
            ("GET", ["v1", "blocks", var id]) => Found(_blocks, id),
            ("GET", ["v1", "databases", var id]) => Found(_databases, id),
            ("GET", ["v1", "blocks", var id, "children"]) when _children.TryGetValue(Key(id), out var ids) =>
                List(ids.Select(child => (JsonNode)_blocks[child].DeepClone()).ToList(), query["start_cursor"]),
            ("POST", ["v1", "data_sources", var id, "query"]) when _sources.TryGetValue(Key(id), out var ids) =>
                List(ids.Select(row => (JsonNode)_pages[row].DeepClone()).ToList(),
                    (await JsonNode.ParseAsync(await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken))?["start_cursor"]?.GetValue<string>()),
            _ => Error(HttpStatusCode.NotFound)
        };
    }

    private HttpResponseMessage List(List<JsonNode> all, string? cursor)
    {
        var start = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
        var slice = all.Skip(start).Take(PageSize).ToList();
        var next = start + slice.Count;
        var more = next < all.Count;
        return Json(new JsonObject
        {
            ["object"] = "list",
            ["results"] = new JsonArray([.. slice]),
            ["has_more"] = more,
            ["next_cursor"] = more ? next.ToString(System.Globalization.CultureInfo.InvariantCulture) : null
        });
    }

    private static HttpResponseMessage Found(Dictionary<string, JsonObject> store, string id) =>
        store.TryGetValue(Key(id), out var found) ? Json(found.DeepClone()) : Error(HttpStatusCode.NotFound);

    private static string Key(string id) => NotionIds.TryNormalize(id, out var key) ? key : id;

    private static HttpResponseMessage Json(JsonNode body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status) =>
        Json(new JsonObject { ["object"] = "error", ["status"] = (int)status, ["message"] = "fake" }, status);

    private string NextId() => new Guid(Interlocked.Increment(ref _ids), 0, 0, new byte[8]).ToString("D");

    /// <summary>
    /// The playbook `make up` and `make e2e` show: one of every block the page
    /// draws, a child page with files, and a database of two pages. The root
    /// is <see cref="SampleRoot"/>.
    /// </summary>
    public static FakeNotion Sample()
    {
        var notion = new FakeNotion();
        var root = notion.Page("Field notes");
        notion.Text(root, "heading_1", Run("How this works"));
        notion.Text(root, "paragraph",
            Run("Read "), Run("before", bold: true), Run(" you start. Keep it "), Run("short", italic: true),
            Run(". Run "), Run("make check", code: true), Run(". See "), Run("the example", href: "https://example.com/"), Run("."));
        notion.Text(root, "heading_2", Run("Steps"));
        var first = notion.Text(root, "numbered_list_item", Run("Gather the inputs"));
        notion.Text(first, "bulleted_list_item", Run("Dates"));
        notion.Text(first, "bulleted_list_item", Run("Names"));
        notion.Text(root, "numbered_list_item", Run("Draft the plan"));
        notion.Text(root, "numbered_list_item", Run("Review it"));
        notion.Block(root, "to_do", new JsonObject { ["rich_text"] = new JsonArray(Run("Book the room")), ["checked"] = true });
        notion.Block(root, "to_do", new JsonObject { ["rich_text"] = new JsonArray(Run("Send the agenda")), ["checked"] = false });
        var toggle = notion.Text(root, "toggle", Run("Why it matters"));
        notion.Text(toggle, "paragraph", Run("A plan nobody reads is not a plan."));
        notion.Text(root, "quote", Run("Slow is smooth."));
        notion.Block(root, "callout", new JsonObject
        {
            ["rich_text"] = new JsonArray(Run("Check the date twice.")),
            ["icon"] = new JsonObject { ["type"] = "emoji", ["emoji"] = "💡" }
        });
        notion.Block(root, "code", new JsonObject { ["rich_text"] = new JsonArray(Run("echo ready")), ["language"] = "shell" });
        notion.Block(root, "divider", new JsonObject());
        var table = notion.Block(root, "table", new JsonObject { ["table_width"] = 2, ["has_column_header"] = true, ["has_row_header"] = false });
        foreach (var (a, b) in new[] { ("Item", "Owner"), ("Agenda", "Lead"), ("Room", "Planner") })
        {
            notion.Block(table, "table_row", new JsonObject { ["cells"] = new JsonArray(new JsonArray(Run(a)), new JsonArray(Run(b))) });
        }

        notion.File(root, "file", "example.txt", "text/plain", Encoding.UTF8.GetBytes("An example file.\n"));

        var checklists = notion.Page("Checklists", root);
        notion.Text(checklists, "paragraph", Run("Print these."));
        notion.File(checklists, "pdf", "packing-list.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.4\n%fake\n"));

        var (database, source) = notion.Database(root, "Templates");
        var letter = notion.Row(database, source, "Letter template");
        notion.Text(letter, "paragraph", Run("Dear reader,"));
        notion.Row(database, source, "Meeting template");

        if (root != SampleRoot) throw new InvalidOperationException("The sample's root is always the first id.");
        return notion;
    }

    /// <summary>The root page of <see cref="Sample"/>, which compose.yaml names as Notion__PlaybookPageId.</summary>
    public const string SampleRoot = "00000001-0000-0000-0000-000000000000";
}
