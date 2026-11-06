using System.Globalization;
using System.Text;
using System.Text.Json;

namespace aberaTech.Server.Playbook;

/// <summary>A file on the plan page or on a plan note. Its id is what the download route takes.</summary>
public sealed record PlanFile(string Id, string Name);

/// <summary>A page the plan page links: one of its subpages, or a page mentioned on it.</summary>
public sealed record PlanLink(string Id, string Title);

/// <summary>One heading on the plan page and the paragraphs under it.</summary>
public sealed record PlanSection(string Heading, IReadOnlyList<string> Paragraphs);

/// <summary>The day a row sits on, and its place in the plan.</summary>
public sealed record PlanPlace(string? Date, int? Week, int? PlanDay, string? Weekday, string? Block, string? WeekLabel);

/// <summary>One row of the Workouts database. Only the columns the page shows.</summary>
public sealed record PlanWorkout(
    string Id,
    string Title,
    PlanPlace Place,
    string? Sport,
    int? Order,
    double? Minutes,
    double? Kilometers,
    double? Tss,
    double? IntensityFactor,
    string Description,
    string Comments,
    string Structure,
    bool Changed,
    string? Url);

/// <summary>One row of the Plan notes database.</summary>
public sealed record PlanNote(string Id, string Title, PlanPlace Place, string Description, IReadOnlyList<PlanFile> Files, bool Changed, string? Url);

/// <summary>The plan page, its files, and every row of its two databases.</summary>
public sealed record TrainingPlan(
    string Id,
    string Title,
    string? Url,
    IReadOnlyList<PlanSection> Sections,
    IReadOnlyList<PlanFile> Files,
    IReadOnlyList<PlanWorkout> Workouts,
    IReadOnlyList<PlanNote> Notes,
    IReadOnlyList<PlanLink> Pages);

/// <summary>A file's current signed address and its name.</summary>
public sealed record PlanDownload(Uri Address, string Name);

/// <summary>
/// Reads the training plan out of Notion: the plan page's own blocks, and
/// the rows of the Workouts and Plan notes databases on it, by column name.
/// </summary>
/// <remarks>
/// One call for the page, one for its blocks, one per database and one per
/// hundred rows: about ten for the Evoke plan's 749 workouts and 99 notes.
/// Row bodies are never read, because every column the page shows is a
/// property. A renamed or deleted column reads as empty, never as an error.
/// A file is served only from a file block on the plan page or in a toggle on it,
/// or from the Attachments column of a row in the plan's own notes
/// database. Its address is asked fresh, since a signed address expires
/// after an hour.
/// </remarks>
public sealed class TrainingPlanReader(NotionClient notion, NotionOptions options)
{
    public const string WorkoutsTitle = "Workouts";
    public const string NotesTitle = "Plan notes";

    private static readonly HashSet<string> FileTypes = ["file", "pdf", "image", "video", "audio"];

    private string Root => NotionIds.TryNormalize(options.TrainingPlanPageId, out var id) ? id : "";

