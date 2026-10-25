using System;
using System.IO;

namespace NexIsland.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public class LoggingService
    {
        private static readonly object _lock = new();
        private static LoggingService? _instance;
        public static LoggingService Instance => _instance ??= new LoggingService();

        private readonly string _logFilePath;

        public LoggingService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appData, "NexIsland", "logs");
            try
            {
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                _logFilePath = Path.Combine(logDir, "nexisland.log");

                // Check size limit: rotate if > 5MB
                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > 5 * 1024 * 1024)
                {
                    var backupPath = Path.Combine(logDir, $"nexisland_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.Move(_logFilePath, backupPath, true);
                }
            }
            catch
            {
                _logFilePath = Path.Combine(Path.GetTempPath(), "nexisland.log");
            }
        }

        public void Log(LogLevel level, string message, Exception? ex = null)
        {
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                if (ex != null)
                {
                    var cur = ex;
                    while (cur != null)
                    {
                        entry += $"{Environment.NewLine}Exception: {cur.GetType().Name}: {cur.Message}{Environment.NewLine}{cur.StackTrace}";
                        cur = cur.InnerException;
                    }
                }

                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, entry + Environment.NewLine);
                }

                System.Diagnostics.Debug.WriteLine(entry);
            }
            catch
            {
                // Never crash the app due to logging failure
            }
        }

        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warn(string message) => Log(LogLevel.Warn, message);
        public void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
        public void Debug(string message) => Log(LogLevel.Debug, message);
    }
}
