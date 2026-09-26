using aberaTech.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace aberaTech.Server.Tests.Api;

public sealed class PostgresDataSourceTests
{
    [Fact]
    public void No_connection_asks_for_GSS_encryption()
    {
        // Npgsql prefers GSS encryption by default, so every physical
        // connection first loads libgssapi_krb5. The chiseled image has no
        // such library, and each boot logged "Error: libgssapi_krb5.so.2:
        // cannot open shared object file" twice. Azure Database for
        // PostgreSQL does not offer GSS encryption. TLS is the transport.
        using var source = PostgresDataSource.Build(
            "Host=localhost;Username=app;Password=unused", new DatabaseOptions(), NullLoggerFactory.Instance);

        var settings = new NpgsqlConnectionStringBuilder(source.ConnectionString);

        Assert.Equal(GssEncryptionMode.Disable, settings.GssEncryptionMode);
    }

    [Fact]
    public void A_connection_string_that_names_a_GSS_mode_keeps_it()
    {
        using var source = PostgresDataSource.Build(
            "Host=localhost;Username=app;Password=unused;GSS Encryption Mode=Prefer",
            new DatabaseOptions(),
            NullLoggerFactory.Instance);

        var settings = new NpgsqlConnectionStringBuilder(source.ConnectionString);

        Assert.Equal(GssEncryptionMode.Prefer, settings.GssEncryptionMode);
    }
}
