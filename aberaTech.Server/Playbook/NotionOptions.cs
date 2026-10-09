namespace aberaTech.Server.Playbook;

/// <summary>
/// Where the playbook lives in Notion. The "Notion" section.
/// </summary>
/// <remarks>
/// The token is a container app secret (Notion__Token, a secret reference)
/// and the root page id a plain setting (Notion__PlaybookPageId). Either
/// missing and the page says Notion is not connected. The server starts
/// either way.
/// </remarks>
public sealed class NotionOptions
{
    public const string Section = "Notion";

    /// <summary>An internal integration's secret, shared with the playbook root page in Notion.</summary>
    public string? Token { get; init; }

    /// <summary>The root page. Every page and file the endpoints serve sits under it.</summary>
    public string? PlaybookPageId { get; init; }

    /// <summary>The API's origin. A test points it at a fake.</summary>
    public string BaseUrl { get; init; } = "https://api.notion.com";

    /// <summary>How long a Notion answer is reused. Files are always fetched fresh.</summary>
    public int CacheSeconds { get; init; } = 300;

    /// <summary>
    /// Development only: a Notion in memory with made-up pages
    /// (<see cref="FakeNotion"/>), so `make e2e` can read and download
    /// without a workspace. Ignored outside Development.
    /// </summary>
    public bool Fake { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Token) && NotionIds.TryNormalize(PlaybookPageId, out _);
}

/// <summary>Notion ids are UUIDs, written with or without dashes. One spelling is used everywhere here.</summary>
public static class NotionIds
{
    public static bool TryNormalize(string? value, out string id)
    {
        if (Guid.TryParse(value, out var guid))
        {
            id = guid.ToString("D");
            return true;
        }

        id = "";
        return false;
    }
}
