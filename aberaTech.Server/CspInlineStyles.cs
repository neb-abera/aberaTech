using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace aberaTech.Server;

/// <summary>
/// The CSP hashes for the inline style elements the site uses, so
/// <c>style-src</c> needs no <c>'unsafe-inline'</c>.
/// </summary>
/// <remarks>
/// Two kinds exist. The prerendered pages carry MUI's styles in a few
/// <c>&lt;style data-emotion&gt;</c> elements in the head
/// (tools/prerender.mjs gathers them there), each allowed by its hash, read
/// from every shipped page at startup the way CspInlineScripts reads the
/// scripts. At run time emotion's production sheet creates empty style
/// elements and adds rules through <c>insertRule</c>. CSP checks an
/// element's text and not the CSSOM, so the empty string's hash admits
/// those and nothing with content.
/// </remarks>
public static partial class CspInlineStyles
{
    /// <summary>sha256 of the empty string: the element emotion creates before it inserts rules.</summary>
    public const string EmptyElement = "'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='";

    [GeneratedRegex("""<style(?:\s[^>]*)?>(?<content>.*?)</style>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StyleElement();

    public static IReadOnlyList<string> HashesIn(string html) =>
        StyleElement()
            .Matches(html)
            .Select(match => match.Groups["content"].Value)
            .Distinct()
            .Select(Hash)
            .ToList();

    /// <summary>Every style element on every page under the web root, once each.</summary>
    public static IReadOnlyList<string> HashesUnder(string webRoot) =>
        Directory.EnumerateFiles(webRoot, "*.html", SearchOption.AllDirectories)
            .SelectMany(file => HashesIn(File.ReadAllText(file)))
            .Distinct()
            .ToList();

    private static string Hash(string content) =>
        $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'";
}
