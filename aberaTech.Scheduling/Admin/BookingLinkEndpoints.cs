using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Admin;

/// <summary>A link's rules as the editor sends them.</summary>
/// <param name="EarliestDate">"yyyy-MM-dd" on the host's calendar, or empty for today.</param>
public sealed record BookingLinkRules(
    string? Name,
    string? EarliestDate,
    int HorizonDays,
    int LeadMinutes,
    int AppointmentMinutes,
    bool ShowsQueue,
    bool Active = true);

/// <summary>A link as the host sees it, with its key so the page can show the link.</summary>
/// <param name="Key">The key, for the owner alone. Null for the default row, a revoked link, and a link made before keys were kept.</param>
/// <param name="IssuedAt">When the current key was issued.</param>
/// <param name="RevokedAt">When the link was revoked, or null.</param>
public sealed record BookingLinkView(
    Guid? Id,
    string Name,
    bool IsDefault,
    string? EarliestDate,
    int HorizonDays,
    int LeadMinutes,
    int AppointmentMinutes,
    bool ShowsQueue,
    bool Active,
    int Bookings,
    string? Key = null,
    string? IssuedAt = null,
    string? RevokedAt = null);

public sealed record BookingLinkList(BookingLinkView Default, IReadOnlyList<BookingLinkView> Links);

/// <summary>A new key and the link it belongs to.</summary>
public sealed record BookingLinkKey(BookingLinkView Link, string Key);

/// <summary>A link's rules after validation.</summary>
public sealed record ValidatedRules(
    string Name,
    LocalDate? EarliestDate,
    int HorizonDays,
    int LeadMinutes,
    int AppointmentMinutes,
    bool ShowsQueue,
    bool Active);

/// <summary>
/// The host's booking links: one per audience, each with its own rules.
/// </summary>
public static class BookingLinkEndpoints
{
    public const int MaxHorizonDays = 365;
    public const int MaxLeadMinutes = 30 * 24 * 60;
    public const int MinAppointmentMinutes = 5;
    public const int MaxAppointmentMinutes = 240;

    private static readonly LocalDatePattern DatePattern = LocalDatePattern.Iso;

    /// <summary>The Data Protection purpose for kept keys. Changing it makes every kept key unreadable.</summary>
    internal const string ProtectionPurpose = "aberaTech.Scheduling.BookingLinkKey.v1";

    /// <summary>The name the default rules show under.</summary>
    public const string DefaultName = "Plain /schedule, no link";

    /// <summary>Checks a submitted set of rules, returning them parsed or the one thing wrong.</summary>
    internal static (ValidatedRules? Rules, string? Error) Validate(BookingLinkRules? request, bool isDefault)
    {
        if (request is null)
        {
            return (null, "Send the link's rules.");
        }

        var name = isDefault ? DefaultName : (request.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120)
        {
            return (null, "Give the link a name.");
        }

        LocalDate? earliest = null;
        if (!string.IsNullOrWhiteSpace(request.EarliestDate))
        {
            var parsed = DatePattern.Parse(request.EarliestDate.Trim());
            if (!parsed.Success)
            {
                return (null, "The earliest date must look like 2026-11-16.");
            }

            earliest = parsed.Value;
        }

        if (request.HorizonDays is < 1 or > MaxHorizonDays)
        {
            return (null, $"Days ahead must be from 1 to {MaxHorizonDays}.");
        }

        if (request.LeadMinutes is < 0 or > MaxLeadMinutes)
        {
            return (null, "Notice must be from 0 minutes to 30 days.");
        }

        if (request.AppointmentMinutes is < MinAppointmentMinutes or > MaxAppointmentMinutes)
        {
            return (null, $"A meeting must be from {MinAppointmentMinutes} to {MaxAppointmentMinutes} minutes.");
        }

        return (new ValidatedRules(
            name,
            earliest,
            request.HorizonDays,
            request.LeadMinutes,
            request.AppointmentMinutes,
            request.ShowsQueue,
            isDefault || request.Active), null);
    }

