using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// How the CSP learns to allow the one inline script the prerendered pages
/// carry.
/// </summary>
/// <remarks>
/// The pages are prerendered with MUI's color-scheme bootstrap inline — it
/// must run before first paint, which is the one thing an external script
/// cannot promise. A hardcoded hash in the CSP would break silently whenever
/// an MUI upgrade changes a byte of that script, so the hashes are computed
/// from the shipped HTML at startup instead: whatever was baked is what is
/// allowed, and nothing else.
/// </remarks>
public class CspInlineScriptsTests
{
    private static string Sha256Of(string content) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    [Fact]
    public void An_inline_script_yields_its_csp_hash()
    {
        var hashes = CspInlineScripts.HashesIn("<html><script>doThing()</script></html>");

        Assert.Equal([$"'sha256-{Sha256Of("doThing()")}'"], hashes);
    }

    [Fact]
    public void A_script_loaded_by_src_needs_no_hash()
    {
        var hashes = CspInlineScripts.HashesIn(
            "<script type=\"module\" crossorigin src=\"/assets/index-abc.js\"></script>");

        Assert.Empty(hashes);
    }

    [Fact]
    public void Duplicate_scripts_yield_one_hash()
    {
        // Every prerendered page carries the same bootstrap; the CSP needs it
        // once.
        var hashes = CspInlineScripts.HashesIn(
            "<script>same()</script><div></div><script>same()</script>");

        Assert.Single(hashes);
    }

    [Fact]
    public void Html_without_inline_scripts_yields_nothing()
    {
        Assert.Empty(CspInlineScripts.HashesIn("<html><body>plain</body></html>"));
    }

    [Fact]
    public void The_hash_covers_the_exact_bytes_between_the_tags()
    {
        // CSP hashes the script's text verbatim — whitespace included. A
        // trimmed or normalised hash would not match the browser's.
        var content = "\n  let x = 1;\n";
        var hashes = CspInlineScripts.HashesIn($"<script>{content}</script>");

        Assert.Equal([$"'sha256-{Sha256Of(content)}'"], hashes);
    }
}
