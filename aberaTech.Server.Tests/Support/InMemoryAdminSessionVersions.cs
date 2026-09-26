using System.Collections.Concurrent;
using aberaTech.Scheduling.Admin;

namespace aberaTech.Server.Tests.Support;

/// <summary>
/// Session versions for a test host with no database it can reach. The
/// cookie scheme refuses every admin cookie when there is no store at all, so
/// a host whose scheduling database is a placeholder gets this one.
/// SessionRevocationTests prove the database store the server ships.
/// </summary>
public sealed class InMemoryAdminSessionVersions : IAdminSessionVersions
{
    private readonly ConcurrentDictionary<string, int> _versions = new();

    public Task<int> CurrentAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_versions.GetValueOrDefault(AdminSessions.Key(email)));

    public Task RevokeAsync(string email, CancellationToken cancellationToken)
    {
        _versions.AddOrUpdate(AdminSessions.Key(email), 1, (_, version) => version + 1);
        return Task.CompletedTask;
    }
}
