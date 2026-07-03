using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ChildCare.Api.Tests;

public record CapturedLogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>
/// In-memory log sink so integration tests can assert something was logged server-side
/// (e.g. FR-008a's "the real exception is logged, never returned to the client") without
/// depending on console/file output (tasks.md T030).
/// </summary>
public class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose() { }

    private class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new CapturedLogEntry(logLevel, category, formatter(state, exception), exception));
    }
}
