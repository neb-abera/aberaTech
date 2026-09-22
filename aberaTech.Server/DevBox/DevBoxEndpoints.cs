using aberaTech.Scheduling.Admin;

namespace aberaTech.Server.DevBox;

/// <summary>
/// The owner's dev box: its power state, and a button that starts it.
/// </summary>
/// <remarks>
/// The box deallocates itself after 30 idle minutes and at 03:00 UTC, and
/// nothing wakes it. On 2026-09-21 every Remote Control session on the phone
/// died with it, and the only ways back were a laptop or the Azure app. This
/// is the way back from the phone: sign in to the site, press Start.
///
/// Owner only, behind the same policy as the queue. A failure talking to
/// Azure is a 502 with the exception type, never its message: the message
/// can name the subscription.
/// </remarks>
public static class DevBoxEndpoints
{
    public const string StartPolicy = "devbox-start";

    public static IEndpointRouteBuilder MapDevBoxEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/devbox")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Dev box");

        group.MapGet("/status", async (DevBoxClient client, ILogger<DevBoxClient> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                var power = await client.GetPowerStateAsync(cancellationToken);
                return Results.Ok(new { configured = true, power });
            }
            catch (Exception exception) when (exception is HttpRequestException or Azure.RequestFailedException or Azure.Identity.CredentialUnavailableException)
            {
                logger.LogWarning(exception, "Dev box status failed");
                return Results.Text(exception.GetType().Name, "text/plain", statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapPost("/start", async (DevBoxClient client, ILogger<DevBoxClient> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                await client.StartAsync(cancellationToken);
                logger.LogInformation("Dev box start requested");
                return Results.Accepted(value: new { configured = true, power = "starting" });
            }
            catch (Exception exception) when (exception is HttpRequestException or Azure.RequestFailedException or Azure.Identity.CredentialUnavailableException)
            {
                logger.LogWarning(exception, "Dev box start failed");
                return Results.Text(exception.GetType().Name, "text/plain", statusCode: StatusCodes.Status502BadGateway);
            }
        }).RequireRateLimiting(StartPolicy);

        return routes;
    }

    /// <summary>
    /// Deployed without a subscription, or without sign-in: the page asks and
    /// is told so, rather than getting the SPA shell where it expected JSON.
    /// Anonymous because it discloses nothing: the feature genuinely is absent.
    /// </summary>
    public static IEndpointRouteBuilder MapDevBoxUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/devbox/status", () =>
            Results.Ok(new { configured = false, power = (string?)null })).AllowAnonymous();

        return routes;
    }
}
