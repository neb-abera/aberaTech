using aberaTech.Scheduling.Sms;

namespace aberaTech.Server;

/// <summary>
/// The security log: who was refused what, under ids an alert can be written
/// against.
/// </summary>
/// <remarks>
/// Written from outcomes, in one middleware, rather than from inside each
/// handler. The route and the status code between them already say what
/// happened — a 401 from the digest is a rejected key, a 403 from the webhook
/// is a bad signature, a 403 for a signed-in caller is the allowlist — and
/// reading them here means a handler cannot forget to log, a new route on the
/// same footing is covered when it is mapped, and the feature libraries stay
/// free of logging policy.
///
/// What a line carries is fixed by <see cref="Log"/>: the resolved client
/// address (<see cref="ClientAddress"/>), the method, the route *pattern* and
/// the status. Never the path, because the path of a queue or booking route
/// contains the capability id; never the query string, a header, a cookie, a
/// body or the signed-in user. The bar is the one LoggingMessageSender set for
/// phone numbers: a log is the easiest place for personal data to end up
/// somewhere it was never meant to go.
///
/// The event ids are a contract with whoever writes alerts. Add; never renumber.
/// </remarks>
public static partial class SecurityEvents
{
    public const string Category = "aberaTech.Security";

    /// <summary>429: a rate limit or a failed-guess limit refused the request.</summary>
    public const int RateLimited = 4001;

    /// <summary>401 from a route behind sign-in: no session, or an expired one.</summary>
    public const int SignInRequired = 4002;

    /// <summary>403 for a signed-in caller: a Google account the allowlist does not name.</summary>
    public const int AllowlistRefused = 4003;

    /// <summary>401 from the digest: a missing or wrong bearer key.</summary>
    public const int DigestKeyRejected = 4004;

    /// <summary>403 from the Twilio webhook: a missing or wrong signature.</summary>
    public const int WebhookSignatureRejected = 4005;

    /// <summary>404 from a route whose id is its capability: a guessed, stale or mistyped id.</summary>
    public const int UnknownCapability = 4006;

    /// <summary>400 from a public write: input the form would not have sent.</summary>
    public const int PublicWriteRefused = 4007;

    /// <summary>401 from the dev box heartbeat: a missing or wrong agent token.</summary>
    public const int AgentTokenRejected = 4008;

    /// <summary>The owner's session began: the sign-in completed and the cookie was issued.</summary>
    public const int OwnerSignedIn = 4009;

    /// <summary>The owner's session ended by sign-out.</summary>
    public const int OwnerSignedOut = 4010;

    /// <summary>
    /// Sign-in and sign-out as events. The application STIG (V-222462,
    /// V-222464) wants a record of when a session starts and ends, with
    /// the source address; the refusal events above never see a success.
    /// Hooked on the cookie scheme's own events, after the scheme is
    /// configured, so it holds whichever sign-in issued the cookie.
    /// </summary>
    public static IServiceCollection AddSessionAudit(this IServiceCollection services)
    {
        services
            .AddOptions<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .PostConfigure<ILoggerFactory>((options, loggers) =>
            {
                var logger = loggers.CreateLogger(Category);
                options.Events.OnSignedIn = context =>
                {
                    Log.OwnerSignedIn(logger, ClientAddress.For(context.HttpContext));
                    return Task.CompletedTask;
                };
                options.Events.OnSigningOut = context =>
                {
                    Log.OwnerSignedOut(logger, ClientAddress.For(context.HttpContext));
                    return Task.CompletedTask;
                };
            });

        return services;
    }

    private const string DigestRoute = "/api/fitness/digest.txt";

