using System.Data.Common;
using aberaTech.Fitness.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The real server with the fitness surface switched on, over an in-memory
/// SQLite database instead of Postgres.
/// </summary>
/// <remarks>
/// SQLite rather than EF's in-memory provider because the point of several
/// tests is how many commands a request sends: only a relational provider
/// sends commands at all, so only a relational provider can be counted.
/// Nothing in production changes to make this possible — the context is
/// subclassed to teach SQLite the two NodaTime types, and the migrator is
/// swapped for one that creates the schema from the model, because the
/// committed migrations are written in Postgres' dialect.
/// </remarks>
internal sealed class FitnessApp : IDisposable
{
    /// <summary>Midday, mid-month, so nothing seeded sits on a boundary.</summary>
    public static readonly Instant Now = Instant.FromUtc(2026, 6, 15, 12, 0);

    private readonly string _webRoot;
    private readonly SqliteConnection _connection;

    public FitnessApp(Action<IWebHostBuilder>? configure = null)
    {
        _webRoot = Directory.CreateTempSubdirectory("wwwroot-fitness-app").FullName;
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html>home</html>");
        File.WriteAllText(Path.Combine(_webRoot, "spa.html"), "<html>shell</html>");

        // One open connection is what keeps an in-memory SQLite database alive.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseWebRoot(_webRoot);
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Scheduling", "");
            // Never dialled: the options that would read it are replaced below.
            builder.UseSetting("ConnectionStrings:Fitness", "Host=unused.invalid;Database=fitness");
            builder.UseSetting("Fitness:DevelopmentOwner", "true");
            configure?.Invoke(builder);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<FitnessDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<FitnessDbContext>>();
                services.RemoveAll<FitnessDbContext>();

                services.AddDbContext<FitnessDbContext>(options => options
                    .UseSqlite(_connection)
                    .ReplaceService<IMigrator, CreateFromModel>()
                    .AddInterceptors(Commands));
                services.RemoveAll<FitnessDbContext>();
                services.AddScoped<FitnessDbContext>(provider =>
                    new SqliteFitnessDbContext(provider.GetRequiredService<DbContextOptions<FitnessDbContext>>()));

                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(Now));
            });
        });
    }

    public WebApplicationFactory<Program> Factory { get; }

    /// <summary>Every command the fitness database has been sent.</summary>
    public CommandCounter Commands { get; } = new();

    public async Task SeedAsync(Func<FitnessDbContext, Task> seed)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        await seed(database);
        await database.SaveChangesAsync();
    }

    public void Dispose()
    {
        Factory.Dispose();
        _connection.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    internal sealed class FixedClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }

    /// <summary>Counts commands as they leave for the database, whatever their shape.</summary>
    internal sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// The production model, with the two NodaTime types stored as values
    /// SQLite can hold. Both conversions keep their order, which is all the
    /// queries ask of them.
    /// </summary>
    private sealed class SqliteFitnessDbContext(DbContextOptions<FitnessDbContext> options) : FitnessDbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<Instant>().HaveConversion<InstantAsTicks>();
            configurationBuilder.Properties<LocalDate>().HaveConversion<LocalDateAsDays>();
        }

        private sealed class InstantAsTicks() : ValueConverter<Instant, long>(
            instant => instant.ToUnixTimeTicks(), ticks => Instant.FromUnixTimeTicks(ticks));

        private sealed class LocalDateAsDays() : ValueConverter<LocalDate, int>(
            date => Period.DaysBetween(new LocalDate(1970, 1, 1), date),
            days => new LocalDate(1970, 1, 1).PlusDays(days));
    }

    /// <summary>Stands in for the Postgres migrations: the schema, straight from the model.</summary>
    private sealed class CreateFromModel(ICurrentDbContext current) : IMigrator
    {
        public void Migrate(string? targetMigration) => current.Context.Database.EnsureCreated();

        public Task MigrateAsync(string? targetMigration, CancellationToken cancellationToken = default) =>
            current.Context.Database.EnsureCreatedAsync(cancellationToken);

        public string GenerateScript(
            string? fromMigration = null, string? toMigration = null,
            MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default) =>
            throw new NotSupportedException();

        public bool HasPendingModelChanges() => false;
    }
}
