using System.Threading.RateLimiting;
using aberaTech.Scheduling.Admin;
using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Sms;
using aberaTech.Server.DevBox;

namespace aberaTech.Server;

/// <summary>
/// Every ceiling on what one visitor can ask of the server, in one place.
/// All of them count against <see cref="ClientAddress.PartitionKey(HttpContext)"/>,
/// and all reject outright rather than queue: holding excess requests is
/// itself a way to exhaust the server.
/// </summary>
public static class RateLimits
{
    /// <summary>A route where only failure is suspicious, and how much of it one visitor gets a minute.</summary>
    /// <param name="Route">The route pattern as mapped.</param>
    /// <param name="FailureStatus">The status that route answers a wrong guess with.</param>
    /// <param name="Failures">Wrong guesses a minute before the visitor is refused unheard.</param>
    public sealed record GuessedRoute(string Route, int FailureStatus, int Failures);

    /// <summary>
    /// Routes limited by their failures rather than their traffic.
    /// </summary>
    /// <remarks>
    /// The digest's one legitimate caller presents the right key once a day,
    /// and Twilio's receipts arrive correctly signed and in bursts — every
    /// reminder for a day can land at once. A limit on requests would have to
    /// be loose enough for that burst, which is loose enough to guess through.
    /// A limit on failures costs the real caller nothing, ever, and can
    /// therefore be tight. Once it is spent the visitor is refused before the
    /// route runs — right answers included, or the limit would slow a search
    /// down without ending it.
    /// </remarks>
    public static readonly IReadOnlyList<GuessedRoute> GuessedRoutes =
    [
        new("/api/fitness/digest.txt", StatusCodes.Status401Unauthorized, 10),
        new(SmsReceiptEndpoint.Path, StatusCodes.Status403Forbidden, 30),
        // The dev box reports once a minute with a token; a wrong token is a guess.
        new(DevBoxEndpoints.HeartbeatPath, StatusCodes.Status401Unauthorized, 10)
    ];

    public static IServiceCollection AddAppRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Everything a stranger can call that writes a row or causes a
            // message to be sent. The booking page is public by design, and a
            // public form wired to an SMS provider is a way to spend somebody
            // else's money; this is the second half of that defence, after
            // restricting destinations to +1.
            options.AddPolicy(SchedulingEndpoints.PublicWritePolicy, context => PerMinute(context, 5));

            // Each sign-in attempt mints correlation state and a redirect to
            // Google. A person does it once; ten a minute is a loop.
            options.AddPolicy(AdminAuth.SignInPolicy, context => PerMinute(context, 10));

            // Each press asks Azure to start a VM that bills by the hour. The
            // owner presses once and waits; five a minute is a stuck finger.
            options.AddPolicy(DevBoxEndpoints.StartPolicy, context => PerMinute(context, 5));

            // The box reports once a minute with a token. The failure limit
            // below counts wrong tokens; this one bounds a right token in a
            // loop, or a replay of a captured one.
            options.AddPolicy(DevBoxEndpoints.HeartbeatPolicy, context => PerMinute(context, 10));
        });

    /// <summary>Applies <see cref="GuessedRoutes"/>. After routing, which is what names the route.</summary>
    public static IApplicationBuilder UseGuessLimits(this IApplicationBuilder app)
    {
        var limiters = GuessedRoutes.ToDictionary(
            route => route.Route,
            route => (route.FailureStatus, Limiter: PartitionedRateLimiter.Create<string, string>(key =>
                RateLimitPartition.GetFixedWindowLimiter(key, _ => Window(route.Failures)))),
            StringComparer.OrdinalIgnoreCase);

        return app.Use(async (context, next) =>
        {
            // The pattern the endpoint was mapped with, not the request path:
            // routing ignores case and a trailing slash, and a limit that
            // compared paths itself would be one that "/x/" walks around.
            var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

            if (pattern is null || !limiters.TryGetValue(pattern, out var guarded))
            {
                await next();
                return;
            }

            var key = ClientAddress.PartitionKey(context);

            if (guarded.Limiter.GetStatistics(key) is { CurrentAvailablePermits: <= 0 })
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            await next();

            if (context.Response.StatusCode == guarded.FailureStatus)
            {
                guarded.Limiter.AttemptAcquire(key).Dispose();
            }
        });
    }

    private static RateLimitPartition<string> PerMinute(HttpContext context, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientAddress.PartitionKey(context), _ => Window(permits));

    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    };
}
