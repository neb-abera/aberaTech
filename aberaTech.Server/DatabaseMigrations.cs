using aberaTech.Fitness.Data;
using aberaTech.Postgres;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace aberaTech.Server;

/// <summary>
/// Who applies schema migrations, and when.
/// </summary>
/// <remarks>
/// Migrating on start is convenient and means the identity that answers
/// anonymous traffic also owns every table: an injection anywhere in the app
/// is then an injection with DROP TABLE. The alternative is two identities —
/// an owner that runs <c>dotnet aberaTech.Server.dll migrate</c> as a step of
/// its own, and a runtime role limited to rows (see
/// aberaTech.Postgres/Sql/least-privilege.sql) that serves requests with
/// <c>Database:MigrateOnStart=false</c>.
///
/// The switch defaults to today's behaviour, so nothing changes until the
/// roles exist. aberaTech.Postgres/Sql/README.md is the switch-over.
/// </remarks>
public static class DatabaseMigrations
{
    /// <summary>The command-line verb that applies migrations and exits.</summary>
    public const string Verb = "migrate";

    /// <summary>
    /// A verb rather than a <c>--switch</c>: the command-line configuration
    /// provider reads a bare switch as a key and swallows the argument after
    /// it as its value.
    /// </summary>
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Verb, StringComparison.Ordinal);

    /// <summary>
    /// Applies every pending migration to each configured database, as
    /// whatever identity the connection strings name. Returns the exit code.
    /// </summary>
    /// <remarks>
    /// Built from the connection strings rather than resolved from the
    /// container: the fitness context is only registered when its sign-in
    /// allowlist is configured, and a migrator has no reason to carry the
    /// app's Google credentials just to be allowed to create a table.
    /// </remarks>
    public static async Task<int> RunAsync(IConfiguration configuration, ILoggerFactory loggers)
    {
        var logger = loggers.CreateLogger("DatabaseMigrations");
        var options = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();

        try
        {
            await MigrateAsync(
                "scheduling", configuration.GetConnectionString("Scheduling"), options, loggers, logger,
                source => new SchedulingDbContext(
                    new DbContextOptionsBuilder<SchedulingDbContext>()
                        .UseNpgsql(source, npgsql => npgsql.UseNodaTime()).Options));

            await MigrateAsync(
                "fitness", configuration.GetConnectionString("Fitness"), options, loggers, logger,
                source => new FitnessDbContext(
                    new DbContextOptionsBuilder<FitnessDbContext>()
                        .UseNpgsql(source, npgsql => npgsql.UseNodaTime()).Options));

            return 0;
        }
        catch (Exception exception)
        {
            // The type and the database name only, for the reason /readyz
            // gives: provider messages can carry a connection string.
            logger.LogCritical("Migration failed with {Failure}.", exception.GetType().Name);
            return 1;
        }
    }

    /// <summary>
    /// At start-up: migrate when that is this process's job, and otherwise
    /// refuse to serve a schema that is behind the code.
    /// </summary>
    public static async Task PrepareAsync<TContext>(this WebApplication app, string name)
        where TContext : DbContext
    {
        var options = app.Configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrations");

        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<TContext>().Database;

        if (options.MigrateOnStart)
        {
            await database.MigrateAsync();
            return;
        }

        IReadOnlyList<string> pending;
        try
        {
            pending = [.. await database.GetPendingMigrationsAsync()];
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            // Unreachable is /readyz's to report, and a process that is not
            // migrating has no other reason to need the database before its
            // first request. Only a schema known to be behind stops the boot.
            logger.LogWarning(
                "Could not read the {Database} migration history at start ({Failure}); /readyz reports reachability.",
                name,
                exception.GetType().Name);
            return;
        }

        if (pending.Count > 0)
        {
            // Failing the boot fails the revision, which leaves the previous
            // one serving. Booting anyway would answer 500 on every route the
            // missing migration touches, behind a green deploy.
            throw new InvalidOperationException(
                $"The {name} database has {pending.Count} pending migration(s) and Database:MigrateOnStart is false. "
                + $"Run `dotnet aberaTech.Server.dll {Verb}` as the owner role first.");
        }
    }

    private static async Task MigrateAsync<TContext>(
        string name,
        string? connectionString,
        DatabaseOptions options,
        ILoggerFactory loggers,
        ILogger logger,
        Func<NpgsqlDataSource, TContext> create)
        where TContext : DbContext
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogInformation("No {Database} connection string; nothing to migrate.", name);
            return;
        }

        await using var source = PostgresDataSource.Build(connectionString, options, loggers);
        await using var context = create(source);

        var pending = (await context.Database.GetPendingMigrationsAsync()).Count();
        await context.Database.MigrateAsync();
        logger.LogInformation("Applied {Count} migration(s) to {Database}.", pending, name);
    }
}
