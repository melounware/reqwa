using System.IO;
using System.Text;

namespace ReqwaColors.Services;

/// <summary>
/// Minimal async-friendly file logger. Writes to %LOCALAPPDATA%\ReqwaColors\logs.
/// Kept intentionally tiny: no third-party deps, no locking contention in
/// normal operation (single-writer UI process).
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static string? _logFilePath;

    public static string LogDirectory
    {
        get
        {
            if (_logFilePath is null)
                Initialize();
            return Path.GetDirectoryName(_logFilePath!)!;
        }
    }

    private static void Initialize()
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReqwaColors", "logs");
            Directory.CreateDirectory(dir);
            _logFilePath = Path.Combine(dir, $"reqwa-{DateTime.Now:yyyy-MM-dd}.log");
        }
        catch (UnauthorizedAccessException)
        {
            // Fall back to temp path if LocalApplicationData is inaccessible.
            _logFilePath = Path.Combine(Path.GetTempPath(), $"reqwa-colors-{Environment.UserName}.log");
        }
        catch
        {
            // Logging must never take the app down.
            _logFilePath = Path.Combine(Path.GetTempPath(), "reqwa-colors.log");
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}\n{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            if (_logFilePath is null)
                Initialize();

            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(level).Append("] ")
                .AppendLine(message);

            lock (Gate)
            {
                File.AppendAllText(_logFilePath!, line.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Swallow: a failing logger must not crash the app.
        }
    }
}