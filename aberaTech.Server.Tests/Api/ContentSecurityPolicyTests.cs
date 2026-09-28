using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// Which hashes each page's policy carries: its own, read at startup.
/// </summary>
public sealed class ContentSecurityPolicyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csp-pages").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Hash(string content) =>
        $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'";

    private ContentSecurityPolicy Load(IReadOnlyDictionary<string, string>? rendered = null)
    {
        Directory.CreateDirectory(Path.Combine(_root, "links"));
        File.WriteAllText(Path.Combine(_root, "index.html"), "<script>a()</script><style>x{}</style>");
        File.WriteAllText(Path.Combine(_root, "links", "index.html"), "<script>a()</script><script>b()</script>");
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "<script>c()</script>");
        return ContentSecurityPolicy.Load(_root, rendered ?? new Dictionary<string, string>());
    }

    [Fact]
    public void Every_shipped_page_is_read_and_keeps_its_own_hashes()
    {
        var policy = Load();

        var home = policy.ForPath("/index.html", https: false);
        var links = policy.ForPath("/links/index.html", https: false);

        Assert.Contains(Hash("a()"), home);
        Assert.Contains(Hash("x{}"), home);
        Assert.DoesNotContain(Hash("b()"), home);
        Assert.Contains(Hash("b()"), links);
        Assert.DoesNotContain(Hash("x{}"), links);
        Assert.DoesNotContain(Hash("c()"), policy.ForPath("/notes.txt", https: false));
    }

    [Fact]
    public void A_rendered_page_is_hashed_by_its_route()
    {
        var policy = Load(new Dictionary<string, string> { ["/sms-terms"] = "<style>t{}</style>" });

        Assert.Contains(Hash("t{}"), policy.ForPath("/sms-terms", https: false));
        Assert.DoesNotContain(Hash("t{}"), policy.ForPath("/index.html", https: false));
    }

    [Fact]
    public void A_path_nobody_hashed_gets_the_base_policy_with_no_inline_block()
    {
        var policy = Load();

        var unknown = policy.ForPath("/elsewhere.html", https: false);

        Assert.Equal(policy.Base, unknown);
        Assert.Equal(1, unknown.Split("'sha256-").Length - 1);
        Assert.Contains(CspInlineStyles.EmptyElement, unknown);
    }

    [Fact]
    public void A_missing_web_root_still_yields_a_policy()
    {
        var policy = ContentSecurityPolicy.Load(Path.Combine(_root, "absent"), new Dictionary<string, string>());

        Assert.Equal(policy.Base, policy.ForPath("/index.html", https: false));
    }
}
