namespace aberaTech.Server.App;

/// <summary>
/// The aberaTech iPhone app's people and data. The "App" section.
/// </summary>
/// <remarks>
/// The owner is whoever the admin allowlist names (Admin:AllowedEmails),
/// known to the app as <see cref="OwnerName"/>. A household member signs in
/// with any of their own Google addresses and gets one view whichever they
/// use (Neb, 2026-11-21). The Notion ids are the
/// Tasks and Wish list items data sources, which the server's Notion
/// integration has to be connected to.
/// </remarks>
public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>The keyed NotionClient that holds <see cref="NotionToken"/>.</summary>
    public const string NotionKey = "app";

    /// <summary>
    /// The "abera.tech app" Notion integration's secret: read, insert, and
    /// update, connected to Tasks and Wish list items alone. The playbook's
    /// integration only reads, so the app writes with its own.
    /// </summary>
    public string? NotionToken { get; init; }

    public string OwnerName { get; init; } = "Neb";

    public AppMember[] Household { get; init; } = [];

    /// <summary>The scheme the app registers, where a finished sign-in is sent.</summary>
    public string Scheme { get; init; } = "aberatech";

    /// <summary>Notion data source of Tasks.</summary>
    public string? TasksDataSourceId { get; init; }

    /// <summary>Notion data source of Wish list items.</summary>
    public string? WishListDataSourceId { get; init; }

    /// <summary>
    /// The Tasks select that records the last status email the requester
    /// got from the email routine. A request added in the app sets it to
    /// "Got it", so the routine sends no receipt for it. Unset, it is left alone.
    /// </summary>
    public string? ToldProperty { get; init; }
}

/// <summary>A household member: the name Tasks records them by, and every Google address they sign in with.</summary>
public sealed class AppMember
{
    public string Name { get; init; } = string.Empty;

    public string[] Emails { get; init; } = [];
}

public enum AppRole
{
    Owner,
    Household
}

/// <summary>Who is signed in to the app.</summary>
public sealed record AppPerson(string Name, AppRole Role);

public static class AppPeople
{
    /// <summary>The person a Google address belongs to, or null for anyone else.</summary>
    public static AppPerson? Resolve(string? email, string[] ownerEmails, AppOptions options)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var address = email.Trim();
        if (ownerEmails.Any(owner => string.Equals(owner, address, StringComparison.OrdinalIgnoreCase)))
        {
            return new AppPerson(options.OwnerName, AppRole.Owner);
        }

        var member = options.Household.FirstOrDefault(m =>
            m.Emails.Any(e => string.Equals(e, address, StringComparison.OrdinalIgnoreCase)));
        return member is null || string.IsNullOrWhiteSpace(member.Name) ? null : new AppPerson(member.Name, AppRole.Household);
    }

    /// <summary>The person a stored session names, if they are still allowed in.</summary>
    public static AppPerson? ByName(string name, AppOptions options)
    {
        if (string.Equals(name, options.OwnerName, StringComparison.Ordinal)) return new AppPerson(name, AppRole.Owner);
        return options.Household.Any(m => string.Equals(m.Name, name, StringComparison.Ordinal))
            ? new AppPerson(name, AppRole.Household)
            : null;
    }

    /// <summary>
    /// Whose requests a person sees: their own, or for the owner the first
    /// household member's, so he sees what she sees.
    /// </summary>
    public static string? Requester(AppPerson person, AppOptions options) =>
        person.Role == AppRole.Household ? person.Name : options.Household.FirstOrDefault()?.Name;
}
