using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SurveillanceAI.Desktop;
public sealed class LogPreferences { public LogLevel MinimumLevel { get; set; } = LogLevel.Information; }
public sealed class FileLogProvider(string directory, LogPreferences preferences) : ILoggerProvider
{
    private readonly object gate = new();
    private readonly string directory = directory;
    private readonly LogPreferences preferences = preferences;
    public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);
    public void Dispose() { }
    private sealed class Sink(FileLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= owner.preferences.MinimumLevel && !category.StartsWith("Microsoft.EntityFrameworkCore");
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            // Call sites use identifiers/error types only. Do not serialize arbitrary exception content.
            var json = JsonSerializer.Serialize(new { utc = DateTime.UtcNow, level = level.ToString(), category, message = formatter(state, null), errorType = error?.GetType().Name });
            lock (owner.gate)
            {
                try
                {
                    Directory.CreateDirectory(owner.directory);
                    File.AppendAllText(Path.Combine(owner.directory, $"app-{DateTime.UtcNow:yyyyMMdd}.log"), json + Environment.NewLine);
                    foreach (var old in Directory.EnumerateFiles(owner.directory, "app-*.log").Where(x => File.GetLastWriteTimeUtc(x) < DateTime.UtcNow.AddDays(-14))) File.Delete(old);
                }
                catch (IOException) { /* A log sink failure must not stop camera monitoring. */ }
                catch (UnauthorizedAccessException) { /* Permissions may change while monitoring. */ }
            }
        }
    }
}
