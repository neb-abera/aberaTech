using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace aberaTech.Server.Playbook;

/// <summary>The tree under the root page, with every page and database in it by id.</summary>
public sealed class PlaybookTree(PlaybookNode root, IReadOnlyDictionary<string, PlaybookNode> pages, IReadOnlyDictionary<string, PlaybookNode> databases)
{
    public PlaybookNode Root { get; } = root;
    public IReadOnlyDictionary<string, PlaybookNode> Pages { get; } = pages;
    public IReadOnlyDictionary<string, PlaybookNode> Databases { get; } = databases;
}

/// <summary>A file block under the root, with the signed address it holds right now.</summary>
public sealed record PlaybookFile(Uri Address, string Name);

/// <summary>
/// Reads the playbook out of Notion: the tree under the root page, one
/// page's blocks in the shapes <see cref="PlaybookBlock"/> lists, and a
/// file block's current address.
/// </summary>
/// <remarks>
/// The tree is the allowlist. A page is served only when the tree has it,
/// and a file only when its block's parents lead to a page the tree has.
/// Anything else is null, which the endpoints answer with 404, whatever
/// the token could reach.
/// </remarks>
public sealed class PlaybookReader(NotionClient notion, IMemoryCache cache, NotionOptions options)
{
    /// <summary>How deep pages nest before the tree stops looking.</summary>
    public const int MaxDepth = 12;

    /// <summary>How many parents a file block may have before it is refused.</summary>
    private const int MaxParents = 64;

    /// <summary>The hosts a signed file address may name. Notion keeps uploads on S3.</summary>
    private static readonly string[] FileHostSuffixes =
        [".amazonaws.com", ".notion.so", ".notion-static.com", ".notionusercontent.com"];

    private static readonly HashSet<string> FileTypes = ["file", "pdf", "image", "video", "audio"];

    private static readonly HashSet<string> Flattened = ["column_list", "column", "synced_block"];

    private static readonly HashSet<string> Skipped = ["breadcrumb", "table_of_contents", "unsupported"];

    public async Task<PlaybookTree> TreeAsync(CancellationToken cancellationToken)
    {
        const string key = "playbook:tree";
        if (cache.TryGetValue(key, out PlaybookTree? hit) && hit is not null) return hit;

        NotionIds.TryNormalize(options.PlaybookPageId, out var rootId);
        var root = await notion.PageAsync(rootId, cancellationToken);
        var pages = new Dictionary<string, PlaybookNode>();
        var databases = new Dictionary<string, PlaybookNode>();
        var seen = new HashSet<string> { rootId };

        var children = await CollectAsync(rootId, 1, pages, databases, seen, cancellationToken);
        var node = new PlaybookNode(rootId, PageTitle(root), PlaybookNode.Page, children);
        pages[rootId] = node;

        var tree = new PlaybookTree(node, pages, databases);
        cache.Set(key, tree, TimeSpan.FromSeconds(Math.Max(0, options.CacheSeconds)));
        return tree;
    }

    /// <summary>A page's blocks, or null when the page is not in the tree.</summary>
    public async Task<PlaybookPage?> PageAsync(string id, CancellationToken cancellationToken)
    {
        var tree = await TreeAsync(cancellationToken);
        if (!tree.Pages.TryGetValue(id, out var node)) return null;

        var blocks = await ConvertAsync(await notion.ChildrenAsync(id, cancellationToken), tree, cancellationToken);
        return new PlaybookPage(node.Id, node.Title, blocks);
    }

    /// <summary>
    /// A file block's address as Notion signs it now, or null when the block
    /// is not a file Notion holds, or is not under the root.
    /// </summary>
    public async Task<PlaybookFile?> FileAsync(string blockId, CancellationToken cancellationToken)
    {
        var tree = await TreeAsync(cancellationToken);

        JsonElement block;
        try
        {
            block = await notion.BlockAsync(blockId, fresh: true, cancellationToken);
        }
        catch (NotionException exception) when (exception.Status is System.Net.HttpStatusCode.NotFound
                                                     or System.Net.HttpStatusCode.BadRequest)
        {
            return null;
        }

        var type = Text(block, "type");
        if (type is null || !FileTypes.Contains(type) || IsTrashed(block)) return null;
        if (!await IsUnderRootAsync(block, tree, cancellationToken)) return null;

        var payload = block.GetProperty(type);
        if (Text(payload, "type") != "file") return null;
        if (!payload.TryGetProperty("file", out var file)) return null;
        if (!Uri.TryCreate(Text(file, "url"), UriKind.Absolute, out var address) || !IsFileHost(address)) return null;

        return new PlaybookFile(address, FileName(payload, address));
    }