    public async Task<TrainingPlan> ReadAsync(CancellationToken cancellationToken)
    {
        var page = await notion.PageAsync(Root, cancellationToken);
        var blocks = await notion.ChildrenAsync(Root, cancellationToken);

        var sections = new List<PlanSection>();
        var files = new List<PlanFile>();
        var pages = new List<PlanLink>();
        string? heading = null;
        var paragraphs = new List<string>();
        string? workoutsSource = null, notesSource = null;

        foreach (var block in blocks)
        {
            var type = Str(block, "type");
            var id = Str(block, "id");
            if (type is null || id is null || !block.TryGetProperty(type, out var payload)) continue;

            Links(payload, pages);
            if (type == "child_page" && NotionIds.TryNormalize(id, out var subpageId) && pages.All(link => link.Id != subpageId))
            {
                pages.Add(new PlanLink(subpageId, Str(payload, "title") ?? ""));
            }

            if (type is "heading_1" or "heading_2" or "heading_3")
            {
                if (heading is not null || paragraphs.Count > 0) sections.Add(new PlanSection(heading ?? "", [.. paragraphs]));
                heading = Plain(payload, "rich_text");
                paragraphs = [];
            }
            else if (type is "paragraph" or "bulleted_list_item" or "numbered_list_item" or "quote" or "callout")
            {
                var text = Plain(payload, "rich_text");
                if (text.Length > 0) paragraphs.Add(text);
            }
            else if (FileTypes.Contains(type))
            {
                files.Add(new PlanFile(id, FileName(payload) ?? "file"));
            }

            // A toggle holds its attachment one level down. The file takes the
            // toggle's title, so two files with one name stay apart.
            if (type is not ("child_page" or "child_database")
                && block.TryGetProperty("has_children", out var nested) && nested.ValueKind == JsonValueKind.True)
            {
                var label = Plain(payload, "rich_text");
                var inside = new List<PlanFile>();
                foreach (var child in await notion.ChildrenAsync(id, cancellationToken))
                {
                    var childType = Str(child, "type");
                    if (childType is null || !FileTypes.Contains(childType) || Str(child, "id") is not { } childId
                        || !child.TryGetProperty(childType, out var childPayload)) continue;
                    inside.Add(new PlanFile(childId, FileName(childPayload) ?? "file"));
                }

                files.AddRange(inside.Count == 1 && label.Length > 0 ? [inside[0] with { Name = label }] : inside);
            }
            else if (type == "child_database" && NotionIds.TryNormalize(id, out var databaseId))
            {
                var title = Str(payload, "title");
                if (title == WorkoutsTitle) workoutsSource ??= await SourceAsync(databaseId, cancellationToken);
                if (title == NotesTitle) notesSource ??= await SourceAsync(databaseId, cancellationToken);
            }
        }

        if (heading is not null || paragraphs.Count > 0) sections.Add(new PlanSection(heading ?? "", [.. paragraphs]));

        var workouts = new List<PlanWorkout>();
        if (workoutsSource is not null)
        {
            foreach (var row in await notion.QueryAsync(workoutsSource, cancellationToken))
            {
                if (Str(row, "id") is not { } id || !row.TryGetProperty("properties", out var p) || p.ValueKind != JsonValueKind.Object) continue;
                workouts.Add(new PlanWorkout(
                    id,
                    Title(p),
                    Place(p),
                    Select(p, "Sport"),
                    Number(p, "Order on day") is { } order ? (int)order : null,
                    Number(p, "Duration (min)"),
                    Number(p, "Distance (km)"),
                    Number(p, "TSS"),
                    Number(p, "IF"),
                    RichText(p, "Description"),
                    RichText(p, "Pre-activity comments"),
                    RichText(p, "Structure"),
                    Checkbox(p, "Changed"),
                    Str(row, "url")));
            }
        }

        var notes = new List<PlanNote>();
        if (notesSource is not null)
        {
            foreach (var row in await notion.QueryAsync(notesSource, cancellationToken))
            {
                if (Str(row, "id") is not { } id || !row.TryGetProperty("properties", out var p) || p.ValueKind != JsonValueKind.Object) continue;
                var attached = Files(p, "Attachments")
                    .Select((name, index) => new PlanFile($"{id}:{index}", name))
                    .ToList();
                notes.Add(new PlanNote(id, Title(p), Place(p), RichText(p, "Description"), attached, Checkbox(p, "Changed"), Str(row, "url")));
            }
        }

        workouts.Sort((a, b) => Compare(a.Place, b.Place, a.Order ?? 0, b.Order ?? 0));
        notes.Sort((a, b) => Compare(a.Place, b.Place, 0, 0));

        return new TrainingPlan(Root, PageTitle(page), Str(page, "url"), sections, files, workouts, notes, pages);
    }

