using aberaTech.Scheduling.Admin;

namespace aberaTech.Server.Playbook;

/// <summary>
/// The owner's Notion playbook, read live: the page tree, one page's
/// blocks, and a file block's download. Read only, and nothing is stored.
/// </summary>
/// <remarks>
/// For a work computer that can reach this site and not Notion. Notion
/// stays the record. Owner only, behind the same policy as the dev box.
/// Every id must be under <see cref="NotionOptions.PlaybookPageId"/>, or
/// the answer is 404. A Notion failure is a 502 naming the status or the
/// exception type, never Notion's message. A rate limit Notion will not
/// lift soon is a 503 with its Retry-After.
/// </remarks>
public static class PlaybookEndpoints
{
    public const string NotConfigured = "Notion is not connected. Set Notion__Token and Notion__PlaybookPageId on the container app.";

    public const string LessonsNotConfigured = "The lessons page is not connected. Set Notion__FitnessLessonsPageId on the container app and share the page with the integration.";

    /// <summary>The keyed service name of the reader rooted at the fitness lessons page.</summary>
    public const string LessonsReader = "fitness-lessons";

    public static IEndpointRouteBuilder MapPlaybookEndpoints(this IEndpointRouteBuilder routes, NotionOptions options)
    {
        var group = routes
            .MapGroup("/api/playbook")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Playbook");

        group.MapGet("", (HttpContext context, ILogger<PlaybookReader> logger, CancellationToken cancellationToken) =>
            Answer(options, logger, async reader =>
            {
                var tree = await reader.TreeAsync(cancellationToken);
                return Results.Json(new { configured = true, root = tree.Root }, PlaybookJson.Options);
            }, context));

        group.MapGet("/pages/{id}", (string id, HttpContext context, ILogger<PlaybookReader> logger, CancellationToken cancellationToken) =>
            Answer(options, logger, async reader =>
            {
                if (!NotionIds.TryNormalize(id, out var pageId)) return Results.NotFound();
                var page = await reader.PageAsync(pageId, cancellationToken);
                return page is null ? Results.NotFound() : Results.Json(page, PlaybookJson.Options);
            }, context));

        group.MapGet("/files/{blockId}", (string blockId, HttpContext context, NotionClient notion, ILogger<PlaybookReader> logger, CancellationToken cancellationToken) =>
            Answer(options, logger, async reader =>
            {
                if (!NotionIds.TryNormalize(blockId, out var id)) return Results.NotFound();
                var file = await reader.FileAsync(id, cancellationToken);
                if (file is null) return Results.NotFound();

                var response = await notion.DownloadAsync(file.Address, cancellationToken);
                context.Response.RegisterForDispose(response);
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return Results.Stream(stream, ContentType(response), file.Name);
            }, context));

        return routes;
    }

    /// <summary>
    /// The fitness console's lessons learned: one Notion page, read live,
    /// for the Plan tab. Same owner policy and the same failure answers as
    /// the playbook. Only that page is served.
    /// </summary>
    public static IEndpointRouteBuilder MapFitnessLessonsEndpoints(this IEndpointRouteBuilder routes, NotionOptions options)
    {
        routes.MapGet("/api/fitness/lessons", (HttpContext context, ILogger<PlaybookReader> logger, CancellationToken cancellationToken) =>
                Answer(
                    options.HasFitnessLessons,
                    LessonsNotConfigured,
                    () => context.RequestServices.GetRequiredKeyedService<PlaybookReader>(LessonsReader),
                    logger,
                    async reader =>
                    {
                        var tree = await reader.TreeAsync(cancellationToken);
                        var page = await reader.PageAsync(tree.Root.Id, cancellationToken);
                        return Results.Json(new { configured = true, page }, PlaybookJson.Options);
                    },
                    context))
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Playbook");

        return routes;
    }

    /// <summary>
    /// Deployed without sign-in: there is no owner to show the playbook to.
    /// The page asks and is told so. Anonymous because it discloses nothing.
    /// </summary>
    public static IEndpointRouteBuilder MapPlaybookUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/playbook", () => Results.Ok(new { configured = false })).AllowAnonymous();
        return routes;
    }

    private static Task<IResult> Answer(
        NotionOptions options, ILogger logger, Func<PlaybookReader, Task<IResult>> read, HttpContext context) =>
        Answer(options.IsConfigured, NotConfigured, () => context.RequestServices.GetRequiredService<PlaybookReader>(), logger, read, context);

    private static async Task<IResult> Answer(
        bool configured, string notConfigured, Func<PlaybookReader> reader, ILogger logger, Func<PlaybookReader, Task<IResult>> read, HttpContext context)
    {
        if (!configured)
        {
            return Results.Json(new { configured = false, message = notConfigured });
        }

        try
        {
            return await read(reader());
        }
        catch (NotionThrottledException throttled)
        {
            logger.LogWarning("Notion rate limited the playbook for {Seconds} s", throttled.RetryAfter.TotalSeconds);
            context.Response.Headers.RetryAfter = Math.Ceiling(throttled.RetryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Text("Notion is busy. Try again shortly.", "text/plain", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (NotionException failed)
        {
            logger.LogWarning("Notion answered {Status} for the playbook", (int)failed.Status);
            return Results.Text($"Notion answered {(int)failed.Status}", "text/plain", statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException
                                              or TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Notion could not be read for the playbook: {Type}", exception.GetType().Name);
            return Results.Text(exception.GetType().Name, "text/plain", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>The file's own type, except one a browser would run. It is an attachment either way.</summary>
    private static string ContentType(HttpResponseMessage response)
    {
        var type = response.Content.Headers.ContentType?.MediaType;
        return type is null or "text/html" or "image/svg+xml" or "application/xhtml+xml" or "text/xml" or "application/xml"
            ? "application/octet-stream"
            : type;
    }
}
