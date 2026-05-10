using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FasterChantDevice.Services;

/// <summary>
/// Structured debug logger with file output and callback events.
/// Logs to %LocalAppData%\FasterChantDevice\debug.log.
/// Disabled unless AppSettings.DebugMode = true.
/// </summary>
public static class DebugLogger
{
    private static readonly object _lock = new();
    private static string? _logPath;
    private static bool _enabled;
    private static string _minLevel = "Info";

    // Levels ordered for filtering
    private static readonly string[] LevelOrder = { "Trace", "Debug", "Info", "Warning", "Error" };

    /// <summary>
    /// Raised on every log entry (UI thread-safe via SynchronizationContext).
    /// </summary>
    public static event Action<LogEntry>? OnLog;

    /// <summary>
    /// Enable debug logging with file output.
    /// </summary>
    public static void Enable(string dataDir, string minLevel = "Info")
    {
        lock (_lock)
        {
            _enabled = true;
            _minLevel = minLevel;
            _logPath = Path.Combine(dataDir, "debug.log");

            // Rotate: rename old log if > 10MB
            try
            {
                var fi = new FileInfo(_logPath);
                if (fi.Exists && fi.Length > 10 * 1024 * 1024)
                {
                    var backup = _logPath + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(_logPath, backup);
                }
            }
            catch { /* best effort */ }

            // Write header
            WriteRaw($"[FasterChantDevice Debug Log] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteRaw(new string('-', 60));
        }
    }

    public static bool IsEnabled => _enabled;

    public static void Trace(string message, string? category = null) => Log("Trace", message, category);
    public static void Debug(string message, string? category = null) => Log("Debug", message, category);
    public static void Info(string message, string? category = null) => Log("Info", message, category);
    public static void Warning(string message, string? category = null) => Log("Warning", message, category);
    public static void Error(string message, string? category = null) => Log("Error", message, category);
    public static void Error(Exception ex, string? context = null)
    {
        var msg = context != null ? $"{context}: {ex}" : ex.ToString();
        Log("Error", msg, "Exception");
    }

    private static void Log(string level, string message, string? category)
    {
        if (!_enabled) return;
        if (LevelIndex(level) < LevelIndex(_minLevel)) return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Category = category ?? "General",
            Message = message
        };

        WriteRaw(entry.ToLine());
        OnLog?.Invoke(entry);
    }

    private static void WriteRaw(string line)
    {
        if (_logPath == null) return;
        lock (_lock)
        {
            try { File.AppendAllText(_logPath, line + Environment.NewLine); }
            catch { /* best effort */ }
        }
    }

    private static int LevelIndex(string level) =>
        Array.IndexOf(LevelOrder, level) is var i && i >= 0 ? i : 2;
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Category { get; set; } = "General";
    public string Message { get; set; } = "";

    public string ToLine() =>
        $"{Timestamp:HH:mm:ss.fff} [{Level,-7}] [{Category}] {Message}";
}