    /// <summary>
    /// A file block on the plan page (its id), or the nth attachment of a row
    /// in the plan's notes database ("rowId:n"). Anything else is null.
    /// </summary>
    public async Task<PlanDownload?> FileAsync(string id, CancellationToken cancellationToken)
    {
        var parts = id.Split(':');
        if (parts.Length == 1)
        {
            if (!NotionIds.TryNormalize(parts[0], out var blockId)) return null;
            var block = await notion.BlockAsync(blockId, fresh: true, cancellationToken);
            var type = Str(block, "type");
            if (type is null || !FileTypes.Contains(type) || !block.TryGetProperty(type, out var payload)) return null;
            if (!await OnPlanPageAsync(block, cancellationToken)) return null;
            return Address(payload) is { } address ? new PlanDownload(address, FileName(payload) ?? "file") : null;
        }

        if (parts.Length != 2 || !NotionIds.TryNormalize(parts[0], out var rowId)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index)) return null;

        var notesSource = await NotesSourceAsync(cancellationToken);
        if (notesSource is null) return null;

        var row = await notion.PageFreshAsync(rowId, cancellationToken);
        if (!row.TryGetProperty("parent", out var rowParent) || Str(rowParent, "data_source_id") is not { } source
            || !NotionIds.TryNormalize(source, out var sourceId) || sourceId != notesSource) return null;
        if (!row.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object
            || !Column(properties, "Attachments", out var column)
            || !column.TryGetProperty("files", out var list) || list.ValueKind != JsonValueKind.Array
            || index >= list.GetArrayLength()) return null;

