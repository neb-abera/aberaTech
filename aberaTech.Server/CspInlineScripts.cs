using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace aberaTech.Server;

/// <summary>
/// The CSP hashes for the inline scripts a prerendered page carries.
/// </summary>
/// <remarks>
/// Prerendering bakes MUI's color-scheme bootstrap into the HTML as an inline
/// script — it must run before first paint, which an external script cannot
/// promise. script-src 'self' blocks it, and a hash hardcoded next to that
/// directive would break silently whenever an MUI upgrade changes a byte of
/// the script. So the hashes come from the shipped HTML itself, read once at
/// startup: whatever was baked is what is allowed, and nothing else is.
/// </remarks>
public static partial class CspInlineScripts
{
    [GeneratedRegex("""<script(?<attrs>[^>]*)>(?<content>.*?)</script>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptElement();

    public static IReadOnlyList<string> HashesIn(string html) =>
        ScriptElement()
            .Matches(html)
            .Where(match => !match.Groups["attrs"].Value.Contains("src", StringComparison.OrdinalIgnoreCase))
            .Select(match => match.Groups["content"].Value)
            .Distinct()
            .Select(content =>
                $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'")
            .ToList();
}
