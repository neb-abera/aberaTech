using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;

namespace aberaTech.Server;

/// <summary>How many proxies stand between a visitor and this process.</summary>
public sealed class ClientAddressOptions
{
    public const string Section = "ClientAddress";

    /// <summary>
    /// The number of trusted proxies in front of the app, each of which
    /// appends the address it saw to X-Forwarded-For.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>0 — the app is on a socket of its own; forwarded headers are ignored.</item>
    /// <item>1 — one proxy (a bare Container Apps ingress, or compose behind a local proxy). The default.</item>
    /// <item>2 — production: Cloudflare, then the Container Apps ingress.</item>
    /// </list>
    /// Too low and everybody behind the nearest proxy is one client. Too high
    /// and the app reads an entry the caller wrote. It must equal the real
    /// chain, which is why it is configuration and not a guess.
    /// </remarks>
    public int ForwardedHops { get; set; } = 1;

    /// <summary>
    /// How much of an IPv6 address identifies a visitor. A /64 is the smallest
    /// allocation an end site is given, and every address inside it is the
    /// same subscriber's to use.
    /// </summary>
    public int Ipv6PrefixLength { get; set; } = 64;
}

/// <summary>
/// The one place that decides who a request is from. The rate limiter's
/// partition key and the security log both read it from here.
/// </summary>
/// <remarks>
/// In production the chain is client → Cloudflare → Container Apps ingress →
/// app. Cloudflare appends the address that connected to it, then the ingress
/// appends the Cloudflare edge it saw ("Only the rightmost IP is provided by
/// Azure Container Apps", in Microsoft's words), so X-Forwarded-For arrives as
///
///     [whatever the caller sent], client, cloudflare-edge
///
/// and the client is the second entry from the right. Reading exactly
/// <see cref="ClientAddressOptions.ForwardedHops"/> entries from the right is
/// what makes it unspoofable: a caller can only ever add to the left, beyond
/// where the walk stops. That holds because the origin accepts connections
/// from Cloudflare's ranges alone — enforced on the container app's ingress,
/// not here — so nothing reaches the ingress without Cloudflare's entry on it.
///
/// CF-Connecting-IP was the alternative and is deliberately not read. It is
/// one header rather than a position, which is simpler, but trusting it safely
/// means proving the request came through Cloudflare, and the only in-process
/// proof is checking the rightmost forwarded entry against a list of
/// Cloudflare's ranges compiled into the app — a list that goes stale
/// silently. The hop count goes wrong loudly (everybody shares a bucket, which
/// the tests pin) and needs no list.
///
/// The known-proxy allowlists are cleared because neither proxy has a fixed
/// address: the ingress is an internal address that changes with the
/// environment, and Cloudflare's edges are thousands. The hop count is the
/// trust boundary instead.
/// </remarks>
public static class ClientAddress
{
    public static ClientAddressOptions Bind(IConfiguration configuration)
    {
        var options = configuration.GetSection(ClientAddressOptions.Section).Get<ClientAddressOptions>()
                      ?? new ClientAddressOptions();

        if (options.ForwardedHops is < 0 or > 10)
        {
            throw new InvalidOperationException(
                $"{ClientAddressOptions.Section}:ForwardedHops must be between 0 and 10; it is the number of proxies in front of the app.");
        }

        if (options.Ipv6PrefixLength is < 16 or > 128)
        {
            throw new InvalidOperationException(
                $"{ClientAddressOptions.Section}:Ipv6PrefixLength must be between 16 and 128.");
        }

        return options;
    }

    /// <summary>
    /// Rewrites the connection's remote address and scheme from the forwarded
    /// headers. Everything after this in the pipeline sees the visitor.
    /// </summary>
    public static IApplicationBuilder UseClientAddress(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<ClientAddressOptions>();

        if (options.ForwardedHops == 0)
        {
            return app;
        }

        var forwarded = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = options.ForwardedHops
        };

        // Clear(), not an empty initializer: the defaults trust only loopback,
        // an empty collection initializer leaves those defaults in place, and
        // a list with entries in it means "trust only these".
        forwarded.KnownIPNetworks.Clear();
        forwarded.KnownProxies.Clear();

        return app.UseForwardedHeaders(forwarded);
    }

    /// <summary>The visitor's address as resolved, for a log line.</summary>
    public static string For(HttpContext context) =>
        Normalize(context.Connection.RemoteIpAddress)?.ToString() ?? "unknown";

    /// <summary>
    /// What a rate limit is counted against: the IPv4 address, or the IPv6
    /// network the visitor was allocated.
    /// </summary>
    public static string PartitionKey(HttpContext context) =>
        PartitionKey(
            context.Connection.RemoteIpAddress,
            context.RequestServices.GetService<ClientAddressOptions>()?.Ipv6PrefixLength ?? 64);

    public static string PartitionKey(IPAddress? address, int ipv6PrefixLength = 64)
    {
        var normal = Normalize(address);

        if (normal is null)
        {
            return "unknown";
        }

        if (normal.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return normal.ToString();
        }

        var bytes = normal.GetAddressBytes();
        for (var bit = ipv6PrefixLength; bit < bytes.Length * 8; bit++)
        {
            bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
        }

        return $"{new IPAddress(bytes)}/{ipv6PrefixLength}";
    }

    /// <summary>::ffff:203.0.113.7 and 203.0.113.7 are one visitor.</summary>
    private static IPAddress? Normalize(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;
}
