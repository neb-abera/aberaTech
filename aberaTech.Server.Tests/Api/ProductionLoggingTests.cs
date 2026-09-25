using Microsoft.Extensions.Configuration;
using Xunit;

namespace aberaTech.Server.Tests.Api;

/// <summary>
/// The log levels appsettings.Production.json ships.
/// </summary>
/// <remarks>
/// A chatty logger is not a cosmetic problem. Measured on 2026-09-25 in the
/// aberaTechServer Log Analytics workspace: 32,009 console lines in 24 hours,
/// of which 9,576 were Azure.Identity headers and 22,127 their continuations,
/// every one of them saying a cached managed-identity token had been found.
/// The application's own loggers produced 278 lines in the same window. At
/// that ratio the useful lines are unfindable and the ingest is paid for
/// nothing.
///
/// Reading the shipped file rather than a running app, because the defect is
/// in what is deployed. A test that stood up the server would pass on a
/// configuration the container never sees.
/// </remarks>
public sealed class ProductionLoggingTests
{
    private static IConfiguration Shipped() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json"))
            .Build();

    [Theory]
    // The SDKs that talk constantly and say nothing at Information.
    [InlineData("Azure.Identity")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command")]
    [InlineData("System.Net.Http.HttpClient")]
    public void A_noisy_source_is_quiet_below_a_warning(string source)
    {
        Assert.Equal("Warning", Shipped()[$"Logging:LogLevel:{source}"]);
    }

    [Fact]
    public void The_applications_own_logs_are_not_silenced_with_them()
    {
        // The point is to cut the SDK chatter, not the lines that say what the
        // app did. Nothing under aberaTech may be raised above Information.
        var levels = Shipped().GetSection("Logging:LogLevel").GetChildren();

        Assert.DoesNotContain(
            levels,
            entry => entry.Key.StartsWith("aberaTech", StringComparison.Ordinal)
                     && entry.Value is not ("Information" or "Debug" or "Trace"));
    }
}
