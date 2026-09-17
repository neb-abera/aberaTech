using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// The identity that answers anonymous traffic should not own the schema. The
/// owner role applies migrations as a separate step; the app then serves as a
/// runtime role that can read and write rows and nothing else.
/// </summary>
public sealed class DatabaseLeastPrivilegeTests : IDisposable
{
    private const string Password = "test-only";
    private const string DigestKey = "0123456789abcdef0123456789abcdef-test";

    private readonly string _suffix = Guid.NewGuid().ToString("N")[..12];
    private readonly TestDatabase? _scheduling;
    private readonly TestDatabase? _fitness;

    public DatabaseLeastPrivilegeTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        var admin = TestPostgres.AdminConnectionString!;
        TestDatabase.Execute(admin, $"CREATE ROLE \"{Owner}\" LOGIN PASSWORD '{Password}'");
        TestDatabase.Execute(admin, $"CREATE ROLE \"{Runtime}\" LOGIN PASSWORD '{Password}'");

        _scheduling = new TestDatabase("lp_scheduling", Owner);
        _fitness = new TestDatabase("lp_fitness", Owner);
    }

    private string Owner => $"owner_{_suffix}";

    private string Runtime => $"runtime_{_suffix}";

    [PostgresFact]
    public async Task The_app_serves_as_a_runtime_role_that_cannot_change_the_schema()
    {
        // 1. The owner applies migrations as its own step, through the same
        //    entry point the image has: `dotnet aberaTech.Server.dll migrate`.
        var migrated = await RunServerAsync(
            "migrate",
            $"--ConnectionStrings:Scheduling={_scheduling!.ConnectionStringFor(Owner, Password)}",
            $"--ConnectionStrings:Fitness={_fitness!.ConnectionStringFor(Owner, Password)}");
        Assert.Equal(0, migrated);

        // 2. The owner hands the runtime role its grants, from the checked-in
        //    script, once per database.
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "least-privilege.sql"));
        foreach (var database in new[] { _scheduling, _fitness })
        {
            await using var connection = new NpgsqlConnection(database.ConnectionStringFor(Owner, Password));
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"SET abera.runtime_role = '{Runtime}'; SET abera.owner_role = '{Owner}'; {script}",
                connection);
            await command.ExecuteNonQueryAsync();
        }

        // 3. The app, as deployed, with the runtime role and migrate-on-start
        //    off. It has to boot, be ready, and do real reads and writes.
        using var app = new TestApp(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Scheduling"] = _scheduling.ConnectionStringFor(Runtime, Password),
            ["ConnectionStrings:Fitness"] = _fitness.ConnectionStringFor(Runtime, Password),
            ["Database:MigrateOnStart"] = "false",
            ["Admin:GoogleClientId"] = "test-client",
            ["Admin:GoogleClientSecret"] = "test-secret",
            ["Admin:AllowedEmails:0"] = "owner@example.test",
            ["Fitness:AllowedEmails:0"] = "owner@example.test",
            ["Fitness:DigestKey"] = DigestKey
        });
        using var client = app.CreateClient();

        var ready = await client.GetAsync("/readyz");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var checks = (await ready.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checks");
        Assert.All(checks.EnumerateArray(), check => Assert.True(check.GetProperty("configured").GetBoolean()));

        using (var scope = app.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var now = SystemClock.Instance.GetCurrentInstant();
            database.QueueSessions.Add(new QueueSession
            {
                Id = Guid.NewGuid(),
                Name = "Least privilege",
                OpensAt = now,
                ClosesAt = now + Duration.FromHours(4),
                Open = true
            });
            await database.SaveChangesAsync();
        }

        var joined = await client.PostAsJsonAsync("/api/scheduling/queue", new { name = "Private Snuffy" });
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);

        var id = (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/scheduling/queue/{id}")).StatusCode);

        using var digest = new HttpRequestMessage(HttpMethod.Get, "/api/fitness/digest.txt");
        digest.Headers.Authorization = new("Bearer", DigestKey);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(digest)).StatusCode);

        // 4. And the point of the exercise: no DDL, in either database.
        foreach (var database in new[] { _scheduling, _fitness })
        {
            var asRuntime = database.ConnectionStringFor(Runtime, Password);

            AssertDenied(asRuntime, "CREATE TABLE intruder (id int)");
            AssertDenied(asRuntime, "CREATE SCHEMA intruder");
            AssertDenied(asRuntime, "CREATE TEMP TABLE intruder (id int)");
            AssertDenied(asRuntime, "INSERT INTO \"__EFMigrationsHistory\" VALUES ('x', 'y')");
        }

        AssertDenied(_scheduling.ConnectionStringFor(Runtime, Password), "ALTER TABLE \"QueueEntries\" ADD COLUMN intruder int");
        AssertDenied(_scheduling.ConnectionStringFor(Runtime, Password), "DROP TABLE \"QueueEntries\"");
        AssertDenied(_scheduling.ConnectionStringFor(Runtime, Password), "TRUNCATE \"QueueEntries\"");
        AssertDenied(_fitness.ConnectionStringFor(Runtime, Password), "ALTER TABLE \"Settings\" ADD COLUMN intruder int");
        AssertDenied(_fitness.ConnectionStringFor(Runtime, Password), "DROP TABLE \"Settings\"");
    }

    [PostgresFact]
    public void With_migrate_on_start_off_the_app_refuses_to_serve_a_schema_it_does_not_match()
    {
        // An unmigrated database and a runtime that may not migrate it: the
        // honest outcome is a revision that fails to start, which leaves the
        // previous one serving, rather than one that boots and answers 500.
        using var app = new TestApp(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Scheduling"] = _scheduling!.ConnectionStringFor(Owner, Password),
            ["Database:MigrateOnStart"] = "false"
        });

        var failure = Assert.ThrowsAny<Exception>(() => app.CreateClient());
        Assert.Contains("migrate", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertDenied(string connectionString, string sql)
    {
        var failure = Assert.Throws<PostgresException>(() => TestDatabase.Execute(connectionString, sql));

        // insufficient_privilege. Anything else means the statement failed for
        // a reason that proves nothing about the role.
        Assert.Equal("42501", failure.SqlState);
    }

    /// <summary>The server's own entry point, as the image would run it.</summary>
    private static async Task<int> RunServerAsync(params string[] args)
    {
        var run = Task.Run(() =>
        {
            var result = typeof(Program).Assembly.EntryPoint!.Invoke(null, [args]);
            return result as int? ?? Environment.ExitCode;
        });

        // A command that never returns is a web server that started instead.
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromMinutes(2)));
        Assert.True(finished == run, "The migrate command did not exit.");
        return await run;
    }

    public void Dispose()
    {
        _scheduling?.Dispose();
        _fitness?.Dispose();

        if (TestPostgres.AdminConnectionString is { } admin)
        {
            TestDatabase.Execute(admin, $"DROP ROLE IF EXISTS \"{Runtime}\"");
            TestDatabase.Execute(admin, $"DROP ROLE IF EXISTS \"{Owner}\"");
        }
    }
}
