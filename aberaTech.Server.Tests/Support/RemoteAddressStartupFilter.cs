using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace aberaTech.Server.Tests.Support;

/// <summary>
/// Gives a TestServer request the socket address a real connection would have.
/// </summary>
/// <remarks>
/// TestServer has no socket, so <c>Connection.RemoteIpAddress</c> is null and
/// everything keyed on "who is calling" collapses into one caller. This runs
/// before the application's own pipeline and sets the address from a header
/// only the tests send, which stands in for the TCP peer: the hop directly in
/// front of the app. What the app then does with X-Forwarded-For on top of
/// that peer is exactly what is under test.
/// </remarks>
public sealed class RemoteAddressStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Peer";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, following) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var peer)
                && IPAddress.TryParse(peer.ToString(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
                context.Request.Headers.Remove(Header);
            }

            return following();
        });

        next(app);
    };
}
