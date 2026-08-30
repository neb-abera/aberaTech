using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace aberaTech.Scheduling.Data;

/// <summary>How the application proves who it is to Postgres.</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>
    /// Authenticate as the container app's managed identity rather than with a
    /// password.
    /// </summary>
    /// <remarks>
    /// The point is that there is then no password: none in a container app
    /// secret, none in a deploy command, none in anybody's shell history, and
    /// nothing to rotate or leak. Azure mints a short-lived token for the
    /// identity Azure already knows the container has.
    ///
    /// Off by default so a developer machine and a plain connection string keep
    /// working unchanged.
    /// </remarks>
    public bool UseEntraAuth { get; set; }
}

public static class SchedulingDataSource
{
    /// <summary>
    /// The scope Azure Database for PostgreSQL issues access tokens for.
    /// </summary>
    private const string PostgresScope = "https://ossrdbms-aad.database.windows.net/.default";

    /// <summary>
    /// How often to fetch a fresh token.
    /// </summary>
    /// <remarks>
    /// Entra access tokens last about an hour. Refreshing at fifty minutes
    /// leaves a margin wide enough that a connection opened just before the
    /// refresh is not authenticated with a token about to expire, and narrow
    /// enough that a failure to refresh is noticed while the old one still
    /// works.
    /// </remarks>
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(50);

    /// <summary>How long to keep using a token after a refresh attempt fails.</summary>
    private static readonly TimeSpan FailureRetry = TimeSpan.FromSeconds(10);

    public static NpgsqlDataSource Build(string connectionString, DatabaseOptions options, ILoggerFactory loggers)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // The shared abera-postgres server allows 50 connections and also
        // serves Facewoof (pool 10 x up to 3 replicas). Npgsql's default
        // Maximum Pool Size is 100 - one busy replica could exhaust the whole
        // server. Pin it unless the connection string already says otherwise.
        if (!connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("MaxPoolSize", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectionStringBuilder.MaxPoolSize = 10;
        }

        builder.UseNodaTime();
        builder.UseLoggerFactory(loggers);

        if (!options.UseEntraAuth)
        {
            return builder.Build();
        }

        // DefaultAzureCredential resolves to the container app's managed
        // identity when deployed, and to the developer's az login locally, so
        // the same configuration works in both places without a branch.
        var credential = new DefaultAzureCredential();

        builder.UsePeriodicPasswordProvider(
            async (_, cancellationToken) =>
            {
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext([PostgresScope]),
                    cancellationToken);

                // The token *is* the password. Npgsql sends it as one, and
                // Postgres validates it with Entra.
                return token.Token;
            },
            RefreshEvery,
            FailureRetry);

        return builder.Build();
    }
}
