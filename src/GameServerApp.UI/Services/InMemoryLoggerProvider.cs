using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GameServerApp.UI.Services;

/// <summary>
/// In-memory log provider that captures all ILogger output for display in the UI.
/// </summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new();

    public static InMemoryLoggerProvider Instance { get; } = new();

    public event Action<LogEntry>? LogReceived;

    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private const int MaxEntries = 5000;

    public IReadOnlyCollection<LogEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    internal void AddEntry(LogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        LogReceived?.Invoke(entry);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new InMemoryLogger(name, this));
    }

    public void Dispose() { }
}

public sealed class InMemoryLogger : ILogger
{
    private readonly string _category;
    private readonly InMemoryLoggerProvider _provider;

    public InMemoryLogger(string category, InMemoryLoggerProvider provider)
    {
        _category = category;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception != null)
            message += $"\n{exception}";

        // Shorten the category name (e.g., "GameServerApp.Core.Services.ServerManager" → "ServerManager")
        var shortCategory = _category;
        var lastDot = _category.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < _category.Length - 1)
            shortCategory = _category[(lastDot + 1)..];

        _provider.AddEntry(new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = logLevel,
            Category = shortCategory,
            Message = message
        });
    }
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public string FormattedLine =>
        $"[{Timestamp:HH:mm:ss}] [{Level,-11}] [{Category}] {Message}";
}
