namespace aberaTech.Scheduling.Sms;

/// <summary>Says one thing, once, at startup.</summary>
/// <remarks>
/// A misconfiguration that degrades silently is worse than one that fails,
/// because the symptom shows up somewhere else entirely. This exists so the
/// reason is in the log at boot rather than inferred later from a pile of dead
/// lettered messages.
/// </remarks>
public sealed class StartupWarning(ILogger logger, string message) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogError("{Message}", message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
