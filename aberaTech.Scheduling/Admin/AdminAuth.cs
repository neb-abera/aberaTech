using System.Security.Claims;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NodaTime;

namespace aberaTech.Scheduling.Admin;

/// <summary>Google sign-in, narrowed to the accounts that may run the queue.</summary>
public static class AdminAuth
{
    public const string PolicyName = "scheduling-admin";

    public const string SignInPath = "/api/scheduling/admin/sign-in";

    public const string CookieName = "__Host-abera.admin";

    /// <summary>
    /// What the session cookie was called before it took the __Host- prefix.
    /// A session under this name is simply no longer a session — the host
    /// signs in again, once — and the leftover is expired when seen.
    /// </summary>
    private const string LegacyCookieName = "abera.admin";

    /// <summary>
    /// The rate limiting policy on the sign-in redirect. The host registers
    /// it; named here so the route and the registration cannot drift apart.
    /// </summary>
    public const string SignInPolicy = "scheduling-admin-sign-in";

    public static IServiceCollection AddSchedulingAdminAuth(this IServiceCollection services, AdminOptions options)
    {
        services
            .AddAuthentication(configure =>
            {
                configure.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                // The cookie, not Google. An unauthenticated call to an API
                // endpoint should come back as a status code the caller can act
                // on; challenging Google directly answers a fetch() with a 302
                // to accounts.google.com, which it cannot follow and cannot
                // read. The sign-in endpoint challenges Google explicitly, which
                // is the one place a redirect is the right answer.
                configure.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(cookie =>
            {
                // __Host-: the browser itself then refuses the cookie unless
                // it is Secure, has Path=/ and names no Domain, so neither a
                // sibling subdomain nor a plain-HTTP response can plant or
                // overwrite an admin session. The three settings below are
                // what the prefix demands; drop one and sign-in silently stops
                // working, which AdminCookieTests pins.
                cookie.Cookie.Name = CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.Path = "/";
                cookie.Cookie.Domain = null;

                // Strict, not Lax. These endpoints change state — closing a
                // session, marking somebody a no-show — and with Lax a top level
                // navigation from another site still carries the cookie. Strict
                // means no cross-site request carries it at all, which removes
                // the cross-site request forgery surface rather than mitigating
                // it.
                cookie.Cookie.SameSite = SameSiteMode.Strict;

                cookie.ExpireTimeSpan = TimeSpan.FromHours(12);
                cookie.SlidingExpiration = true;

                // An API, so both failure modes are status codes rather than
                // redirects to a page a fetch() cannot render. 401 means "sign
                // in", 403 means "signed in, but not you" — and the admin page
                // needs to tell those apart to know whether to show the button.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddGoogle("Google", google =>
            {
                google.ClientId = options.GoogleClientId;
                google.ClientSecret = options.GoogleClientSecret;

                // Not in the cookie. A refresh token in a cookie travels to the
                // browser on every request for the life of the session; it is
                // captured server side below and stored encrypted instead.
                google.SaveTokens = false;

                google.Events.OnCreatingTicket = async context =>
                {
                    // The only moment a refresh token exists. Google returns it
                    // once, in this response, and never again — so if it is not
                    // taken here it is gone until the host re-consents.
                    var services = context.HttpContext.RequestServices;

                    await CalendarAdminEndpoints.CaptureRefreshTokenAsync(
                        services.GetRequiredService<SchedulingDbContext>(),
                        services.GetRequiredService<GoogleAccessTokens>(),
                        services.GetRequiredService<IClock>(),
                        context.Principal ?? new ClaimsPrincipal(),
                        context.RefreshToken,
                        CalendarAdminEndpoints.ReadScopes(context.TokenResponse.Response!.RootElement),
                        context.HttpContext.RequestAborted);
                };

                // The address is the whole basis of the allowlist, so it has to
                // be on the principal. The Google handler maps it to
                // ClaimTypes.Email by default; the scope is what makes Google
                // return it in the first place.
                google.Scope.Add("email");
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyName, policy => policy
                .RequireAuthenticatedUser()
                // Authentication proves somebody has a Google account, which
                // everybody does. This is the part that proves it is *his*.
                .RequireAssertion(context => options.Allows(
                    context.User.FindFirstValue(ClaimTypes.Email))));

        return services;
    }

    /// <summary>Sign-in and sign-out, and a way for the page to ask who it is talking to.</summary>
    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder routes, AdminOptions options)
    {
        routes.MapGet(SignInPath, (HttpContext context, string? returnUrl) =>
            Results.Challenge(
                new AuthenticationProperties
                {
                    // Only ever a path on this site. Echoing an arbitrary
                    // returnUrl back into a redirect is the standard open
                    // redirect, and an attacker would use it to bounce a
                    // freshly signed in admin somewhere else.
                    RedirectUri = LocalOrDefault(returnUrl)
                },
                ["Google"]))
            .RequireRateLimiting(SignInPolicy)
            .AllowAnonymous();

        routes.MapPost("/api/scheduling/admin/sign-out", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization(PolicyName);

        routes.MapGet("/api/scheduling/admin/me", (HttpContext context) =>
        {
            // The first call the admin page makes, so the place to tidy up:
            // otherwise the dead cookie rides along on every request until it
            // lapses.
            if (context.Request.Cookies.ContainsKey(LegacyCookieName))
            {
                context.Response.Cookies.Delete(LegacyCookieName, new CookieOptions
                {
                    Path = "/",
                    Secure = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict
                });
            }

            var email = context.User.FindFirstValue(ClaimTypes.Email);

            return Results.Ok(new
            {
                configured = true,
                signedIn = context.User.Identity?.IsAuthenticated == true && options.Allows(email),
                email = options.Allows(email) ? email : null
            });
        }).AllowAnonymous();

        return routes;
    }

    /// <summary>
    /// The identity endpoint alone, for a deployment with no admin credentials.
    /// </summary>
    /// <remarks>
    /// Same reasoning as the public schedule state: without this the route does
    /// not exist, the request falls through to the SPA fallback, and the admin
    /// page gets index.html where it expected JSON. Answering "not configured"
    /// lets the page say so instead of showing a parse error.
    ///
    /// It is safe to answer unauthenticated because it discloses nothing: the
    /// admin surface genuinely is absent on such a deployment.
    /// </remarks>
    public static IEndpointRouteBuilder MapAdminUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/scheduling/admin/me", () =>
            Results.Ok(new { configured = false, signedIn = false, email = (string?)null })).AllowAnonymous();

        return routes;
    }

    /// <summary>A same-site path, or the schedule page.</summary>
    private static string LocalOrDefault(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/schedule";
}
