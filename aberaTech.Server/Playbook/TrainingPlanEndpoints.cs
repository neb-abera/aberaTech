using aberaTech.Scheduling.Admin;

namespace aberaTech.Server.Playbook;

/// <summary>
/// The owner's training plan for /training-plan, read live from one Notion
/// page and its Workouts and Plan notes databases, and a download for each
/// of its files. Read only, and nothing is stored.
/// </summary>
/// <remarks>
/// Notion stays the record. The owner edits the plan there, and the page
/// shows the edit on the next read. Owner only, behind the same policy and
/// the same failure answers as the playbook (<see cref="PlaybookEndpoints"/>).
/// </remarks>
public static class TrainingPlanEndpoints
{
    public const string NotConfigured = "The training plan is not connected. Set Notion__TrainingPlanPageId on the container app and share the page with the integration.";

    public static IEndpointRouteBuilder MapTrainingPlanEndpoints(this IEndpointRouteBuilder routes, NotionOptions options)
    {
        var group = routes
            .MapGroup("/api/training-plan")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Playbook");

        group.MapGet("", (HttpContext context, ILogger<TrainingPlanReader> logger, CancellationToken cancellationToken) =>
            PlaybookEndpoints.Answer(
                options.HasTrainingPlan,
                NotConfigured,
                () => context.RequestServices.GetRequiredService<TrainingPlanReader>(),
                logger,
                async reader =>
                {
                    var plan = await reader.ReadAsync(cancellationToken);
                    return Results.Json(new { configured = true, plan }, PlaybookJson.Options);
                },
                context));

        group.MapGet("/files/{id}", (string id, HttpContext context, NotionClient notion, ILogger<TrainingPlanReader> logger, CancellationToken cancellationToken) =>
            PlaybookEndpoints.Answer(
                options.HasTrainingPlan,
                NotConfigured,
                () => context.RequestServices.GetRequiredService<TrainingPlanReader>(),
                logger,
                async reader =>
                {
                    var file = await reader.FileAsync(id, cancellationToken);
                    if (file is null) return Results.NotFound();

                    var response = await notion.DownloadAsync(file.Address, cancellationToken);
                    context.Response.RegisterForDispose(response);
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return Results.Stream(stream, PlaybookEndpoints.ContentType(response), file.Name);
                },
                context));

        return routes;
    }

    /// <summary>Deployed without sign-in: the page asks and is told so. Anonymous because it discloses nothing.</summary>
    public static IEndpointRouteBuilder MapTrainingPlanUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/training-plan", () => Results.Ok(new { configured = false })).AllowAnonymous();
        return routes;
    }
}
