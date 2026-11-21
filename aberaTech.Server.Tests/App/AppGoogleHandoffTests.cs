using System.Security.Claims;
using aberaTech.Scheduling.AppAccess;
using aberaTech.Server.App;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace aberaTech.Server.Tests.App;

/// <summary>
/// The moment Google's answer arrives: a sign-in the app started goes to
/// the app's scheme with a code and sets no cookie, and a sign-in on the
/// site goes on as before.
/// </summary>
public sealed class AppGoogleHandoffTests : IDisposable
{
    private readonly MemoryAppAccessStore _store = new();
    private readonly TestApp _app;

    public AppGoogleHandoffTests()
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["App:Household:0:Name"] = "Sam";
        settings["App:Household:0:Emails:0"] = "member.second@example.test";
        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IAppAccessStore>();
            services.AddSingleton<IAppAccessStore>(_store);
        });
    }

    public void Dispose() => _app.Dispose();

    private async Task<TicketReceivedContext> ReceiveAsync(bool fromApp)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var properties = new AuthenticationProperties { RedirectUri = "/schedule/admin" };
        if (fromApp)
        {
            properties.Items[IAppSignInHandoff.ChallengeItem] = AppTokens.Challenge(AppTokens.NewCode() + "-verifier");
            properties.Items["app.state"] = "s9";
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "member.second@example.test")], "Google"));
        var context = new TicketReceivedContext(http, new AuthenticationScheme("Google", null, typeof(GoogleHandler)), options,
            new AuthenticationTicket(principal, properties, "Google"));
        await options.Events.TicketReceived(context);
        return context;
    }

    [Fact]
    public async Task A_sign_in_the_app_started_goes_back_to_the_app_with_a_code()
    {
        var context = await ReceiveAsync(fromApp: true);

        Assert.True(context.Result?.Handled);
        Assert.StartsWith("aberatech://signin?state=s9&code=", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task A_sign_in_on_the_site_is_left_alone()
    {
        var context = await ReceiveAsync(fromApp: false);

        Assert.Null(context.Result);
        Assert.Equal(0, _store.Sessions);
    }
}