    /// <summary>The routes where holding the id is the whole of the authorization.</summary>
    private static readonly HashSet<string> CapabilityRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/scheduling/queue/{entryId:guid}",
        "/api/scheduling/book/{appointmentId:guid}"
    };

    private static readonly HashSet<string> PublicWriteRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/scheduling/queue",
        "/api/scheduling/book"
    };

    /// <summary>
    /// After routing, so the route pattern is known, and before the rate
    /// limiter and authentication, so their refusals pass back through here.
    /// </summary>
    public static IApplicationBuilder UseSecurityEvents(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);

        return app.Use(async (context, next) =>
        {
            await next();

            var status = context.Response.StatusCode;
            if (status < 400 || status >= 500) return;

            // No endpoint means the SPA fallback or a static file: a 404 for a
            // page that does not exist is not a security event.
            if ((context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText is not { } route) return;

            var signedIn = context.User.Identity?.IsAuthenticated == true;

            if (Classify(route, context.Request.Method, status, signedIn) is { } eventId)
            {
                Write(logger, eventId, ClientAddress.For(context), context.Request.Method, route, status);
            }
        });
    }

    /// <summary>Which event, if any, an outcome is. Pure, so the table is testable on its own.</summary>
    public static int? Classify(string route, string method, int status, bool signedIn) => status switch
    {
        StatusCodes.Status429TooManyRequests => RateLimited,
        StatusCodes.Status401Unauthorized when Is(route, DigestRoute) => DigestKeyRejected,
        StatusCodes.Status401Unauthorized when Is(route, DevBox.DevBoxEndpoints.HeartbeatPath) => AgentTokenRejected,
        StatusCodes.Status401Unauthorized => SignInRequired,
        StatusCodes.Status403Forbidden when Is(route, SmsReceiptEndpoint.Path) => WebhookSignatureRejected,
        StatusCodes.Status403Forbidden when signedIn => AllowlistRefused,
        StatusCodes.Status404NotFound when CapabilityRoutes.Contains(route) => UnknownCapability,
        StatusCodes.Status400BadRequest when PublicWriteRoutes.Contains(route) && HttpMethods.IsPost(method) => PublicWriteRefused,
        _ => null
    };

    private static bool Is(string route, string expected) =>
        string.Equals(route, expected, StringComparison.OrdinalIgnoreCase);

    private static void Write(ILogger logger, int eventId, string clientIp, string method, string route, int status)
    {
        switch (eventId)
        {
            case RateLimited: Log.RateLimited(logger, clientIp, method, route, status); break;
            case SignInRequired: Log.SignInRequired(logger, clientIp, method, route, status); break;
            case AllowlistRefused: Log.AllowlistRefused(logger, clientIp, method, route, status); break;
            case DigestKeyRejected: Log.DigestKeyRejected(logger, clientIp, method, route, status); break;
            case WebhookSignatureRejected: Log.WebhookSignatureRejected(logger, clientIp, method, route, status); break;
            case UnknownCapability: Log.UnknownCapability(logger, clientIp, method, route, status); break;
            case PublicWriteRefused: Log.PublicWriteRefused(logger, clientIp, method, route, status); break;
            case AgentTokenRejected: Log.AgentTokenRejected(logger, clientIp, method, route, status); break;
        }
    }

    /// <summary>
    /// Source-generated, so each event has a fixed id, name and shape, and so
    /// the only values that can ever be logged are the four in the signature.
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(EventId = SecurityEvents.RateLimited, EventName = nameof(RateLimited), Level = LogLevel.Warning,
            Message = "Rate limit refused {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void RateLimited(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.SignInRequired, EventName = nameof(SignInRequired), Level = LogLevel.Information,
            Message = "No valid session from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void SignInRequired(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.AllowlistRefused, EventName = nameof(AllowlistRefused), Level = LogLevel.Warning,
            Message = "A signed-in account that is not on the allowlist was refused from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void AllowlistRefused(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.DigestKeyRejected, EventName = nameof(DigestKeyRejected), Level = LogLevel.Warning,
            Message = "Digest key rejected from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void DigestKeyRejected(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.WebhookSignatureRejected, EventName = nameof(WebhookSignatureRejected), Level = LogLevel.Warning,
            Message = "Webhook signature rejected from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void WebhookSignatureRejected(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.UnknownCapability, EventName = nameof(UnknownCapability), Level = LogLevel.Warning,
            Message = "Unknown id presented from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void UnknownCapability(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.PublicWriteRefused, EventName = nameof(PublicWriteRefused), Level = LogLevel.Information,
            Message = "Public write refused as malformed from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void PublicWriteRefused(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.AgentTokenRejected, EventName = nameof(AgentTokenRejected), Level = LogLevel.Warning,
            Message = "Dev box agent token rejected from {ClientIp} on {Method} {Route} ({Status}).")]
        public static partial void AgentTokenRejected(ILogger logger, string clientIp, string method, string route, int status);

        [LoggerMessage(EventId = SecurityEvents.OwnerSignedIn, EventName = nameof(OwnerSignedIn), Level = LogLevel.Information,
            Message = "Owner signed in from {ClientIp}.")]
        public static partial void OwnerSignedIn(ILogger logger, string clientIp);

        [LoggerMessage(EventId = SecurityEvents.OwnerSignedOut, EventName = nameof(OwnerSignedOut), Level = LogLevel.Information,
            Message = "Owner signed out from {ClientIp}.")]
        public static partial void OwnerSignedOut(ILogger logger, string clientIp);
    }
}
