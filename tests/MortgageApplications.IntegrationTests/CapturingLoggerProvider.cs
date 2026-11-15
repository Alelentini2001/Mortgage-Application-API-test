using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MortgageApplications.IntegrationTests;

/// <summary>Keeps every log entry (message and exception text) so tests can check what reaches the logs (I-14).</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    public sealed record LogEntry(string Category, LogLevel Level, string? EventName, string Message, string? Exception)
    {
        /// <summary>Everything a log sink would store for this entry.</summary>
        public string Text => $"{Category} {Level} {Message} {Exception}";
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, eventId.Name, formatter(state, exception), exception?.ToString()));
    }
}
