using System.Text.Json;
using System.Text.Json.Serialization;

namespace aberaTech.Server.Playbook;

/// <summary>A page or a database in the tree. A database is a heading over its pages, not something to open.</summary>
public sealed record PlaybookNode(string Id, string Title, string Kind, IReadOnlyList<PlaybookNode> Children)
{
    public const string Page = "page";
    public const string Database = "database";
}

/// <summary>A run of text with one set of marks. <c>Href</c> is a link, already pointed at this site for a playbook page.</summary>
public sealed record PlaybookText(string Text)
{
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public bool? Strikethrough { get; init; }
    public bool? Underline { get; init; }
    public bool? Code { get; init; }
    public string? Href { get; init; }
}

/// <summary>
/// One block of a page, in the few shapes the page draws. Only the fields
/// a type uses are set; the rest are left out of the JSON.
/// </summary>
/// <remarks>
/// Types: heading (Level 1 to 3), paragraph, bulleted, numbered, todo
/// (Checked), toggle, quote, callout (Icon), code (Language), divider,
/// table (Rows, HeaderRow, HeaderColumn), page (Id, Title), database (Id,
/// Title, Pages), file (Id, Name, Kind: file, pdf, image, video or audio;
/// Url only when the file lives outside Notion), link (Url), equation, and
/// unsupported (NotionType). Children nest under any of them.
/// </remarks>
public sealed record PlaybookBlock(string Type)
{
    public string? Id { get; init; }
    public int? Level { get; init; }
    public IReadOnlyList<PlaybookText>? Text { get; init; }
    public bool? Checked { get; init; }
    public string? Icon { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
    public string? Name { get; init; }
    public string? Kind { get; init; }
    public string? Url { get; init; }
    public IReadOnlyList<PlaybookText>? Caption { get; init; }
    public bool? HeaderRow { get; init; }
    public bool? HeaderColumn { get; init; }
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<PlaybookText>>>? Rows { get; init; }
    public IReadOnlyList<PlaybookNode>? Pages { get; init; }
    public string? NotionType { get; init; }
    public IReadOnlyList<PlaybookBlock>? Children { get; init; }
}

public sealed record PlaybookPage(string Id, string Title, IReadOnlyList<PlaybookBlock> Blocks);

internal static class PlaybookJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
