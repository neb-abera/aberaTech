using System.Runtime.CompilerServices;
using Npgsql;
using Xunit;
using Xunit.v3;

namespace aberaTech.Server.Tests.Support;

/// <summary>
/// The Postgres the database-backed tests run against, when there is one.
/// </summary>
/// <remarks>
/// The hermetic <c>servertest</c> image stage runs inside <c>docker build</c>,
/// where there is no database to reach, so these tests skip there. The compose
/// <c>servertest</c> service sets <see cref="Variable"/> to the compose
/// database's superuser connection, and both `make servertest` and the
/// "Compose database boots healthy" CI job run them for real.
///
/// A real Postgres rather than an in-memory provider on purpose: the queue
/// leans on unique indexes, the bookings on an exclusion constraint, and the
/// least-privilege tests are about Postgres roles. A second engine that has
/// none of those would prove nothing about the one that ships.
/// </remarks>
public static class TestPostgres
{
    public const string Variable = "ABERA_TEST_POSTGRES";

    /// <summary>Set by the compose service so an unset connection is a failure there, not a skip.</summary>
    public const string RequiredVariable = "ABERA_REQUIRE_POSTGRES";

    public static string? AdminConnectionString
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(Variable);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public static bool Required =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RequiredVariable));
}

/// <summary>A fact that needs the compose Postgres, and says so when it is absent.</summary>
/// <remarks>
/// Carries the trait <c>Category=Postgres</c>, which is how
/// scripts/server-db-tests.sh selects exactly these.
/// </remarks>
public sealed class PostgresFactAttribute : FactAttribute, ITraitAttribute
{
    // The caller's file and line pass through so the runner reports each test
    // at its own source location, as a plain [Fact] does.
    public PostgresFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        // Where the database is promised (compose, CI) the test runs and fails
        // on its own if the connection is missing, so a misconfigured job
        // cannot pass by skipping everything it was there to run.
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required)
        {
            Skip = $"Needs Postgres: set {TestPostgres.Variable} (the compose servertest service does).";
        }
    }

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>("Category", "Postgres")];
}

/// <summary>One throwaway database, created on the compose server and dropped afterwards.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _admin;

    public TestDatabase(string prefix = "t", string? owner = null)
    {
        _admin = TestPostgres.AdminConnectionString
                 ?? throw new InvalidOperationException($"{TestPostgres.Variable} is not set.");

        // Unique per instance: test classes run in parallel against one server.
        Name = $"{prefix}_{Guid.NewGuid():N}";
        Execute(_admin, owner is null
            ? $"CREATE DATABASE \"{Name}\""
            : $"CREATE DATABASE \"{Name}\" OWNER \"{owner}\"");
    }

    public string Name { get; }

    /// <summary>A connection string to this database as the server's admin user.</summary>
    public string ConnectionString => ConnectionStringFor(null, null);

    /// <summary>A connection string to this database as another role.</summary>
    public string ConnectionStringFor(string? username, string? password)
    {
        var builder = new NpgsqlConnectionStringBuilder(_admin) { Database = Name };
        if (username is not null)
        {
            builder.Username = username;
            builder.Password = password;
        }

        // Small pools: several test hosts are alive at once and the compose
        // server has the default hundred connections to share between them.
        builder.MaxPoolSize = 5;
        return builder.ConnectionString;
    }

    public static void Execute(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        NpgsqlConnection.ClearAllPools();
        Execute(_admin, $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
    }
}
