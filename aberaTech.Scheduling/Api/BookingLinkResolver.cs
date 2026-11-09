using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace aberaTech.Scheduling.Api;

/// <summary>The rules a request books under, and the link they came from.</summary>
/// <param name="LinkId">The link's id, or null for the default rules.</param>
public sealed record ResolvedPolicy(Guid? LinkId, BookingPolicy Policy);

/// <summary>
/// Finds the rules for the booking link a request carries.
/// </summary>
/// <remarks>
/// The key travels in a header. The page reads it from the part of the
/// address after #, which a browser never sends anywhere, so the key stays
/// out of Cloudflare's logs, the server's logs and other sites' Referer.
///
/// An unknown key, a key for a link that is off, and no key at all get the
/// same answer: the default rules. A visitor cannot tell a wrong key from no
/// key, so guessing learns nothing.
/// </remarks>
public static class BookingLinkResolver
{
    public const string Header = "X-Booking-Link";

    /// <summary>The rules from the deployment's settings, used until the host saves default rules.</summary>
    public static BookingPolicy SettingsDefault(SchedulingOptions options) =>
        new(null, options.HorizonDays, options.BookingLeadMinutes, options.DefaultAppointmentMinutes, true);

    public static async Task<ResolvedPolicy> ResolveAsync(
        HttpRequest request,
        SchedulingDbContext database,
        SchedulingOptions options,
        CancellationToken cancellationToken)
    {
        var hash = BookingLinkKeys.HashOrNull(request.Headers[Header].ToString());

        // One read for both: the link the key names, and the default row in
        // case it names none.
        var candidates = await database.BookingLinks
            .AsNoTracking()
            .Where(link => link.IsDefault || (hash != null && link.KeyHash == hash && link.Active))
            .ToListAsync(cancellationToken);

        var named = hash is null ? null : candidates.FirstOrDefault(link => link.KeyHash == hash);
        if (named is not null)
        {
            return new ResolvedPolicy(named.Id, named.ToPolicy());
        }

        var fallback = candidates.FirstOrDefault(link => link.IsDefault);

        return fallback is null
            ? new ResolvedPolicy(null, SettingsDefault(options))
            : new ResolvedPolicy(null, fallback.ToPolicy());
    }
}
