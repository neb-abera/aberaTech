using System.Globalization;
using System.Security.Claims;
using aberaTech.Scheduling.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace aberaTech.Scheduling.Admin;

/// <summary>Where each account's session version is kept.</summary>
public interface IAdminSessionVersions
{
    /// <summary>The version a cookie issued now carries. 0 for an account that has never signed out.</summary>
    Task<int> CurrentAsync(string email, CancellationToken cancellationToken);

    /// <summary>Ends every session the account holds, on every device.</summary>
    Task RevokeAsync(string email, CancellationToken cancellationToken);
}

/// <summary>The versions in the scheduling database, one row per account.</summary>
public sealed class DatabaseAdminSessionVersions(SchedulingDbContext database) : IAdminSessionVersions
{
    public async Task<int> CurrentAsync(string email, CancellationToken cancellationToken)
    {
        var key = AdminSessions.Key(email);
        return await database.AdminSessions.AsNoTracking()
            .Where(session => session.Email == key)
            .Select(session => (int?)session.Version)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
    }

    public async Task RevokeAsync(string email, CancellationToken cancellationToken)
    {
        // One statement, so two sign-outs at once both count and neither
        // fails on the key.
        var key = AdminSessions.Key(email);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "AdminSessions" ("Email", "Version") VALUES ({key}, 1)
             ON CONFLICT ("Email") DO UPDATE SET "Version" = "AdminSessions"."Version" + 1
             """,
            cancellationToken);
    }
}

/// <summary>
/// Sign-out that ends the session on the server. The cookie is a signed,
/// encrypted ticket the server keeps no copy of, so deleting it from one
/// browser does nothing to a copy of it anywhere else. Each ticket carries
/// the account's session version from when it was issued. Every request
/// compares it with the current one, and sign-out moves the current one on.
/// </summary>
public static class AdminSessions
{
    public const string VersionClaim = "abera:session-version";

    /// <summary>The row key: the address as the allowlist compares it.</summary>
    public static string Key(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Stamps the ticket being issued with the account's current version.</summary>
    public static async Task StampAsync(CookieSigningInContext context)
    {
        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        var versions = context.HttpContext.RequestServices.GetService<IAdminSessionVersions>();

        // No store (a deployment without the scheduling database) or no
        // address: the ticket goes out unstamped and every request refuses it.
        if (email is null || versions is null || context.Principal!.Identity is not ClaimsIdentity identity) return;

        var version = await versions.CurrentAsync(email, context.HttpContext.RequestAborted);
        identity.AddClaim(new Claim(VersionClaim, version.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Refuses a ticket whose version is not the account's current one.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        var stamped = context.Principal?.FindFirstValue(VersionClaim);
        var versions = services.GetService<IAdminSessionVersions>();

        if (email is null || stamped is null || versions is null
            || !int.TryParse(stamped, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            Refuse(context);
            return;
        }

        int current;
        try
        {
            current = await versions.CurrentAsync(email, context.HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The database is unreachable. Treated as signed out for this
            // request, and the cookie is kept: the session is not over, the
            // answer to "is it still valid" is only unknown. The type alone,
            // for the reason /readyz gives.
            services.GetService<ILoggerFactory>()?.CreateLogger(typeof(AdminSessions))
                .LogWarning("Session version check failed with {Failure}.", exception.GetType().Name);
            context.RejectPrincipal();
            return;
        }

        if (current != version)
        {
            Refuse(context);
        }
    }

    /// <summary>Anonymous for this request, and the dead cookie removed from the browser that sent it.</summary>
    private static void Refuse(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        context.ShouldRenew = false;
        context.Options.CookieManager.DeleteCookie(
            context.HttpContext,
            context.Options.Cookie.Name!,
            context.Options.Cookie.Build(context.HttpContext));
    }
}
