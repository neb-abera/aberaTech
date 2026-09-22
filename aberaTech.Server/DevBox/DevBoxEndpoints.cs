using System.Security.Cryptography;
using System.Text;
using aberaTech.Scheduling.Admin;

namespace aberaTech.Server.DevBox;

/// <summary>
/// The owner's dev box: its power state, what its agent last reported, and
/// the buttons: Start, Hold, Park.
/// </summary>
/// <remarks>
/// The box deallocates itself after 30 idle minutes, and nothing wakes it.
/// On 2026-09-21 every Remote Control session on the phone died with it, and
/// the only ways back were a laptop or the Azure app. This is the way back
/// from the phone: sign in to the site, press Start.
///
/// Start goes through Azure with the container app's identity. Hold and Park
/// go through the box's own heartbeat (<see cref="DevBoxAgentState"/>): the
/// box asks once a minute and does what the reply says. Owner only, behind
/// the same policy as the queue, except the heartbeat itself, which the box
/// authenticates with a shared token. A failure talking to Azure is a 502
/// with the exception type, never its message: the message can name the
/// subscription.
/// </remarks>
public static class DevBoxEndpoints
{
    public const string StartPolicy = "devbox-start";

    public const string HeartbeatPath = "/api/devbox/heartbeat";

    /// <summary>The longest a Hold may keep the box up: one working day.</summary>
    public const int MaxHoldMinutes = 12 * 60;

    public static IEndpointRouteBuilder MapDevBoxEndpoints(this IEndpointRouteBuilder routes, DevBoxOptions options)
    {
        var group = routes
            .MapGroup("/api/devbox")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Dev box");

        group.MapGet("/status", async (DevBoxClient client, DevBoxAgentState agent, ILogger<DevBoxClient> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                var power = await client.GetPowerStateAsync(cancellationToken);
                return Results.Ok(new { configured = true, power, agent = Agent(agent, options) });
            }
            catch (Exception exception) when (IsAzure(exception))
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
            catch (Exception exception) when (IsAzure(exception))
            {
                logger.LogWarning(exception, "Dev box start failed");
                return Results.Text(exception.GetType().Name, "text/plain", statusCode: StatusCodes.Status502BadGateway);
            }
        }).RequireRateLimiting(StartPolicy);

        if (options.HasHeartbeat)
        {
            group.MapPost("/hold", (HoldRequest request, DevBoxAgentState agent) =>
            {
                if (request.Minutes is < 1 or > MaxHoldMinutes)
                {
                    return Results.BadRequest($"minutes must be 1 to {MaxHoldMinutes}");
                }

                agent.Hold(request.Minutes);
                return Results.Accepted(value: agent.Pending());
            }).RequireRateLimiting(StartPolicy);

            group.MapPost("/park", (DevBoxAgentState agent) =>
            {
                agent.Park();
                return Results.Accepted(value: agent.Pending());
            }).RequireRateLimiting(StartPolicy);

            // The box, not the owner: a bearer token in place of a session.
            // Anonymous as far as the cookie scheme is concerned, and the
            // GuessedRoutes limit in RateLimits.cs counts its 401s.
            routes.MapPost(HeartbeatPath, (HttpRequest request, DevBoxHeartbeat heartbeat, DevBoxAgentState agent) =>
            {
                var header = request.Headers.Authorization.ToString();
                if (!header.StartsWith("Bearer ", StringComparison.Ordinal)
                    || !FixedTimeEquals(header["Bearer ".Length..].Trim(), options.HeartbeatToken!))
                {
                    return Results.Unauthorized();
                }

                agent.Report(heartbeat);
                return Results.Ok(agent.Take());
            }).AllowAnonymous();
        }

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

    private static object? Agent(DevBoxAgentState agent, DevBoxOptions options)
    {
        if (!options.HasHeartbeat) return null;

        var last = agent.Last();
        if (last is null) return new { seen = false, pending = agent.Pending() };

        var (heartbeat, ago) = last.Value;
        return new
        {
            seen = true,
            seenSecondsAgo = Math.Round(ago),
            heartbeat.RemoteControl,
            heartbeat.Sessions,
            heartbeat.Load,
            heartbeat.UptimeSeconds,
            heartbeat.HoldUntil,
            heartbeat.EnvironmentUrl,
            pending = agent.Pending()
        };
    }

    private static bool IsAzure(Exception exception) =>
        exception is HttpRequestException or Azure.RequestFailedException or Azure.Identity.CredentialUnavailableException;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public sealed record HoldRequest(int Minutes);
}
