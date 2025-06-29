using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace YAWDA.Utilities
{
    /// <summary>
    /// Enhanced logging configuration for YAWDA with file output and structured logging
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>
        /// Configures enhanced logging with file output and different severity levels
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="isDebugMode">Whether to enable debug logging</param>
        public static void ConfigureEnhancedLogging(this IServiceCollection services, bool isDebugMode = false)
        {
            services.AddLogging(builder =>
            {
                // Clear default providers
                builder.ClearProviders();

                // Add console logging for development
                builder.AddConsole();

                // Debug mode already covered by console logging with appropriate level

                // Add custom file logger
                builder.AddProvider(new FileLoggerProvider());

                // Set minimum log level based on mode
                var minLevel = isDebugMode ? LogLevel.Debug : LogLevel.Information;
                builder.SetMinimumLevel(minLevel);

                // Configure specific log levels for different namespaces
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
                builder.AddFilter("YAWDA.Services", LogLevel.Information);
                builder.AddFilter("YAWDA.ViewModels", LogLevel.Information);
                builder.AddFilter("YAWDA.Utilities", LogLevel.Warning);
            });
        }

        /// <summary>
        /// Gets the application log directory
        /// </summary>
        public static string GetLogDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(appDataPath, "YAWDA", "Logs");
            Directory.CreateDirectory(logDirectory);
            return logDirectory;
        }

        /// <summary>
        /// Cleans up old log files
        /// </summary>
        /// <param name="olderThanDays">Remove logs older than specified days</param>
        public static void CleanupOldLogs(int olderThanDays = 7)
        {
            try
            {
                var logDirectory = GetLogDirectory();
                var cutoffDate = DateTime.Now.AddDays(-olderThanDays);

                foreach (var file in Directory.GetFiles(logDirectory, "*.log"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors - not critical
            }
        }
    }

    /// <summary>
    /// Custom file logger provider for structured logging to files
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();

        public FileLoggerProvider()
        {
            _logDirectory = LoggingConfiguration.GetLogDirectory();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _logDirectory, _lock);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    /// <summary>
    /// Custom file logger implementation
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly object _lock;

        public FileLogger(string categoryName, string logDirectory, object lockObject)
        {
            _categoryName = categoryName;
            _logDirectory = logDirectory;
            _lock = lockObject;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            try
            {
                var message = formatter(state, exception);
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel.ToString().ToUpper()}] [{_categoryName}] {message}";

                if (exception != null)
                {
                    logEntry += Environment.NewLine + $"Exception: {exception}";
                }

                var logFileName = GetLogFileName(logLevel);
                var logPath = Path.Combine(_logDirectory, logFileName);

                lock (_lock)
                {
                    File.AppendAllText(logPath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // Ignore logging errors to prevent infinite loops
            }
        }

        private static string GetLogFileName(LogLevel logLevel)
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            return logLevel switch
            {
                LogLevel.Critical => $"critical_{date}.log",
                LogLevel.Error => $"error_{date}.log",
                LogLevel.Warning => $"warning_{date}.log",
                LogLevel.Information => $"info_{date}.log",
                LogLevel.Debug => $"debug_{date}.log",
                _ => $"general_{date}.log"
            };
        }
    }
} 