using System;
using System.IO;

namespace DPS_bot.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class LoggerService
    {
        private static string _logDirectory = "Data/Logs";
        private static LogLevel _minimumLevel = LogLevel.Debug;
        private static bool _writeToConsole = true;
        private static readonly object _lock = new();

        public static void Configure(LoggerConfig config)
        {
            _logDirectory = config.LogDirectory;
            _minimumLevel = Enum.TryParse(config.MinimumLevel, true, out LogLevel level) ? level : LogLevel.Debug;
            _writeToConsole = config.WriteToConsole;
        }

        public static void LogInfo(string message) => WriteLog(LogLevel.Info, message);
        public static void LogError(string message) => WriteLog(LogLevel.Error, message);
        public static void LogDebug(string message) => WriteLog(LogLevel.Debug, message);
        public static void LogWarning(string message) => WriteLog(LogLevel.Warning, message);

        private static void WriteLog(LogLevel level, string message)
        {
            if (level < _minimumLevel) return;

            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(_logDirectory))
                        Directory.CreateDirectory(_logDirectory);

                    string fileName = $"{level.ToString().ToLower()}_{DateTime.Now:yyyy-MM-dd}.log";
                    string fullPath = Path.Combine(_logDirectory, fileName);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string formatted = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}{Environment.NewLine}";

                    File.AppendAllText(fullPath, formatted);

                    if (_writeToConsole)
                    {
                        var prevColor = Console.ForegroundColor;
                        Console.ForegroundColor = GetColorForLevel(level);
                        Console.Write(formatted);
                        Console.ForegroundColor = prevColor;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger] Ошибка записи лога: {ex.Message}");
            }
        }

        private static ConsoleColor GetColorForLevel(LogLevel level) =>
            level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

        public static void CleanOldLogs(int daysToKeep = 30)
        {
            try
            {
                var files = Directory.GetFiles(_logDirectory, "*.log");
                foreach (var file in files)
                {
                    var creationTime = File.GetCreationTime(file);
                    if ((DateTime.Now - creationTime).TotalDays > daysToKeep)
                    {
                        File.Delete(file);
                        Console.WriteLine($"[Logger] Удалён старый лог: {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger] Ошибка при очистке логов: {ex.Message}");
            }
        }
    }
}