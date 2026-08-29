using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// The browser-cache contract for everything the SPA build serves.
/// </summary>
/// <remarks>
/// Vite writes a content hash into every filename under /assets/, so those
/// files can never change behind their URL and are safe to cache for a year.
/// index.html is the one file whose bytes change under a stable URL on every
/// deploy — it is the thing that names the current hashes — so it must always
/// revalidate. Get this backwards and a visitor is pinned to a stale shell
/// requesting assets that no longer exist.
/// </remarks>
public class StaticAssetCachingTests
{
    [Theory]
    [InlineData("/assets/index-Dg-NvJFP.js", "index-Dg-NvJFP.js")]
    [InlineData("/assets/index-x1XGuNl0.css", "index-x1XGuNl0.css")]
    [InlineData("/Assets/Chunk-ABC123.js", "Chunk-ABC123.js")]
    public void Hashed_assets_are_immutable_for_a_year(string path, string file)
    {
        Assert.Equal("public, max-age=31536000, immutable", StaticAssetCaching.For(path, file));
    }

    [Fact]
    public void The_shell_served_at_the_root_always_revalidates()
    {
        Assert.Equal("no-cache", StaticAssetCaching.For("/", "index.html"));
    }

    [Fact]
    public void The_shell_served_by_the_spa_fallback_always_revalidates()
    {
        // Client-side routes fall back to index.html under their own path, so
        // the rule has to key on the file being served, not the URL asked for.
        Assert.Equal(
            "no-cache",
            StaticAssetCaching.For("/guides/military-transition", "index.html"));
    }

    [Fact]
    public void Unhashed_files_get_a_day()
    {
        Assert.Equal(
            "public, max-age=86400",
            StaticAssetCaching.For("/fingerprint_24dp_E8EAED.png", "fingerprint_24dp_E8EAED.png"));
    }
}
