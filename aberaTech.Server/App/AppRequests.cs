using System.Globalization;
using System.Text.Json;
using aberaTech.Server.Playbook;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Server.App;

/// <summary>One request on the app, as its requester sees it.</summary>
/// <param name="Status">received, in-progress, done, or not-doing.</param>
/// <param name="Reason">Why it is not being done, from a "Reason:" line in Notes.</param>
/// <param name="FromEmail">It came in by email, through the email routine.</param>
public sealed record AppRequestDto(
    string Id,
    string Title,
    string? Details,
    string? Due,
    string Status,
    string? Reason,
    string? Link,
    bool FromEmail,
    bool Editable,
    string CreatedAt,
    string UpdatedAt);

/// <summary>What the app sends to add or change a request.</summary>
public sealed record AppRequestInput(string? Title, string? Details, string? Due, string? Link);

/// <summary>
/// A household member's requests, read from and written to Neb's Notion
/// Tasks, live. Nothing is kept here.
/// </summary>
/// <remarks>
/// A household member sees the tasks whose Requested by names them: the
/// ones they add here and the ones the email routine files from their
/// mail. They add, edit, and remove their own. Remove never deletes: the
/// task goes to Archived with "Reason: Removed by" and their name, so
/// Neb's record keeps it, and it leaves their list. A task finished more
/// than 30 days ago leaves the list too. A task added here is marked "Got
/// it" in <see cref="AppOptions.ToldProperty"/>, so the routine does not
/// email a receipt for something typed in by hand.
/// Neb asked for this on 2026-11-21.
/// </remarks>
public static class AppRequests
{
    public const string WritePolicy = "app-writes";
    public const int DefaultWritesPerMinute = 30;

    public const int MaxTitle = 200;
    public const int MaxDetails = 2000;
    public const int MaxLink = 2000;

    /// <summary>Finished tasks stay on the list this long after their last change.</summary>
    public static readonly Duration FinishedShownFor = Duration.FromDays(30);

    private const string RemovedPrefix = "Removed by ";

    public static IEndpointRouteBuilder MapAppRequestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/app/requests").RequireAuthorization(AppAuth.SignedInPolicy);

        group.MapGet("", async (HttpContext context, NotionClient notion, AppOptions app, IClock clock, CancellationToken cancellationToken) =>
        {
            if (Setup(context, app) is not { } setup) return Results.Problem(statusCode: 503, title: "Tasks is not connected.");
            var query = new Dictionary<string, object>
            {
                ["filter"] = new { property = "Requested by", select = new { equals = setup.Requester } },
                ["sorts"] = new[] { new { timestamp = "created_time", direction = "descending" } }
            };
            var pages = await Call(() => notion.QueryFreshAsync(setup.DataSource, query, cancellationToken));
            if (pages is null) return Unavailable();

            var since = clock.GetCurrentInstant() - FinishedShownFor;
            return Results.Ok(pages.Select(Read).Where(r => Shown(r, since)).ToList());
        });

        group.MapPost("", async (AppRequestInput input, HttpContext context, NotionClient notion, AppOptions app, CancellationToken cancellationToken) =>
        {
            if (Setup(context, app) is not { } setup) return Results.Problem(statusCode: 503, title: "Tasks is not connected.");
            if (Problem(input) is { } problem) return Invalid(problem);

            var properties = Properties(input);
            properties["Requested by"] = new { select = new { name = setup.Requester } };
            properties["Status"] = new { status = new { name = "Not started" } };
            properties["Area"] = new { select = new { name = "Personal" } };
            properties["Priority"] = new { select = new { name = "P2" } };
            if (!string.IsNullOrWhiteSpace(app.ToldProperty)) properties[app.ToldProperty] = new { select = new { name = "Got it" } };
            var body = new Dictionary<string, object>
            {
                ["parent"] = new { type = "data_source_id", data_source_id = setup.DataSource },
                ["properties"] = properties
            };
            var page = await Call(() => notion.CreatePageAsync(body, cancellationToken));
            return page is null ? Unavailable() : Results.Ok(Read(page.Value));
        }).RequireRateLimiting(WritePolicy);

