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

    public string Page(string title, string? parentBlockId = null, string? id = null)
    {
        id ??= NextId();
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

    public string Row(string databaseId, string sourceId, string title, JsonObject? properties = null)
    {
        var id = NextId();
        var all = new JsonObject
        {
            ["Name"] = new JsonObject { ["id"] = "title", ["type"] = "title", ["title"] = new JsonArray(Run(title)) }
        };
        foreach (var (name, value) in properties ?? []) all[name] = value?.DeepClone();
        _pages[id] = new JsonObject
        {
            ["object"] = "page",
            ["id"] = id,
            ["in_trash"] = false,
            ["url"] = "https://www.notion.so/" + id.Replace("-", ""),
            ["parent"] = new JsonObject { ["type"] = "data_source_id", ["data_source_id"] = sourceId, ["database_id"] = databaseId },
            ["properties"] = all
        };
        _children[id] = [];
        _sources[sourceId].Add(id);
        return id;
    }

    /// <summary>Sets one column of a row, as the owner editing it in Notion would.</summary>
    public void SetProperty(string pageId, string name, JsonObject value) =>
        _pages[Key(pageId)]["properties"]!.AsObject()[name] = value;

    /// <summary>A file Notion holds, as one entry of a Files column.</summary>
    public JsonObject FileEntry(string name, string contentType, byte[] body)
    {
        var path = $"/secure/{NextId()}/{Uri.EscapeDataString(name)}";
        _files[path] = (body, contentType);
        return new JsonObject
        {
            ["name"] = name,
            ["type"] = "file",
            ["file"] = new JsonObject { ["url"] = $"https://{FileHost}{path}?X-Amz-Signature=fake", ["expiry_time"] = "2099-01-01T00:00:00.000Z" }
        };
    }

    /// <summary>Column values the way Notion's API writes them.</summary>
    public static class Column
    {
        public static JsonObject Text(string text) => new() { ["type"] = "rich_text", ["rich_text"] = text.Length == 0 ? new JsonArray() : new JsonArray(Run(text)) };
        public static JsonObject Number(double? value) => new() { ["type"] = "number", ["number"] = value };
        public static JsonObject Select(string? name) => new() { ["type"] = "select", ["select"] = name is null ? null : new JsonObject { ["name"] = name } };
        public static JsonObject Date(string start) => new() { ["type"] = "date", ["date"] = new JsonObject { ["start"] = start, ["end"] = null } };
        public static JsonObject Checkbox(bool value) => new() { ["type"] = "checkbox", ["checkbox"] = value };
        public static JsonObject Files(params JsonObject[] files) => new() { ["type"] = "files", ["files"] = new JsonArray([.. files]) };
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

    /// <summary>A mention of a page, the way Notion writes one in rich text.</summary>
    public static JsonObject Mention(string pageId, string title) => new()
    {
        ["type"] = "mention",
        ["mention"] = new JsonObject { ["type"] = "page", ["page"] = new JsonObject { ["id"] = pageId } },
        ["annotations"] = new JsonObject { ["bold"] = false, ["italic"] = false, ["strikethrough"] = false, ["underline"] = false, ["code"] = false, ["color"] = "default" },
        ["plain_text"] = title,
        ["href"] = "https://www.notion.so/" + pageId.Replace("-", "")
    };

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

        // The fitness lessons page, a root of its own, as it sits in Notes.
        var lessons = notion.Page("Military athlete: fitness plan lessons learned", id: SampleLessons);
        notion.Text(lessons, "heading_1", Run("Running"));
        notion.Text(lessons, "heading_2", Run("Treadmill or outdoors"));
        notion.Text(lessons, "paragraph", Run("Set the treadmill to 1% incline for outdoor-equivalent effort."));

        // The marksmanship plan, a root of its own, with its training log.
        var plan = notion.Page("President's Hundred pistol plan", id: SampleMarksmanship);
        notion.Text(plan, "heading_2", Run("Bottom line"));
        notion.Text(plan, "paragraph", Run("The goal is 342 at Camp Perry."));
        var (log, logSource) = notion.Database(plan, "Pistol training log");
        notion.Row(log, logSource, "Baseline President's course", new JsonObject
        {
            ["Type"] = new JsonObject { ["type"] = "select", ["select"] = new JsonObject { ["name"] = "Baseline" } },
            ["Date"] = new JsonObject { ["type"] = "date", ["date"] = new JsonObject { ["start"] = "2026-11-21", ["end"] = null } },
            ["Slow fire"] = new JsonObject { ["type"] = "number", ["number"] = 150 },
            ["Timed"] = new JsonObject { ["type"] = "number", ["number"] = 85 },
            ["Rapid"] = new JsonObject { ["type"] = "number", ["number"] = 80 },
            ["Total"] = new JsonObject { ["type"] = "formula", ["formula"] = new JsonObject { ["type"] = "number", ["number"] = 315 } },
            ["Notes"] = new JsonObject { ["type"] = "rich_text", ["rich_text"] = new JsonArray(Run("Slow fire lost the most.")) }
        });
        SampleTrainingPlan(notion);

        if (root != SampleRoot) throw new InvalidOperationException("The sample's root is always the first id.");
        return notion;
    }

    /// <summary>
    /// A made-up two-week plan in the shape of the owner's: a page with a
    /// heading, a paragraph and a file, a Workouts database and a Plan notes
    /// database with one attachment.
    /// </summary>
    private static void SampleTrainingPlan(FakeNotion notion)
    {
        var plan = notion.Page("Sample training plan", id: SamplePlan);
        notion.Text(plan, "heading_3", Run("About the plan"));
        notion.Text(plan, "paragraph", Run("Two made-up weeks of base training."));
        notion.File(plan, "pdf", "plan-guide.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.4\n%guide\n"));
        // Two attachments in toggles, as the owner's plan page holds them: the
        // same file name twice, told apart by the toggle's title.
        foreach (var (title, body) in new[] { ("Block 1 introduction", "%PDF-1.4\n%block1\n"), ("Block 3 introduction", "%PDF-1.4\n%block3\n") })
        {
            var toggle = notion.Block(plan, "heading_4", new JsonObject { ["rich_text"] = new JsonArray(Run(title)), ["is_toggleable"] = true });
            notion.Text(toggle, "paragraph", Run("Attached to: a plan note."));
            notion.File(toggle, "pdf", "For best results.pdf", "application/pdf", Encoding.ASCII.GetBytes(body));
        }

        // A page elsewhere that the plan mentions, and a subpage of its own.
        var guide = notion.Page("Sample guide", id: SamplePlanGuide);
        notion.Text(guide, "paragraph", Run("Start with four easy runs a week."));
        notion.Text(plan, "bulleted_list_item", Mention(guide, "Sample guide"), Run(". A guide for beginners."));
        var analysis = notion.Page("Sample analysis", plan);
        notion.Text(analysis, "heading_2", Run("Progression"));
        notion.Text(analysis, "paragraph", Run("Three build weeks, then a deload."));

        var (workouts, workoutsSource) = notion.Database(plan, TrainingPlanReader.WorkoutsTitle);
        void Workout(string title, string date, int week, int day, string weekday, string sport, int order, double? minutes, double? tss, string description, string comments = "", string structure = "") =>
            notion.Row(workouts, workoutsSource, title, new JsonObject
            {
                ["Date"] = Column.Date(date),
                ["Week"] = Column.Number(week),
                ["Plan day"] = Column.Number(day),
                ["Weekday"] = Column.Select(weekday),
                ["Block"] = Column.Select("Block 1"),
                ["Week label"] = Column.Text($"Block 1 Week {week} Notes"),
                ["Sport"] = Column.Select(sport),
                ["Order on day"] = Column.Number(order),
                ["Duration (min)"] = Column.Number(minutes),
                ["TSS"] = Column.Number(tss),
                ["Description"] = Column.Text(description),
                ["Pre-activity comments"] = Column.Text(comments),
                ["Structure"] = Column.Text(structure),
                ["Changed"] = Column.Checkbox(false)
            });
        Workout("Dynamic warmup", "2026-11-02", 1, 1, "Mon", "Other", 0, 5, null, "Walking knee hugs and quad stretch.");
        Workout("Aerobic base run", "2026-11-02", 1, 1, "Mon", "Run", 1, 45, 33, "Run at your MAF heart rate. See https://example.com/maf for how.", "Nose breathe the whole run.");
        Workout("Push strength", "2026-11-04", 1, 3, "Wed", "Strength", 0, 60, null, "3x10 bench press.");
        Workout("Rest day", "2026-11-08", 1, 7, "Sun", "Day Off", 0, null, null, "Rest.");
        Workout("Heart rate drift test", "2026-11-09", 2, 8, "Mon", "Other", 0, 75, 57.5, "Run at a 2% grade for 60 minutes.", "", "Warm up: 15:00 at 1-3 RPE\nActive: 1:00:00 at 3-4 RPE");

        var (notes, notesSource) = notion.Database(plan, TrainingPlanReader.NotesTitle);
        notion.Row(notes, notesSource, "Intro to block one", new JsonObject
        {
            ["Date"] = Column.Date("2026-11-02"),
            ["Week"] = Column.Number(1),
            ["Plan day"] = Column.Number(1),
            ["Weekday"] = Column.Select("Mon"),
            ["Block"] = Column.Select("Block 1"),
            ["Description"] = Column.Text("Read the attachment before you start."),
            ["Attachments"] = Column.Files(notion.FileEntry("intro.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.4\n%intro\n")))
        });
        notion.Row(notes, notesSource, "Week two recap", new JsonObject
        {
            ["Date"] = Column.Date("2026-11-15"),
            ["Week"] = Column.Number(2),
            ["Plan day"] = Column.Number(14),
            ["Weekday"] = Column.Select("Sun"),
            ["Description"] = Column.Text("Two weeks done.")
        });
    }

    /// <summary>The page the sample plan mentions, outside it.</summary>
    public const string SamplePlanGuide = "0000e70f-0000-0000-0000-000000000000";

    /// <summary>The plan page of <see cref="Sample"/>, which compose.yaml names as Notion__TrainingPlanPageId.</summary>
    public const string SamplePlan = "0000e70e-0000-0000-0000-000000000000";

    /// <summary>The root page of <see cref="Sample"/>, which compose.yaml names as Notion__PlaybookPageId.</summary>
    public const string SampleRoot = "00000001-0000-0000-0000-000000000000";

    /// <summary>The lessons page of <see cref="Sample"/>, which compose.yaml names as Notion__FitnessLessonsPageId.</summary>
    public const string SampleLessons = "0000f17e-0000-0000-0000-000000000000";

    /// <summary>The marksmanship plan of <see cref="Sample"/>, which compose.yaml names as Notion__MarksmanshipPlanPageId.</summary>
    public const string SampleMarksmanship = "0000a3c0-0000-0000-0000-000000000000";
}
