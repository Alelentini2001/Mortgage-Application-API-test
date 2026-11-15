using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Keeps every log entry so tests can check what reaches the logs (I-14, Phase 5): the message and exception text,
/// the structured state (the fields an OpenTelemetry log exporter sends), the scope values, and the trace ID of
/// <see cref="Activity.Current"/> at log time.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    // The activity-tracking scope fields: IDs by construction, kept out of Text (see LogEntry.Text).
    private static readonly HashSet<string> _activityScopeKeys = ["SpanId", "TraceId", "ParentId"];

    private readonly ConcurrentQueue<LogEntry> _entries = new();

    // One provider is shared by every host of a factory, and each host's logger factory brings its own scope
    // provider; scopes live in an AsyncLocal per provider, so reading all of them finds the current flow's scopes.
    private readonly ConcurrentQueue<IExternalScopeProvider> _scopeProviders = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProviders.Enqueue(scopeProvider);

    public void Dispose()
    {
    }

    public sealed record LogEntry(
        string Category,
        LogLevel Level,
        string? EventName,
        string Message,
        string? Exception,
        IReadOnlyList<KeyValuePair<string, string?>> State,
        IReadOnlyList<KeyValuePair<string, string?>> Scopes,
        string? TraceId)
    {
        /// <summary>
        /// Everything a log sink would store for this entry: message, exception, state and scope values. The
        /// activity-tracking scope IDs (trace, span and parent ID) are left out: they are random hex, which could
        /// contain a sentinel's digits by chance; <see cref="TraceId"/> keeps the trace ID.
        /// </summary>
        public string Text =>
            $"{Category} {Level} {Message} {Exception} {Join(State)} {Join(Scopes.Where(s => !_activityScopeKeys.Contains(s.Key)))}";

        private static string Join(IEnumerable<KeyValuePair<string, string?>> pairs) =>
            string.Join(' ', pairs.Select(p => $"{p.Key}={p.Value}"));
    }

    private static List<KeyValuePair<string, string?>> Pairs(object? state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return [.. pairs.Select(p => new KeyValuePair<string, string?>(p.Key, Convert.ToString(p.Value, CultureInfo.InvariantCulture)))];
        }

        return state is null ? [] : [new KeyValuePair<string, string?>("", Convert.ToString(state, CultureInfo.InvariantCulture))];
    }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => provider._scopeProviders.LastOrDefault()?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<KeyValuePair<string, string?>>();
            foreach (var scopeProvider in provider._scopeProviders)
            {
                scopeProvider.ForEachScope((scope, list) => list.AddRange(Pairs(scope)), scopes);
            }

            provider._entries.Enqueue(new LogEntry(
                category,
                logLevel,
                eventId.Name,
                formatter(state, exception),
                exception?.ToString(),
                Pairs(state),
                scopes,
                Activity.Current?.TraceId.ToHexString()));
        }
    }
}