        group.MapPut("/{id}", async (string id, AppRequestInput input, HttpContext context, NotionClient notion, AppOptions app, CancellationToken cancellationToken) =>
        {
            if (Setup(context, app) is not { } setup) return Results.Problem(statusCode: 503, title: "Tasks is not connected.");
            if (Problem(input) is { } problem) return Invalid(problem);
            if (await Owned(notion, setup, id, cancellationToken) is not { } owned) return Results.NotFound();
            if (!owned.Request.Editable) return Results.Conflict(new { detail = "A finished request cannot be changed." });

            var page = await Call(() => notion.UpdatePageAsync(owned.Id, new Dictionary<string, object> { ["properties"] = Properties(input) }, cancellationToken));
            return page is null ? Unavailable() : Results.Ok(Read(page.Value));
        }).RequireRateLimiting(WritePolicy);

        group.MapPost("/{id}/remove", async (string id, HttpContext context, NotionClient notion, AppOptions app, CancellationToken cancellationToken) =>
        {
            if (Setup(context, app) is not { } setup) return Results.Problem(statusCode: 503, title: "Tasks is not connected.");
            if (await Owned(notion, setup, id, cancellationToken) is not { } owned) return Results.NotFound();

            var notes = owned.Notes is { Length: > 0 } n ? n + "\n" : "";
            var reason = $"Reason: {RemovedPrefix}{setup.Person.Name}";
            var removed = new Dictionary<string, object>
            {
                ["Status"] = new { status = new { name = "Archived" } },
                ["Notes"] = RichText(Trim(notes + reason, MaxDetails + 200))
            };
            if (!string.IsNullOrWhiteSpace(app.ToldProperty)) removed[app.ToldProperty] = new { select = new { name = "Not doing" } };
            var body = new Dictionary<string, object> { ["properties"] = removed };
            var page = await Call(() => notion.UpdatePageAsync(owned.Id, body, cancellationToken));
            return page is null ? Unavailable() : Results.NoContent();
        }).RequireRateLimiting(WritePolicy);

