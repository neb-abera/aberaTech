using System.Text.RegularExpressions;
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
public static partial class DevBoxEndpoints
{
    public const string StartPolicy = "devbox-start";

    /// <summary>The box reports once a minute. Ten a minute per address is a loop, not the box.</summary>
    public const string HeartbeatPolicy = "devbox-heartbeat";

    public const string HeartbeatPath = "/api/devbox/heartbeat";

    private const string DevBoxPage = "https://abera.tech/devbox";

    public const string NotifyPath = "/api/devbox/notify";

    /// <summary>The longest a Hold may keep the box up: one working day.</summary>
    public const int MaxHoldMinutes = 12 * 60;

    public static IEndpointRouteBuilder MapDevBoxEndpoints(this IEndpointRouteBuilder routes, DevBoxOptions options)
    {
        var group = routes
            .MapGroup("/api/devbox")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Dev box");

        group.MapGet("/status", async (IDevBoxClient client, DevBoxAgentState agent, ILogger<DevBoxClient> logger, CancellationToken cancellationToken) =>
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

        group.MapPost("/start", async (IDevBoxClient client, ILogger<DevBoxClient> logger, CancellationToken cancellationToken) =>
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

            group.MapPost("/park", (DevBoxAgentState agent, IDevBoxClient client) =>
            {
                agent.Park();
                // The real box parks itself on its next report. The fake one
                // has no box behind it, so it parks now.
                (client as FakeDevBoxClient)?.Park();
                return Results.Accepted(value: agent.Pending());
            }).RequireRateLimiting(StartPolicy);

            group.MapPost("/login", (DevBoxAgentState agent) =>
            {
                agent.Login();
                return Results.Accepted(value: agent.Pending());
            }).RequireRateLimiting(StartPolicy);

            group.MapPost("/login/code", (LoginCodeRequest request, DevBoxAgentState agent) =>
            {
                var code = request.Code?.Trim() ?? "";
                if (!LoginCodeShape().IsMatch(code))
                {
                    return Results.BadRequest("That is not a sign-in code.");
                }

                agent.LoginCode(code);
                return Results.Accepted(value: agent.Pending());
            }).RequireRateLimiting(StartPolicy);

            // The box, not the owner: a bearer token in place of a session.
            // Anonymous as far as the cookie scheme is concerned, and the
            // GuessedRoutes limit in RateLimits.cs counts its 401s.
            routes.MapPost(HeartbeatPath, async (HttpRequest request, DevBoxHeartbeat heartbeat, DevBoxAgentState agent, DevBoxNotifier notifier) =>
            {
                if (!Authorized(request, options)) return Results.Unauthorized();

                var (claude, google) = agent.Report(heartbeat);
                if (claude)
                {
                    await notifier.SendAsync(
                        new DevBoxNotice("failure", "Dev box: Claude login expired", "Remote Control is down. Open /devbox and tap Sign in again."),
                        DevBoxPage,
                        request.HttpContext.RequestAborted);
                }

                if (google)
                {
                    await notifier.SendAsync(
                        new DevBoxNotice("failure", "Dev box: Google signed out", "The box's Chrome lost its Google sign-in. Sign in on the box's desktop."),
                        DevBoxPage,
                        request.HttpContext.RequestAborted);
                }

                return Results.Ok(agent.Take());
            }).RequireRateLimiting(HeartbeatPolicy).AllowAnonymous();

            // A session on the box telling the owner's phone it finished or
            // needs him. Pushover's secrets stay here, off the box.
            routes.MapPost(NotifyPath, async (HttpRequest request, DevBoxNotice notice, DevBoxAgentState agent, DevBoxNotifier notifier) =>
            {
                if (!Authorized(request, options)) return Results.Unauthorized();

                await notifier.SendAsync(notice, agent.Last()?.Heartbeat.EnvironmentUrl, request.HttpContext.RequestAborted);
                return Results.Accepted();
            }).RequireRateLimiting(HeartbeatPolicy).AllowAnonymous();
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
            heartbeat.ClaudeLogin,
            heartbeat.GoogleLogin,
            heartbeat.LoginExpiresAt,
            LoginUrl = IsClaudeSignIn(heartbeat.LoginUrl) ? heartbeat.LoginUrl : null,
            pending = agent.Pending()
        };
    }

    /// <summary>The page shows this as a link, so only Claude's own sign-in page passes.</summary>
    private static bool IsClaudeSignIn(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && parsed.Scheme == Uri.UriSchemeHttps
        && parsed.Host is "claude.com" or "claude.ai"
        && parsed.AbsolutePath.EndsWith("/oauth/authorize", StringComparison.Ordinal);

    private static bool Authorized(HttpRequest request, DevBoxOptions options)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal)
               && FixedTimeEquals(header["Bearer ".Length..].Trim(), options.HeartbeatToken!);
    }

    private static bool IsAzure(Exception exception) =>
        exception is HttpRequestException or Azure.RequestFailedException or Azure.Identity.CredentialUnavailableException;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public sealed record HoldRequest(int Minutes);

    public sealed record LoginCodeRequest(string? Code);

    [GeneratedRegex("^[A-Za-z0-9_#-]{8,512}$")]
    private static partial Regex LoginCodeShape();
}
