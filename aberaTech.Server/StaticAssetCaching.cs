namespace aberaTech.Server;

/// <summary>
/// Decides the Cache-Control header for a static file, from the request path
/// and the name of the file actually being served.
/// </summary>
/// <remarks>
/// Both inputs matter: /assets/ is recognised by the request path, but the SPA
/// fallback serves index.html under arbitrary route paths, so the shell is
/// recognised by file name. Without an explicit header the CDN invents its own
/// policy (four hours for everything), which is both too short for hashed
/// assets and too long to trust for the shell.
/// </remarks>
public static class StaticAssetCaching
{
    // Hashed filenames can never change behind their URL; cache until doomsday.
    private const string Immutable = "public, max-age=31536000, immutable";

    // The shell changes on every deploy under a stable URL; always revalidate.
    // Revalidation is an ETag round trip, so an unchanged shell costs a 304.
    private const string Revalidate = "no-cache";

    // Unhashed extras (favicon and friends): long enough to be cheap, short
    // enough that replacing one is visible the next day without a rename.
    private const string Daily = "public, max-age=86400";

    public static string For(string requestPath, string fileName)
    {
        if (fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            // The shell, the empty spa.html fallback, and every prerendered
            // page: HTML whose bytes change under a stable URL on each deploy.
            return Revalidate;
        }

        if (requestPath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            return Immutable;
        }

        return Daily;
    }
}
