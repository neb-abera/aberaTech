using System.Net;
using aberaTech.Server.Tests.Support;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// The parts of the migrate-as-a-separate-step design that need no database.
/// The part that does is DatabaseLeastPrivilegeTests.
/// </summary>
public sealed class DatabaseMigrationsTests
{
    /// <summary>Nothing listens here, and the refusal is immediate.</summary>
    internal const string Unreachable = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2";

    [Theory]
    [InlineData(new[] { "migrate" }, true)]
    [InlineData(new[] { "migrate", "--ConnectionStrings:Scheduling=x" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--migrate" }, false)]
    [InlineData(new[] { "--urls=http://*:8080", "migrate" }, false)]
    [InlineData(new[] { "Migrate" }, false)]
    public void Only_the_leading_verb_asks_for_a_migration(string[] args, bool requested)
    {
        Assert.Equal(requested, DatabaseMigrations.IsRequested(args));
    }

    [Fact]
    public async Task A_server_that_does_not_migrate_boots_without_its_database_and_says_it_is_not_ready()
    {
        // With migrate-on-start the process cannot outlive an unreachable
        // database. Without it there is no reason to die: the pages still
        // serve, and readiness is where "the database is gone" belongs.
        using var app = new TestApp(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Scheduling"] = Unreachable,
            ["Database:MigrateOnStart"] = "false"
        });
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/readyz")).StatusCode);
    }

    [Fact]
    public void Migrate_on_start_stays_the_default()
    {
        Assert.True(new aberaTech.Postgres.DatabaseOptions().MigrateOnStart);
    }
}
