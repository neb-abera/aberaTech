using System.Net.Sockets;
using aberaTech.Server;
using Npgsql;
using Xunit;

namespace aberaTech.Server.Tests.Security;

public sealed class MigrationFailureTests
{
    [Fact]
    public void A_failure_names_its_causes_and_the_postgres_code_and_no_message()
    {
        var refused = new NpgsqlException(
            "Host=abera-postgres;Password=secret-token",
            new PostgresException("password authentication failed", "FATAL", "FATAL", "28P01"));
        var unreachable = new NpgsqlException("Password=secret-token", new SocketException(110));

        Assert.Equal("NpgsqlException from PostgresException 28P01", DatabaseMigrations.Describe(refused));
        Assert.Equal("NpgsqlException from SocketException", DatabaseMigrations.Describe(unreachable));
        Assert.DoesNotContain("secret", DatabaseMigrations.Describe(refused));
    }
}
