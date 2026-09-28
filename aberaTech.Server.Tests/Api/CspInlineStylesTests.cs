using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// How the CSP learns which inline style elements to allow: the ones the
/// prerendered pages were baked with, and the empty one emotion creates at
/// run time.
/// </summary>
public class CspInlineStylesTests
{
    private static string Hash(string content) =>
        $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'";

    [Fact]
    public void A_style_element_yields_the_hash_of_its_exact_text()
    {
        var css = "\n.css-abc{color:red}\n";

        Assert.Equal([Hash(css)], CspInlineStyles.HashesIn($"<style data-emotion=\"css abc\">{css}</style>"));
    }

    [Fact]
    public void The_same_element_on_two_pages_yields_one_hash()
    {
        var global = "<style data-emotion=\"css-global x\">html{margin:0}</style>";

        Assert.Single(CspInlineStyles.HashesIn(global + "<p></p>" + global));
    }

    [Fact]
    public void A_page_with_no_style_element_yields_nothing()
    {
        Assert.Empty(CspInlineStyles.HashesIn("<html><body style=\"margin:0\">plain</body></html>"));
    }

    [Fact]
    public void The_runtime_element_is_the_empty_string()
    {
        // emotion's production sheet creates <style> with no text and adds
        // rules through insertRule, which CSP does not govern. Its hash is
        // the empty string's, a constant.
        Assert.Equal(CspInlineStyles.EmptyElement, Hash(""));
        Assert.Equal("'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='", CspInlineStyles.EmptyElement);
    }
}
