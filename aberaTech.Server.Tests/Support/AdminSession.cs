using System.Security.Claims;
using aberaTech.Scheduling.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace aberaTech.Server.Tests.Support;

/// <summary>
/// The cookie the Google sign-in would have issued for an account, made with
/// the application's own ticket format, so a request carrying it goes through
/// the real cookie scheme and the real allowlist policy rather than a test
/// double of either.
/// </summary>
/// <remarks>
/// Who the account is decides what it can do: an address on the admin
/// allowlist is the host, an address on the fitness allowlist is the athlete,
/// and any other address is a stranger who happens to own a Google account,
/// which is what the allowlists exist to turn away with a 403.
/// </remarks>
public static class AdminSession
{
    public static string CookieFor(IServiceProvider services, string email)
    {
        var options = services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, "A Person")],
            CookieAuthenticationDefaults.AuthenticationScheme);

        // Stamped as the sign-in would stamp it, with the account's current
        // session version, from whichever store the host has.
        using (var scope = services.CreateScope())
        {
            if (scope.ServiceProvider.GetService<IAdminSessionVersions>() is { } versions)
            {
                var version = versions.CurrentAsync(email, CancellationToken.None).GetAwaiter().GetResult();
                identity.AddClaim(new Claim(
                    AdminSessions.VersionClaim, version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);

        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    /// <summary>A client that presents <paramref name="email"/>'s session on every request.</summary>
    public static HttpClient SignedInAs(this HttpClient client, IServiceProvider services, string email)
    {
        client.DefaultRequestHeaders.Add("Cookie", CookieFor(services, email));
        return client;
    }
}
