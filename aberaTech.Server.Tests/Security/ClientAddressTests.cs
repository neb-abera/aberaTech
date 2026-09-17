using System.Net;
using System.Net.Http.Json;
using aberaTech.Server.Tests.Support;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// Who a request is from, as the rate limiter and the security log see it.
/// </summary>
/// <remarks>
/// Production is client → Cloudflare → Container Apps ingress → app. Each
/// proxy appends the address it saw to X-Forwarded-For, so the header arrives
/// as "[anything the client sent], client, cloudflare-edge" from a socket
/// that belongs to the ingress. The client is two hops from the right; a
/// caller controls only what is to the left of that.
/// </remarks>
public sealed class ClientAddressTests
{
    private const string Ingress = "100.100.0.5";
    private const string EdgeA = "172.70.1.1";
    private const string EdgeB = "162.158.9.9";

    /// <summary>The public-write budget: five a minute.</summary>
    private const int Budget = 5;

    private static TestApp App(int hops = 2) => new(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Scheduling"] = DatabaseMigrationsTests.Unreachable,
        ["Database:MigrateOnStart"] = "false",
        ["ClientAddress:ForwardedHops"] = hops.ToString(System.Globalization.CultureInfo.InvariantCulture)
    });

    [Fact]
    public async Task Two_visitors_behind_the_same_cloudflare_edge_get_a_budget_each()
    {
        // The defect: with one hop trusted, the "client" was the Cloudflare
        // edge, and everybody behind a point of presence shared five a minute.
        using var app = App();
        using var client = app.CreateClient();

        await SpendAsync(client, $"203.0.113.7, {EdgeA}");

        Assert.Equal(HttpStatusCode.TooManyRequests, await JoinAsync(client, $"203.0.113.7, {EdgeA}"));
        Assert.Equal(HttpStatusCode.BadRequest, await JoinAsync(client, $"203.0.113.8, {EdgeA}"));
    }

    [Fact]
    public async Task One_visitor_has_one_budget_whichever_edge_carries_them()
    {
        using var app = App();
        using var client = app.CreateClient();

        await SpendAsync(client, $"203.0.113.7, {EdgeA}");

        Assert.Equal(HttpStatusCode.TooManyRequests, await JoinAsync(client, $"203.0.113.7, {EdgeB}"));
    }

    [Fact]
    public async Task A_forwarded_for_the_caller_made_up_buys_no_new_budget()
    {
        // Whatever the caller sends ends up to the left of the entry
        // Cloudflare appends, which is further than the app ever reads.
        using var app = App();
        using var client = app.CreateClient();

        await SpendAsync(client, $"198.51.100.1, 203.0.113.7, {EdgeA}");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await JoinAsync(client, $"198.51.100.2, 203.0.113.7, {EdgeA}"));
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await JoinAsync(client, $"10.0.0.1, 198.51.100.3, 203.0.113.7, {EdgeA}"));
    }

    [Fact]
    public async Task A_made_up_cloudflare_header_is_not_read_at_all()
    {
        using var app = App();
        using var client = app.CreateClient();

        await SpendAsync(client, $"203.0.113.7, {EdgeA}");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await JoinAsync(client, $"203.0.113.7, {EdgeA}", ("CF-Connecting-IP", "198.51.100.77")));
    }

    [Fact]
    public async Task An_ipv6_visitor_is_their_whole_slash_64()
    {
        // A residential v6 customer is handed at least a /64 and can source
        // from any of its 2^64 addresses; keyed per address, the limit would
        // be no limit. The next /64 along is somebody else.
        using var app = App();
        using var client = app.CreateClient();

        await SpendAsync(client, $"2001:db8:1:2::1, {EdgeA}");

        Assert.Equal(HttpStatusCode.TooManyRequests, await JoinAsync(client, $"2001:db8:1:2:ffff:abcd:0:9, {EdgeA}"));
        Assert.Equal(HttpStatusCode.BadRequest, await JoinAsync(client, $"2001:db8:1:3::1, {EdgeA}"));
    }

    [Fact]
    public async Task With_no_proxy_configured_forwarded_headers_are_ignored()
    {
        // Zero hops is the app on a socket of its own. Nothing in front of it
        // vouches for the header, so the peer is the client.
        using var app = App(hops: 0);
        using var client = app.CreateClient();

        await SpendAsync(client, "203.0.113.7");

        Assert.Equal(HttpStatusCode.TooManyRequests, await JoinAsync(client, "203.0.113.200"));
    }

    [Fact]
    public async Task The_scheme_the_ingress_reports_still_arrives()
    {
        // The ingress overwrites X-Forwarded-Proto with one value while
        // X-Forwarded-For carries two or more. Reading two hops of one must
        // not lose the other: HSTS, the Secure cookie and the HTTPS redirect
        // all hang off it.
        using var app = App();
        using var client = app.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add(RemoteAddressStartupFilter.Header, Ingress);
        request.Headers.Add("X-Forwarded-For", $"203.0.113.7, {EdgeA}");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void A_hop_count_that_makes_no_sense_stops_the_boot(int hops)
    {
        using var app = App(hops);

        Assert.ThrowsAny<Exception>(() => app.CreateClient());
    }

    private static async Task SpendAsync(HttpClient client, string forwardedFor)
    {
        for (var attempt = 0; attempt < Budget; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, await JoinAsync(client, forwardedFor));
        }
    }

    /// <summary>A join with no name: counted by the limiter, refused before any database work.</summary>
    private static async Task<HttpStatusCode> JoinAsync(
        HttpClient client,
        string forwardedFor,
        params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scheduling/queue")
        {
            Content = JsonContent.Create(new { name = "" })
        };
        request.Headers.Add(RemoteAddressStartupFilter.Header, Ingress);
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return (await client.SendAsync(request)).StatusCode;
    }
}

public sealed class ClientAddressPartitionKeyTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::", "2001:db8:1:2::/64")]
    public void The_key_is_the_address_or_its_allocation(string address, string key)
    {
        Assert.Equal(key, ClientAddress.PartitionKey(IPAddress.Parse(address)));
    }

    [Fact]
    public void A_wider_prefix_is_honoured_bit_for_bit()
    {
        Assert.Equal("2001:db8:1:200::/56", ClientAddress.PartitionKey(IPAddress.Parse("2001:db8:1:2ff::1"), 56));
        Assert.Equal("2001:db8:1:2c0::/58", ClientAddress.PartitionKey(IPAddress.Parse("2001:db8:1:2ff::1"), 58));
    }

    [Fact]
    public void No_address_at_all_is_one_shared_bucket_rather_than_no_limit()
    {
        Assert.Equal("unknown", ClientAddress.PartitionKey((IPAddress?)null));
    }
}
