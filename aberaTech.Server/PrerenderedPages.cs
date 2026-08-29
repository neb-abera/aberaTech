namespace aberaTech.Server;

/// <summary>
/// Maps a route URL to its prerendered HTML file, when one exists.
/// </summary>
/// <remarks>
/// The client build writes the static public pages to wwwroot as
/// &lt;route&gt;/index.html, but a visitor asks for the route. This is the
/// mapping between the two: an extensionless GET path whose prerendered file
/// exists is rewritten to that file; everything else — assets, API routes,
/// pages that are not prerendered — is left for the middleware behind it.
/// The existence check is a parameter so the decision is testable without a
/// filesystem.
/// </remarks>
public static class PrerenderedPages
{
    public static string? RewriteFor(string requestPath, Func<string, bool> fileExists)
    {
        if (requestPath is "/" or "")
        {
            // The default-files middleware already serves index.html here.
            return null;
        }

        if (requestPath.Contains('.'))
        {
            // A file request. Prerendered pages live at extensionless routes.
            return null;
        }

        var candidate = requestPath.TrimEnd('/') + "/index.html";
        return fileExists(candidate) ? candidate : null;
    }
}
