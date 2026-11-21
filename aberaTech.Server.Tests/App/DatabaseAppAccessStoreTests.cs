using aberaTech.Scheduling.Data;
using aberaTech.Server.App;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.App;

/// <summary>
/// The app's codes and sessions in Postgres: a code works once and not
/// after 2 minutes, a token is stored as its hash, and signing out removes
/// the row.
/// </summary>
public sealed class DatabaseAppAccessStoreTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 21, 17, 0);

    private readonly TestDatabase? _database;

    public DatabaseAppAccessStoreTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("appaccess");
        using var context = Context();
        context.Database.Migrate();
    }

    public void Dispose() => _database?.Dispose();

    private SchedulingDbContext Context() =>
        new(new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(_database!.ConnectionString, npgsql => npgsql.UseNodaTime())
            .Options);

    [PostgresFact]
    public async Task A_code_is_redeemed_once_and_not_after_it_expires()
    {
        var code = AppTokens.NewCode();
        var late = AppTokens.NewCode();
        await using (var context = Context())
        {
            var store = new DatabaseAppAccessStore(context);
            await store.AddCodeAsync(AppTokens.Hash(code), "challenge", "Sam", Now + AppSignIn.CodeLifetime, CancellationToken.None);
            await store.AddCodeAsync(AppTokens.Hash(late), "challenge", "Sam", Now + AppSignIn.CodeLifetime, CancellationToken.None);
        }

        await using (var context = Context())
        {
            var store = new DatabaseAppAccessStore(context);
            Assert.Equal(("challenge", "Sam"), await store.RedeemCodeAsync(AppTokens.Hash(code), Now, CancellationToken.None));
            Assert.Null(await store.RedeemCodeAsync(AppTokens.Hash(code), Now, CancellationToken.None));
            Assert.Null(await store.RedeemCodeAsync(AppTokens.Hash(late), Now + Duration.FromMinutes(3), CancellationToken.None));
            Assert.Equal(0, await context.AppSignInCodes.CountAsync());
        }
    }

    [PostgresFact]
    public async Task A_session_is_found_by_its_hash_touched_and_removed_on_sign_out()
    {
        var token = AppTokens.New();
        var id = Guid.NewGuid();
        await using (var context = Context())
        {
            await new DatabaseAppAccessStore(context).AddSessionAsync(id, AppTokens.Hash(token), "Sam", "Sam's iPhone", Now, CancellationToken.None);
        }

        await using (var check = Context())
        {
            var text = await check.Database.SqlQuery<string>($"SELECT row_to_json(s)::text AS \"Value\" FROM \"AppSessions\" s").SingleAsync();
            Assert.DoesNotContain(token[4..], text);

            var store = new DatabaseAppAccessStore(check);
            var session = await store.FindSessionAsync(AppTokens.Hash(token), CancellationToken.None);
            Assert.Equal(("Sam", "Sam's iPhone"), (session!.Person, session.Device));
            await store.TouchAsync(id, Now + Duration.FromMinutes(5), CancellationToken.None);
            Assert.Equal(Now + Duration.FromMinutes(5), (await store.FindSessionAsync(AppTokens.Hash(token), CancellationToken.None))!.LastSeenAt);
            await store.RevokeAsync(id, CancellationToken.None);
            Assert.Null(await store.FindSessionAsync(AppTokens.Hash(token), CancellationToken.None));
        }
    }
}
