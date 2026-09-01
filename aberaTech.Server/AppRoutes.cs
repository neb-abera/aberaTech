using System.Text.Json;

namespace aberaTech.Server;

/// <summary>
/// The paths the single-page app can actually render.
///
/// Written to <c>app-routes.json</c> by the client build, from the same
/// <c>site/routes.ts</c> the router reads, so the two cannot disagree about
/// what the site contains. Without it every path answered 200 with an empty
/// shell: a typo, a dead link and a deleted page were all indistinguishable
/// from a real one, to a reader and to a crawler alike.
/// </summary>
public static class AppRoutes
{
    /// <summary>
    /// The manifest, or null when there is none — an older build, or a test
    /// host with a bare webroot. Null means "answer as before", never
    /// "nothing is a real page".
    /// </summary>
    public static HashSet<string>? Load(string? webRootPath)
    {
        if (string.IsNullOrEmpty(webRootPath)) return null;

        var file = Path.Combine(webRootPath, "app-routes.json");
        if (!File.Exists(file)) return null;

        try
        {
            var paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(file));
            if (paths is null or { Length: 0 }) return null;

            // Trailing slashes are the same page; "/" is both.
            var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                routes.Add(path);
                routes.Add(path == "/" ? "" : path + "/");
            }

            return routes;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            // A manifest that cannot be read is the same as not having one.
            return null;
        }
    }
}
