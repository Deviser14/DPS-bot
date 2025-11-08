using System;
using System.IO;
using System.Threading;

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
        private static readonly string LogDirectory = "Data/Logs";
        private static readonly object _lock = new object();

        public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;
        public static bool WriteToConsole { get; set; } = true;

        public static void LogInfo(string message) => WriteLog(LogLevel.Info, message);
        public static void LogError(string message) => WriteLog(LogLevel.Error, message);
        public static void LogDebug(string message) => WriteLog(LogLevel.Debug, message);
        public static void LogWarning(string message) => WriteLog(LogLevel.Warning, message);

        private static void WriteLog(LogLevel level, string message)
        {
            if (level < MinimumLevel) return;

            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(LogDirectory))
                        Directory.CreateDirectory(LogDirectory);

                    string fileName = $"{level.ToString().ToLower()}_{DateTime.Now:yyyy-MM-dd}.log";
                    string fullPath = Path.Combine(LogDirectory, fileName);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string formatted = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}{Environment.NewLine}";

                    File.AppendAllText(fullPath, formatted);
                    Console.WriteLine($"[DEBUG] Current Directory: {Directory.GetCurrentDirectory()}");

                    if (WriteToConsole)
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
                try
                {
                    Console.WriteLine($"[Logger] Ошибка записи лога: {ex.Message}");
                }
                catch { }
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
                var files = Directory.GetFiles(LogDirectory, "*.log");
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