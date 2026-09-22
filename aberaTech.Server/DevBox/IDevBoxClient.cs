namespace aberaTech.Server.DevBox;

/// <summary>The two things the page asks of the VM's host: its power state, and a start.</summary>
public interface IDevBoxClient
{
    Task<string> GetPowerStateAsync(CancellationToken cancellationToken);

    Task StartAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A VM that lives in this process, for Development and the browser suite.
/// Deallocated at first; Start makes it "starting" and, one tick later,
/// "running"; a Park order parks it, the way the real box parks itself.
/// </summary>
public sealed class FakeDevBoxClient(TimeProvider clock) : IDevBoxClient
{
    /// <summary>How long "starting" lasts before the fake answers "running".</summary>
    public static readonly TimeSpan StartTakes = TimeSpan.FromSeconds(3);

    private readonly Lock _lock = new();
    private string _power = "deallocated";
    private DateTimeOffset _startedAt;

    public Task<string> GetPowerStateAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_power == "starting" && clock.GetUtcNow() - _startedAt >= StartTakes)
            {
                _power = "running";
            }

            return Task.FromResult(_power);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_power != "running")
            {
                _power = "starting";
                _startedAt = clock.GetUtcNow();
            }
        }

        return Task.CompletedTask;
    }

    public void Park()
    {
        lock (_lock) _power = "deallocated";
    }
}
