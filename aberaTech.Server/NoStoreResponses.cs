namespace aberaTech.Server;

/// <summary>
/// Cache-Control: no-store on every response that is an answer rather than a
/// file: the probes and the API.
/// </summary>
/// <remarks>
/// Found on 2026-09-21: /healthz was served from the CDN edge with an age of
/// 83,725 seconds. Nothing on the origin wrote a Cache-Control header for it,
/// the edge's cache rule filled the gap, and for the length of that TTL an
/// outage would have answered 200 to every probe. StaticAssetCaching decides
/// the header for files; this is the same decision for endpoints, made once
/// so a new route cannot forget it.
///
/// The edge honours this only where its own rule does not override the
/// origin, so the cache rule excludes these paths as well. The deploy smoke
/// test fetches /healthz twice and fails on a HIT, which is how a rule that
/// drifts back is noticed.
/// </remarks>
public static class NoStoreResponses
{
    public const string Header = "no-store";

    private static readonly PathString[] Paths = ["/healthz", "/readyz", "/api"];

    public static bool Applies(PathString path) =>
        Paths.Any(prefix => path.StartsWithSegments(prefix));

    public static IApplicationBuilder UseNoStoreResponses(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (Applies(context.Request.Path))
            {
                context.Response.Headers.CacheControl = Header;
            }

            return next();
        });
}