    public static IEndpointRouteBuilder MapBookingLinkEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/scheduling/admin/links")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Scheduling admin");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/default", SaveDefaultAsync);
        group.MapPut("/{linkId:guid}", UpdateAsync);
        group.MapPost("/{linkId:guid}/key", NewKeyAsync);
        group.MapPost("/{linkId:guid}/revoke", RevokeAsync);

        return routes;
    }

    private static async Task<IResult> ListAsync(
        SchedulingDbContext database,
        SchedulingOptions options,
        IDataProtectionProvider protection,
        CancellationToken cancellationToken)
    {
        var protector = protection.CreateProtector(ProtectionPurpose);

        var links = await database.BookingLinks
            .AsNoTracking()
            .OrderBy(link => link.CreatedAt)
            .ToListAsync(cancellationToken);

        var counts = await database.Appointments
            .AsNoTracking()
            .Where(appointment => !appointment.Cancelled)
            .GroupBy(appointment => appointment.BookingLinkId)
            .Select(group => new { LinkId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        int Count(Guid? id) => counts.FirstOrDefault(entry => entry.LinkId == id)?.Count ?? 0;

        var stored = links.FirstOrDefault(link => link.IsDefault);
        var fallback = stored is null
            ? FromPolicy(BookingLinkResolver.SettingsDefault(options), Count(null))
            : View(stored, Count(null));

        return Results.Ok(new BookingLinkList(
            fallback,
            [.. links.Where(link => !link.IsDefault).Select(link => View(link, Count(link.Id), Reveal(protector, link)))]));
    }

    private static async Task<IResult> CreateAsync(
        BookingLinkRules request,
        SchedulingDbContext database,
        IDataProtectionProvider protection,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (rules, error) = Validate(request, isDefault: false);
        if (rules is null)
        {
            return Results.BadRequest(new { error });
        }

        var now = clock.GetCurrentInstant();
        var link = new BookingLink { Id = Guid.NewGuid(), CreatedAt = now };
        Apply(link, rules);
        var key = Issue(link, protection, now);

        database.BookingLinks.Add(link);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new BookingLinkKey(View(link, 0, key), key));
    }

    private static async Task<IResult> UpdateAsync(
        Guid linkId,
        BookingLinkRules request,
        SchedulingDbContext database,
        CancellationToken cancellationToken)
    {
        var link = await database.BookingLinks
            .FirstOrDefaultAsync(candidate => candidate.Id == linkId && !candidate.IsDefault, cancellationToken);

        if (link is null)
        {
            return Results.NotFound();
        }

        var (rules, error) = Validate(request, isDefault: false);
        if (rules is null)
        {
            return Results.BadRequest(new { error });
        }

        if (link.RevokedAt is not null && rules.Active)
        {
            return Results.Conflict(new { error = "A revoked link has no key. Press New key to issue a new link." });
        }

        Apply(link, rules);
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Saves the rules for /schedule with no link.
    /// </summary>
    /// <remarks>
    /// The first save creates the row. Until then the deployment's settings
    /// are the default, which is how every deployment behaved before links.
    /// </remarks>
    private static async Task<IResult> SaveDefaultAsync(
        BookingLinkRules request,
        SchedulingDbContext database,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (rules, error) = Validate(request, isDefault: true);
        if (rules is null)
        {
            return Results.BadRequest(new { error });
        }

        var link = await database.BookingLinks.FirstOrDefaultAsync(candidate => candidate.IsDefault, cancellationToken);
        if (link is null)
        {
            link = new BookingLink
            {
                Id = Guid.NewGuid(),
                IsDefault = true,
                CreatedAt = clock.GetCurrentInstant()
            };
            database.BookingLinks.Add(link);
        }

        Apply(link, rules);
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Replaces a link's key. The old link stops working at once.
    /// </summary>
    /// <remarks>
    /// For a link that reached somebody it should not have. Bookings already
    /// made through it stay. A revoked link comes back on, under the new key.
    /// </remarks>
    private static async Task<IResult> NewKeyAsync(
        Guid linkId,
        SchedulingDbContext database,
        IDataProtectionProvider protection,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var link = await database.BookingLinks
            .FirstOrDefaultAsync(candidate => candidate.Id == linkId && !candidate.IsDefault, cancellationToken);

        if (link is null)
        {
            return Results.NotFound();
        }

        if (link.RevokedAt is not null)
        {
            link.RevokedAt = null;
            link.Active = true;
        }

        var key = Issue(link, protection, clock.GetCurrentInstant());
        await database.SaveChangesAsync(cancellationToken);

        var bookings = await database.Appointments
            .CountAsync(appointment => appointment.BookingLinkId == linkId && !appointment.Cancelled, cancellationToken);

        return Results.Ok(new BookingLinkKey(View(link, bookings, key), key));
    }

    /// <summary>
    /// Revokes a link: its key is destroyed and the link is off.
    /// </summary>
    /// <remarks>
    /// Off pauses a link and On brings the same address back. Revoke ends the
    /// address for good: nothing is left that matches it. The row stays, with
    /// its bookings, and New key issues a new address for the same rules.
    /// </remarks>
    private static async Task<IResult> RevokeAsync(
        Guid linkId,
        SchedulingDbContext database,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var link = await database.BookingLinks
            .FirstOrDefaultAsync(candidate => candidate.Id == linkId && !candidate.IsDefault, cancellationToken);

        if (link is null)
        {
            return Results.NotFound();
        }

        link.KeyHash = null;
        link.ProtectedKey = null;
        link.KeyIssuedAt = null;
        link.Active = false;
        link.RevokedAt ??= clock.GetCurrentInstant();
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>Gives a link a new key, kept as a hash for lookup and encrypted for the host's list.</summary>
    private static string Issue(BookingLink link, IDataProtectionProvider protection, Instant now)
    {
        var key = BookingLinkKeys.NewKey();
        link.KeyHash = BookingLinkKeys.Hash(key);
        link.ProtectedKey = protection.CreateProtector(ProtectionPurpose).Protect(key);
        link.KeyIssuedAt = now;
        return key;
    }

    /// <summary>
    /// The kept key, or null when there is none or it cannot be read.
    /// </summary>
    /// <remarks>
    /// A key ring that lost the key that encrypted it gives null rather than
    /// an error, so one unreadable row does not take the list down. The
    /// address still works. New key gives one the page can show.
    /// </remarks>
    private static string? Reveal(IDataProtector protector, BookingLink link)
    {
        if (link.ProtectedKey is null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(link.ProtectedKey);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private static void Apply(BookingLink link, ValidatedRules rules)
    {
        link.Name = rules.Name;
        link.EarliestDate = rules.EarliestDate;
        link.HorizonDays = rules.HorizonDays;
        link.LeadMinutes = rules.LeadMinutes;
        link.AppointmentMinutes = rules.AppointmentMinutes;
        link.ShowsQueue = rules.ShowsQueue;
        link.Active = rules.Active;
    }

    private static BookingLinkView View(BookingLink link, int bookings, string? key = null) => new(
        link.IsDefault ? null : link.Id,
        link.Name,
        link.IsDefault,
        link.EarliestDate is { } date ? DatePattern.Format(date) : null,
        link.HorizonDays,
        link.LeadMinutes,
        link.AppointmentMinutes,
        link.ShowsQueue,
        link.Active,
        bookings,
        key,
        Format(link.RevokedAt is null ? link.KeyIssuedAt ?? link.CreatedAt : null),
        Format(link.RevokedAt));

    private static string? Format(Instant? instant) =>
        instant?.ToString("uuuu-MM-ddTHH:mm:ss'Z'", null);

    private static BookingLinkView FromPolicy(BookingPolicy policy, int bookings) => new(
        null,
        DefaultName,
        true,
        policy.EarliestDate is { } date ? DatePattern.Format(date) : null,
        policy.HorizonDays,
        policy.LeadMinutes,
        policy.AppointmentMinutes,
        policy.ShowsQueue,
        true,
        bookings);
}