        return routes;
    }

    private sealed record Context(AppPerson Person, string Requester, string DataSource);

    private sealed record OwnedPage(string Id, string? Notes, AppRequestDto Request);

    private static Context? Setup(HttpContext context, AppOptions app)
    {
        if (AppAuth.Person(context.User) is not { } person) return null;
        if (!NotionIds.TryNormalize(app.TasksDataSourceId, out var dataSource)) return null;
        return AppPeople.Requester(person, app) is { } requester ? new Context(person, requester, dataSource) : null;
    }

    /// <summary>The page, when it is in Tasks and its requester is the caller's. Anyone else's is not found.</summary>
    private static async Task<OwnedPage?> Owned(NotionClient notion, Context setup, string id, CancellationToken cancellationToken)
    {
        if (!NotionIds.TryNormalize(id, out var pageId)) return null;
        JsonElement page;
        try
        {
            page = await notion.PageFreshAsync(pageId, cancellationToken);
        }
        catch (NotionException)
        {
            return null;
        }

        var parent = page.TryGetProperty("parent", out var p) && p.TryGetProperty("data_source_id", out var ds) ? ds.GetString() : null;
        if (!NotionIds.TryNormalize(parent, out var parentId) || parentId != setup.DataSource) return null;
        if (Select(page, "Requested by") != setup.Requester) return null;
        return new OwnedPage(pageId, Text(page, "Notes"), Read(page));
    }

    internal static AppRequestDto Read(JsonElement page)
    {
        var notes = Text(page, "Notes");
        var status = page.TryGetProperty("properties", out var props) && props.TryGetProperty("Status", out var s)
                     && s.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Object
            ? st.GetProperty("name").GetString()
            : null;
        var shown = status switch
        {
            "In progress" => "in-progress",
            "Done" => "done",
            "Archived" => "not-doing",
            _ => "received"
        };
        var reasonLine = notes?.Split('\n').LastOrDefault(line => line.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase));
        var details = notes is null
            ? null
            : string.Join('\n', notes.Split('\n').Where(line => !line.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))).Trim();

        return new AppRequestDto(
            Id: page.GetProperty("id").GetString() ?? string.Empty,
            Title: Text(page, "Task name") ?? "(untitled)",
            Details: string.IsNullOrEmpty(details) ? null : details,
            Due: DateStart(page, "Due"),
            Status: shown,
            Reason: shown == "not-doing" ? reasonLine?["Reason:".Length..].Trim() : null,
            Link: Url(page, "Source"),
            FromEmail: !string.IsNullOrEmpty(Text(page, "Gmail thread")),
            Editable: shown is "received" or "in-progress",
            CreatedAt: page.TryGetProperty("created_time", out var c) ? c.GetString() ?? "" : "",
            UpdatedAt: page.TryGetProperty("last_edited_time", out var u) ? u.GetString() ?? "" : "");
    }

    /// <summary>Open ones always. Finished ones for 30 days. Ones the requester removed, never.</summary>
    internal static bool Shown(AppRequestDto request, Instant since)
    {
        if (request.Status == "not-doing" && request.Reason?.StartsWith(RemovedPrefix, StringComparison.Ordinal) == true) return false;
        if (request.Status is "received" or "in-progress") return true;
        return InstantPattern.ExtendedIso.Parse(request.UpdatedAt) is { Success: true } edited && edited.Value >= since;
    }

    internal static string? Problem(AppRequestInput input)
    {
        var title = input.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return "A request needs a title.";
        if (title.Length > MaxTitle) return $"A title holds at most {MaxTitle} characters.";
        if (input.Details is { Length: > MaxDetails }) return $"Details hold at most {MaxDetails} characters.";
        if (!string.IsNullOrWhiteSpace(input.Due) && !LocalDatePattern.Iso.Parse(input.Due.Trim()).Success) return "The date is not a date.";
        if (!string.IsNullOrWhiteSpace(input.Link)
            && (input.Link.Length > MaxLink
                || !Uri.TryCreate(input.Link.Trim(), UriKind.Absolute, out var link)
                || link.Scheme is not ("https" or "http")))
        {
            return "The link is not a web address.";
        }

        return null;
    }

    private static Dictionary<string, object> Properties(AppRequestInput input)
    {
        var details = input.Details?.Trim();
        var due = string.IsNullOrWhiteSpace(input.Due) ? null : input.Due.Trim();
        var link = string.IsNullOrWhiteSpace(input.Link) ? null : input.Link.Trim();
        return new Dictionary<string, object>
        {
            ["Task name"] = new { title = new[] { new { text = new { content = input.Title!.Trim() } } } },
            ["Notes"] = RichText(string.IsNullOrEmpty(details) ? null : details),
            ["Due"] = new { date = due is null ? null : new { start = due } },
            ["Source"] = new { url = link }
        };
    }

    private static object RichText(string? text) =>
        new { rich_text = text is null ? Array.Empty<object>() : Chunks(text).Select(part => (object)new { text = new { content = part } }).ToArray() };

    /// <summary>Notion holds at most 2,000 characters in one text object.</summary>
    private static IEnumerable<string> Chunks(string text)
    {
        for (var i = 0; i < text.Length; i += 2000) yield return text.Substring(i, Math.Min(2000, text.Length - i));
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[^max..];

    private static JsonElement? Property(JsonElement page, string name) =>
        page.TryGetProperty("properties", out var props) && props.TryGetProperty(name, out var value) ? value : null;

    internal static string? Text(JsonElement page, string name)
    {
        if (Property(page, name) is not { } value) return null;
        var type = value.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type is null || !value.TryGetProperty(type, out var parts) || parts.ValueKind != JsonValueKind.Array) return null;
        var text = string.Concat(parts.EnumerateArray().Select(part => part.TryGetProperty("plain_text", out var plain) ? plain.GetString() : ""));
        return string.IsNullOrEmpty(text) ? null : text;
    }

    internal static string? Select(JsonElement page, string name) =>
        Property(page, name) is { } value && value.TryGetProperty("select", out var select) && select.ValueKind == JsonValueKind.Object
            ? select.GetProperty("name").GetString()
            : null;

    internal static string? Url(JsonElement page, string name) =>
        Property(page, name) is { } value && value.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String
            ? url.GetString()
            : null;

    internal static string? DateStart(JsonElement page, string name) =>
        Property(page, name) is { } value && value.TryGetProperty("date", out var date) && date.ValueKind == JsonValueKind.Object
        && date.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String
            ? start.GetString()?[..Math.Min(10, start.GetString()!.Length)]
            : null;

    internal static double? Number(JsonElement page, string name) =>
        Property(page, name) is { } value && value.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.Number
            ? number.GetDouble()
            : null;

    private static async Task<T?> Call<T>(Func<Task<T>> call) where T : struct
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (exception is NotionException or NotionThrottledException or HttpRequestException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<JsonElement>?> Call(Func<Task<IReadOnlyList<JsonElement>>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (exception is NotionException or NotionThrottledException or HttpRequestException)
        {
            return null;
        }
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 502, title: "Notion did not answer. Try again.");

    private static IResult Invalid(string problem) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [problem] });

    internal static string Iso(LocalDate date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
