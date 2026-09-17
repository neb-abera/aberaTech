using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace aberaTech.Server.Tests.Support;

/// <summary>One log entry as written: its id, its rendered text and its structured fields.</summary>
public sealed record CapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Fields)
{
    /// <summary>Everything a log sink would receive, flattened for "this must not appear anywhere".</summary>
    public string Everything => Message + " | " + string.Join(" | ", Fields.Select(pair => $"{pair.Key}={pair.Value}"));
}

/// <summary>A logger provider that keeps what it is given, so a test can read the log a sink would.</summary>
public sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => [.. _entries];

    public IReadOnlyList<CapturedLog> InCategory(string category) =>
        [.. _entries.Where(entry => entry.Category == category)];

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                : [];

            entries.Enqueue(new CapturedLog(category, logLevel, eventId, formatter(state, exception), fields));
        }
    }
}
