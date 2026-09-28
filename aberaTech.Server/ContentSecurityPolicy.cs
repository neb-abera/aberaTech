namespace aberaTech.Server;

/// <summary>
/// The Content-Security-Policy header, with each page's own inline hashes.
/// </summary>
/// <remarks>
/// The policy names exactly what the client loads. MUI's styles go in
/// <c>&lt;style&gt;</c> elements allowed by hash (CspInlineStyles.cs). The
/// prerendered pages carry MUI's color-scheme bootstrap as an inline script,
/// because it must run before first paint, and /links carries one that starts
/// its API request from the head (CspInlineScripts.cs). The guides embed
/// Google Docs and YouTube players in iframes, and a handful of partner logos
/// load from their own hosts. Everything else is same-origin only.
///
/// Every HTML file under the web root is read once at startup, and every page
/// the server renders is hashed when this is built. A page is sent the hashes
/// of its own blocks and no others. On 2026-09-28 the union of all 26 was
/// sent with every response: 1,999 bytes of header, which pushed the home
/// page's first response past the 14.6 KB a new connection sends in its
/// first round trip. A page nobody hashed gets the base policy, which allows
/// no inline block at all.
/// </remarks>
public sealed class ContentSecurityPolicy
{
    private readonly Dictionary<string, string> _pages;

    /// <summary>The policy for a response that is no known page.</summary>
    public string Base { get; }

    private ContentSecurityPolicy(Dictionary<string, string> pages)
    {
        _pages = pages;
        Base = For("");
    }

    /// <summary>
    /// Hashes every HTML file under <paramref name="webRoot"/>, keyed by the
    /// request path that serves it, and every rendered page by its route.
    /// </summary>
    public static ContentSecurityPolicy Load(string? webRoot, IReadOnlyDictionary<string, string> renderedPages)
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        if (webRoot is not null && Directory.Exists(webRoot))
        {
            foreach (var file in Directory.EnumerateFiles(webRoot, "*.html", SearchOption.AllDirectories))
            {
                var path = "/" + Path.GetRelativePath(webRoot, file).Replace(Path.DirectorySeparatorChar, '/');
                pages[path] = For(File.ReadAllText(file));
            }
        }

        foreach (var (path, html) in renderedPages)
        {
            pages[path] = For(html);
        }

        return new ContentSecurityPolicy(pages);
    }

    /// <summary>The policy for the file or route at <paramref name="path"/>, over the request's scheme.</summary>
    public string ForPath(string? path, bool https)
    {
        var policy = path is not null && _pages.TryGetValue(path, out var page) ? page : Base;

        // Only over HTTPS, like HSTS and for the same reason. Production
        // always is (Cloudflare, then the ingress; ClientAddress reads
        // X-Forwarded-Proto), so every visitor gets it. Over plain HTTP (the
        // production image on the compose network, where `make e2e` drives a
        // real browser) the same directive makes Chromium fetch the bundle
        // from https://app-under-test:8080, which nothing answers, and the
        // page never boots. StaticPipelineTests pins both halves.
        return https ? policy + "; upgrade-insecure-requests" : policy;
    }

    private static string For(string html)
    {
        var scripts = string.Concat(CspInlineScripts.HashesIn(html).Select(hash => " " + hash));

        // The empty element emotion fills at run time is on every page.
        // CspInlineStyles.cs says why that hash is enough for it.
        var styles = string.Concat(
            CspInlineStyles.HashesIn(html)
                .Prepend(CspInlineStyles.EmptyElement)
                .Distinct()
                .Select(hash => " " + hash));

        return "default-src 'self'; "
            // The one third-party script: Cloudflare's RUM beacon, injected by
            // the CDN into every HTML response and opted into deliberately for
            // real-user Core Web Vitals. It loads from static.cloudflareinsights
            // and reports to cloudflareinsights (connect-src below).
            + $"script-src 'self' https://static.cloudflareinsights.com{scripts}; "
            + $"style-src 'self'{styles}; "
            // Style attributes: React's style prop in the prerendered markup.
            // The standard's one allowance, for attributes alone.
            + "style-src-attr 'unsafe-inline'; "
            // The transition guide's partner images. yceml.net serves the DITY
            // calculator banner, which used to load through a CJ Affiliate
            // redirect on lduhtrp.net. The page links the image directly now.
            + "img-src 'self' data: https://www.va.gov https://www.yceml.net "
            + "https://www.hiringourheroes.org https://nvf.org https://assets.recruitmilitary.com; "
            + "font-src 'self' data:; "
            + "connect-src 'self' https://cloudflareinsights.com; "
            + "frame-src https://docs.google.com https://drive.google.com "
            + "https://www.youtube.com https://www.youtube-nocookie.com; "
            + "object-src 'none'; base-uri 'self'; form-action 'self'; "
            + "frame-ancestors 'self'";
    }
}
