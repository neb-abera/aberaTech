using System.Globalization;
using NodaTime;

namespace aberaTech.Server;

/// <summary>
/// /.well-known/security.txt (RFC 9116): where to report a vulnerability,
/// the same two channels SECURITY.md names.
/// </summary>
/// <remarks>
/// Served by the app rather than shipped as a file because the RFC requires
/// an Expires line no more than a year out, and a file's date is whatever it
/// was on the day of the build. Expires is a year from the request, to the
/// day, so the text changes once a day and caches for a day.
/// </remarks>
public static class SecurityTxt
{
    public const string Path = "/.well-known/security.txt";

    public const string Contact = "mailto:support@alias.abera.tech";
    public const string Advisories = "https://github.com/neb-abera/aberaTech/security/advisories/new";
    public const string Policy = "https://github.com/neb-abera/aberaTech/blob/master/SECURITY.md";
    public const string Canonical = "https://abera.tech" + Path;

    public static string Render(DateTimeOffset now)
    {
        var expires = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddYears(1);
        return $"""
            Contact: {Advisories}
            Contact: {Contact}
            Expires: {expires.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}
            Preferred-Languages: en
            Canonical: {Canonical}
            Policy: {Policy}

            """;
    }

    public static IEndpointRouteBuilder MapSecurityTxt(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(Path, (IClock clock, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = StaticAssetCaching.For(Path, "security.txt");
            return Results.Text(Render(clock.GetCurrentInstant().ToDateTimeOffset()), "text/plain; charset=utf-8");
        }).AllowAnonymous();

        return routes;
    }
}
