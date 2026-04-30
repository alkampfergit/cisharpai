using Microsoft.Extensions.Logging;

namespace Cisharpai.Tests;

internal sealed class TestLogger<T> : ILogger<T>
{
    public List<TestLogEntry> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> structuredState
            ? structuredState.ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, object?>();

        Entries.Add(new TestLogEntry(
            logLevel,
            eventId,
            formatter(state, exception),
            properties,
            exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record TestLogEntry(
    LogLevel LogLevel,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);