        var file = list[index];
        return Address(file) is { } url ? new PlanDownload(url, Str(file, "name") ?? "file") : null;
    }

    /// <summary>A block on the plan page, or inside a block that is: one level, as a toggle holds an attachment.</summary>
    private async Task<bool> OnPlanPageAsync(JsonElement block, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("parent", out var parent) || parent.ValueKind != JsonValueKind.Object) return false;
        if (Str(parent, "page_id") is { } pageId) return NotionIds.TryNormalize(pageId, out var page) && page == Root;
        if (Str(parent, "block_id") is not { } blockId || !NotionIds.TryNormalize(blockId, out var holderId)) return false;

        var holder = await notion.BlockAsync(holderId, fresh: false, cancellationToken);
        return holder.TryGetProperty("parent", out var up) && up.ValueKind == JsonValueKind.Object
               && Str(up, "page_id") is { } upPage && NotionIds.TryNormalize(upPage, out var root) && root == Root;
    }

    /// <summary>Every page mentioned in a block's text, once each, in the order they appear.</summary>
    private static void Links(JsonElement payload, List<PlanLink> into)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("rich_text", out var runs) || runs.ValueKind != JsonValueKind.Array) return;
        foreach (var run in runs.EnumerateArray())
        {
            if (run.ValueKind != JsonValueKind.Object || Str(run, "type") != "mention"
                || !run.TryGetProperty("mention", out var mention) || mention.ValueKind != JsonValueKind.Object
                || Str(mention, "type") != "page" || !mention.TryGetProperty("page", out var page)
                || Str(page, "id") is not { } pageId || !NotionIds.TryNormalize(pageId, out var id)) continue;
            if (into.All(link => link.Id != id)) into.Add(new PlanLink(id, Str(run, "plain_text") ?? ""));
        }
    }

    private async Task<string?> NotesSourceAsync(CancellationToken cancellationToken)
    {
        foreach (var block in await notion.ChildrenAsync(Root, cancellationToken))
        {
            if (Str(block, "type") != "child_database" || Str(block, "id") is not { } id
                || !block.TryGetProperty("child_database", out var payload) || Str(payload, "title") != NotesTitle
                || !NotionIds.TryNormalize(id, out var databaseId)) continue;
            return await SourceAsync(databaseId, cancellationToken);
        }

        return null;
    }

    private async Task<string?> SourceAsync(string databaseId, CancellationToken cancellationToken)
    {
        var database = await notion.DatabaseAsync(databaseId, cancellationToken);
        if (!database.TryGetProperty("data_sources", out var sources) || sources.ValueKind != JsonValueKind.Array) return null;
        foreach (var source in sources.EnumerateArray())
        {
            if (Str(source, "id") is { } id && NotionIds.TryNormalize(id, out var sourceId)) return sourceId;
        }

        return null;
    }

    private static int Compare(PlanPlace a, PlanPlace b, int orderA, int orderB)
    {
        var byDate = string.CompareOrdinal(a.Date ?? "9999", b.Date ?? "9999");
        return byDate != 0 ? byDate : orderA.CompareTo(orderB);
    }

    private static PlanPlace Place(JsonElement p)
    {
        string? date = null;
        if (Column(p, "Date", out var column) && column.TryGetProperty("date", out var value)
            && value.ValueKind == JsonValueKind.Object && Str(value, "start") is { } start)
        {
            date = start.Length >= 10 ? start[..10] : start;
        }

        var week = Number(p, "Week");
        var day = Number(p, "Plan day");
        var label = RichText(p, "Week label");
        return new PlanPlace(date, week is null ? null : (int)week, day is null ? null : (int)day, Select(p, "Weekday"),
            Select(p, "Block"), label.Length == 0 ? null : label);
    }

    private static string Title(JsonElement properties)
    {
        foreach (var property in properties.EnumerateObject())
        {
            if (Str(property.Value, "type") == "title") return Plain(property.Value, "title");
        }

        return "";
    }

    private static string PageTitle(JsonElement page) =>
        page.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object ? Title(properties) : "";

    /// <summary>A column by name, when it is an object as Notion writes one.</summary>
    private static bool Column(JsonElement p, string name, out JsonElement column) =>
        p.TryGetProperty(name, out column) && column.ValueKind == JsonValueKind.Object;

    private static string RichText(JsonElement p, string name) =>
        p.TryGetProperty(name, out var column) ? Plain(column, "rich_text") : "";

    private static string? Select(JsonElement p, string name) =>
        Column(p, name, out var column) && column.TryGetProperty("select", out var value) && value.ValueKind == JsonValueKind.Object
            ? Str(value, "name")
            : null;

    private static double? Number(JsonElement p, string name) =>
        Column(p, name, out var column) && column.TryGetProperty("number", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static bool Checkbox(JsonElement p, string name) =>
        Column(p, name, out var column) && column.TryGetProperty("checkbox", out var value) && value.ValueKind == JsonValueKind.True;

    private static IEnumerable<string> Files(JsonElement p, string name)
    {
        if (!Column(p, name, out var column) || !column.TryGetProperty("files", out var list) || list.ValueKind != JsonValueKind.Array) yield break;
        foreach (var file in list.EnumerateArray()) yield return Str(file, "name") ?? "file";
    }

    private static string Plain(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out var runs) || runs.ValueKind != JsonValueKind.Array) return "";
        var text = new StringBuilder();
        foreach (var run in runs.EnumerateArray())
        {
            if (run.ValueKind != JsonValueKind.Object) continue;
            if (Str(run, "plain_text") is { } plain) text.Append(plain);
            else if (run.TryGetProperty("text", out var t) && Str(t, "content") is { } content) text.Append(content);
        }

        return text.ToString();
    }

    private static string? FileName(JsonElement payload)
    {
        if (Str(payload, "name") is { Length: > 0 } name) return name;
        return Address(payload) is { } url ? Uri.UnescapeDataString(url.Segments[^1]) : null;
    }

    /// <summary>The address of a file Notion hosts, on a host <see cref="PlaybookReader.IsFileHost"/> allows. An external link is never fetched.</summary>
    private static Uri? Address(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || Str(payload, "type") is { } type && type != "file") return null;
        return payload.TryGetProperty("file", out var holder) && holder.ValueKind == JsonValueKind.Object
               && Uri.TryCreate(Str(holder, "url"), UriKind.Absolute, out var uri) && PlaybookReader.IsFileHost(uri)
            ? uri
            : null;
    }

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