    public static bool IsFileHost(Uri address) =>
        address.Scheme == Uri.UriSchemeHttps
        && FileHostSuffixes.Any(suffix => address.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> IsUnderRootAsync(JsonElement block, PlaybookTree tree, CancellationToken cancellationToken)
    {
        var current = block;
        for (var hop = 0; hop < MaxParents; hop++)
        {
            if (!current.TryGetProperty("parent", out var parent)) return false;
            var kind = Text(parent, "type");
            if (kind is not ("page_id" or "block_id")) return false;
            if (!NotionIds.TryNormalize(Text(parent, kind), out var parentId)) return false;
            if (kind == "page_id") return tree.Pages.ContainsKey(parentId);

            try
            {
                current = await notion.BlockAsync(parentId, fresh: false, cancellationToken);
            }
            catch (NotionException)
            {
                return false;
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<PlaybookNode>> CollectAsync(
        string blockId,
        int depth,
        Dictionary<string, PlaybookNode> pages,
        Dictionary<string, PlaybookNode> databases,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        var nodes = new List<PlaybookNode>();
        if (depth > MaxDepth) return nodes;

        foreach (var block in await notion.ChildrenAsync(blockId, cancellationToken))
        {
            if (IsTrashed(block) || !NotionIds.TryNormalize(Text(block, "id"), out var id)) continue;
            var type = Text(block, "type");

            if (type == "child_page")
            {
                if (!seen.Add(id)) continue;
                var title = Text(block.GetProperty("child_page"), "title") ?? "";
                var children = await CollectAsync(id, depth + 1, pages, databases, seen, cancellationToken);
                var node = new PlaybookNode(id, Untitled(title), PlaybookNode.Page, children);
                pages[id] = node;
                nodes.Add(node);
            }
            else if (type == "child_database")
            {
                if (!seen.Add(id)) continue;
                var title = Text(block.GetProperty("child_database"), "title") ?? "";
                var rows = await DatabasePagesAsync(id, depth, pages, databases, seen, cancellationToken);
                var node = new PlaybookNode(id, Untitled(title), PlaybookNode.Database, rows);
                databases[id] = node;
                nodes.Add(node);
            }
            else if (HasChildren(block))
            {
                // A page can sit inside a toggle or a column. It is still the
                // page's child.
                nodes.AddRange(await CollectAsync(id, depth, pages, databases, seen, cancellationToken));
            }
        }

        return nodes;
    }

    private async Task<IReadOnlyList<PlaybookNode>> DatabasePagesAsync(
        string databaseId,
        int depth,
        Dictionary<string, PlaybookNode> pages,
        Dictionary<string, PlaybookNode> databases,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        JsonElement database;
        try
        {
            database = await notion.DatabaseAsync(databaseId, cancellationToken);
        }
        catch (NotionException exception) when (exception.Status == System.Net.HttpStatusCode.NotFound)
        {
            // A linked view of a database the integration cannot see.
            return [];
        }

        var rows = new List<PlaybookNode>();
        if (!database.TryGetProperty("data_sources", out var sources) || sources.ValueKind != JsonValueKind.Array) return rows;

        foreach (var source in sources.EnumerateArray())
        {
            if (!NotionIds.TryNormalize(Text(source, "id"), out var sourceId)) continue;
            foreach (var page in await notion.QueryAsync(sourceId, cancellationToken))
            {
                if (IsTrashed(page) || !NotionIds.TryNormalize(Text(page, "id"), out var id) || !seen.Add(id)) continue;
                var children = await CollectAsync(id, depth + 1, pages, databases, seen, cancellationToken);
                var node = new PlaybookNode(id, PageTitle(page), PlaybookNode.Page, children);
                pages[id] = node;
                rows.Add(node);
            }
        }

        return rows;
    }

    private async Task<IReadOnlyList<PlaybookBlock>> ConvertAsync(
        IReadOnlyList<JsonElement> blocks, PlaybookTree tree, CancellationToken cancellationToken)
    {
        var converted = new List<PlaybookBlock>();
        foreach (var block in blocks)
        {
            if (IsTrashed(block)) continue;
            var type = Text(block, "type") ?? "";
            if (Skipped.Contains(type)) continue;
            NotionIds.TryNormalize(Text(block, "id"), out var id);
            block.TryGetProperty(type, out var payload);

            if (Flattened.Contains(type))
            {
                if (HasChildren(block))
                {
                    converted.AddRange(await ConvertAsync(await notion.ChildrenAsync(id, cancellationToken), tree, cancellationToken));
                }

                continue;
            }

            if (type == "table")
            {
                converted.Add(await TableAsync(id, payload, tree, cancellationToken));
                continue;
            }

            IReadOnlyList<PlaybookBlock>? children = null;
            if (HasChildren(block) && type is not ("child_page" or "child_database"))
            {
                children = await ConvertAsync(await notion.ChildrenAsync(id, cancellationToken), tree, cancellationToken);
            }

            converted.Add(Convert(type, id, payload, tree) with { Children = children is { Count: > 0 } ? children : null });
        }

        return converted;
    }

    private async Task<PlaybookBlock> TableAsync(string id, JsonElement payload, PlaybookTree tree, CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyList<IReadOnlyList<PlaybookText>>>();
        foreach (var row in await notion.ChildrenAsync(id, cancellationToken))
        {
            if (Text(row, "type") != "table_row"
                || !row.TryGetProperty("table_row", out var payloadRow) || payloadRow.ValueKind != JsonValueKind.Object
                || !payloadRow.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Array) continue;
            rows.Add([.. cells.EnumerateArray().Select(cell => Rich(cell, tree))]);
        }

        return new PlaybookBlock("table")
        {
            HeaderRow = Flag(payload, "has_column_header"),
            HeaderColumn = Flag(payload, "has_row_header"),
            Rows = rows
        };
    }

    private static PlaybookBlock Convert(string type, string id, JsonElement payload, PlaybookTree tree)
    {
        IReadOnlyList<PlaybookText> RichOf(string name = "rich_text") =>
            payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var rich) ? Rich(rich, tree) : [];

        switch (type)
        {
            case "heading_1" or "heading_2" or "heading_3":
                return new PlaybookBlock("heading") { Level = type[^1] - '0', Text = RichOf() };
            case "paragraph":
                return new PlaybookBlock("paragraph") { Text = RichOf() };
            case "bulleted_list_item":
                return new PlaybookBlock("bulleted") { Text = RichOf() };
            case "numbered_list_item":
                return new PlaybookBlock("numbered") { Text = RichOf() };
            case "to_do":
                return new PlaybookBlock("todo") { Text = RichOf(), Checked = Flag(payload, "checked") };
            case "toggle":
                return new PlaybookBlock("toggle") { Text = RichOf() };
            case "quote":
                return new PlaybookBlock("quote") { Text = RichOf() };
            case "callout":
                return new PlaybookBlock("callout") { Text = RichOf(), Icon = Emoji(payload) };
            case "code":
                return new PlaybookBlock("code") { Text = RichOf(), Language = Text(payload, "language") };
            case "divider":
                return new PlaybookBlock("divider");
            case "equation":
                return new PlaybookBlock("equation") { Text = [new PlaybookText(Text(payload, "expression") ?? "") { Code = true }] };
            case "child_page":
                return tree.Pages.ContainsKey(id)
                    ? new PlaybookBlock("page") { Id = id, Title = tree.Pages[id].Title }
                    : new PlaybookBlock("page") { Title = Untitled(Text(payload, "title") ?? "") };
            case "child_database":
                return tree.Databases.TryGetValue(id, out var database)
                    ? new PlaybookBlock("database") { Id = id, Title = database.Title, Pages = [.. database.Children.Select(Shallow)] }
                    : new PlaybookBlock("database") { Title = Untitled(Text(payload, "title") ?? "") };
            case "bookmark" or "embed" or "link_preview":
                var url = Text(payload, "url");
                return IsWebAddress(url)
                    ? new PlaybookBlock("link") { Url = url, Caption = RichOf("caption") }
                    : new PlaybookBlock("unsupported") { NotionType = type };
            default:
                if (FileTypes.Contains(type)) return File(type, id, payload, tree);
                return new PlaybookBlock("unsupported") { NotionType = type };
        }
    }

    private static PlaybookBlock File(string type, string id, JsonElement payload, PlaybookTree tree)
    {
        var hosted = Text(payload, "type") == "file";
        var external = payload.TryGetProperty("external", out var outside) ? Text(outside, "url") : null;
        Uri.TryCreate(hosted && payload.TryGetProperty("file", out var file) ? Text(file, "url") : external,
            UriKind.Absolute, out var address);

        return new PlaybookBlock("file")
        {
            Id = id,
            Kind = type,
            Name = address is null ? Untitled(Text(payload, "name") ?? "") : FileName(payload, address),
            Url = !hosted && IsWebAddress(external) ? external : null,
            Caption = payload.TryGetProperty("caption", out var caption) ? Rich(caption, tree) : null
        };
    }

    private static PlaybookNode Shallow(PlaybookNode node) => node with { Children = [] };

    private static IReadOnlyList<PlaybookText> Rich(JsonElement array, PlaybookTree tree)
    {
        if (array.ValueKind != JsonValueKind.Array) return [];

        var runs = new List<PlaybookText>();
        foreach (var run in array.EnumerateArray())
        {
            var text = Text(run, "plain_text") ?? "";
            if (text.Length == 0) continue;
            var marks = run.TryGetProperty("annotations", out var annotations) ? annotations : default;
            runs.Add(new PlaybookText(text)
            {
                Bold = Mark(marks, "bold"),
                Italic = Mark(marks, "italic"),
                Strikethrough = Mark(marks, "strikethrough"),
                Underline = Mark(marks, "underline"),
                Code = Mark(marks, "code"),
                Href = Link(Text(run, "href"), tree)
            });
        }

        return runs;
    }

    /// <summary>
    /// A link to a playbook page opens it here. A link to any other Notion
    /// page is dropped: the computer this is for cannot open Notion. Any
    /// other web address stays as it is.
    /// </summary>
    private static string? Link(string? href, PlaybookTree tree)
    {
        if (string.IsNullOrEmpty(href)) return null;

        var path = Uri.TryCreate(href, UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : href.Split('?', '#')[0];
        var last = path.TrimEnd('/').Split('/')[^1];
        if (last.Length >= 32 && NotionIds.TryNormalize(last[^32..], out var id) && tree.Pages.ContainsKey(id))
        {
            return "/playbook?page=" + id;
        }

        var notionLink = absolute is not null
                         && (absolute.Host.EndsWith("notion.so", StringComparison.OrdinalIgnoreCase)
                             || absolute.Host.EndsWith("notion.site", StringComparison.OrdinalIgnoreCase));
        return !notionLink && IsWebAddress(href) ? href : null;
    }

    private static bool IsWebAddress(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string FileName(JsonElement payload, Uri address)
    {
        var name = Text(payload, "name");
        if (string.IsNullOrWhiteSpace(name)) name = Uri.UnescapeDataString(address.Segments[^1]);

        var clean = new StringBuilder();
        foreach (var c in name)
        {
            clean.Append(char.IsControl(c) || c is '/' or '\\' or '"' ? '_' : c);
        }

        var result = clean.ToString().Trim();
        return result.Length == 0 ? "file" : result;
    }

    private static string PageTitle(JsonElement page)
    {
        if (page.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (Text(property.Value, "type") == "title" && property.Value.TryGetProperty("title", out var title))
                {
                    return Untitled(string.Concat(title.EnumerateArray().Select(run => Text(run, "plain_text"))));
                }
            }
        }

        return Untitled("");
    }

    private static string Untitled(string title) => string.IsNullOrWhiteSpace(title) ? "Untitled" : title;

    private static string? Emoji(JsonElement payload) =>
        payload.TryGetProperty("icon", out var icon) && icon.ValueKind == JsonValueKind.Object && Text(icon, "type") == "emoji"
            ? Text(icon, "emoji")
            : null;

    private static bool HasChildren(JsonElement block) =>
        block.TryGetProperty("has_children", out var value) && value.ValueKind == JsonValueKind.True;

    private static bool IsTrashed(JsonElement element) =>
        (element.TryGetProperty("in_trash", out var trash) && trash.ValueKind == JsonValueKind.True)
        || (element.TryGetProperty("archived", out var archived) && archived.ValueKind == JsonValueKind.True);

    private static bool? Mark(JsonElement marks, string name) =>
        marks.ValueKind == JsonValueKind.Object && marks.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True
            ? true
            : null;

    private static bool Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
