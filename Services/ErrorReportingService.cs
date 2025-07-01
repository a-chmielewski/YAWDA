using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using YAWDA.Utilities;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for error reporting, user notifications, and crash recovery
    /// </summary>
    public class ErrorReportingService : IErrorReportingService
    {
        private readonly ILogger<ErrorReportingService> _logger;
        private readonly IDataService _dataService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly string _errorLogPath;
        private readonly List<ErrorLogEntry> _recentErrors = new();
        private bool _crashReportingEnabled = true;
        private bool _isInDegradedMode = false;
        private readonly object _lockObject = new();

        public ErrorReportingService(ILogger<ErrorReportingService> logger, IDataService dataService, ISystemTrayService systemTrayService)
        {
            _logger = logger;
            _dataService = dataService;
            _systemTrayService = systemTrayService;
            
            // Setup error log directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(appDataPath, "YAWDA", "Logs");
            Directory.CreateDirectory(logDirectory);
            _errorLogPath = Path.Combine(logDirectory, "errors.json");
            
            // Load recent errors on startup
            _ = Task.Run(LoadRecentErrorsAsync);
        }

        /// <summary>
        /// Gets whether the app is in degraded mode due to errors
        /// </summary>
        public bool IsInDegradedMode => _isInDegradedMode;

        /// <summary>
        /// Event fired when a critical error occurs
        /// </summary>
        public event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        /// <summary>
        /// Reports an error with appropriate user notification
        /// </summary>
        public async Task ReportErrorAsync(Exception exception, string? context = null, bool showToUser = true)
        {
            try
            {
                var errorEntry = new ErrorLogEntry
                {
                    Exception = exception,
                    Context = context ?? "Unknown",
                    Timestamp = DateTime.Now,
                    Severity = DetermineErrorSeverity(exception)
                };

                // Log the error
                await LogErrorAsync(errorEntry);

                // Handle based on exception type
                if (exception is YawdaException yawdaEx)
                {
                    if (yawdaEx.IsRecoverable)
                    {
                        await ReportRecoverableErrorAsync(yawdaEx);
                    }
                    else
                    {
                        await ReportCriticalErrorAsync(exception, false);
                    }
                }
                else
                {
                    // Unknown exception - treat as potentially critical
                    if (showToUser && errorEntry.Severity >= ErrorSeverity.Error)
                    {
                        await ShowUserErrorAsync(
                            "Unexpected Error",
                            GetUserFriendlyMessage(exception),
                            errorEntry.Severity
                        );
                    }
                }

                // Check if app should enter degraded mode
                await CheckDegradedModeAsync();
            }
            catch (Exception ex)
            {
                // Fallback logging if error reporting itself fails
                _logger.LogCritical(ex, "Error reporting service failed while reporting error: {OriginalError}", exception.Message);
            }
        }

        /// <summary>
        /// Reports a recoverable error with suggested actions
        /// </summary>
        public async Task ReportRecoverableErrorAsync(YawdaException exception, List<string>? suggestedActions = null)
        {
            try
            {
                _logger.LogWarning(exception, "Recoverable error occurred: {ErrorCode}", exception.ErrorCode);

                // Attempt automatic recovery first
                var recovered = await AttemptRecoveryAsync(exception);
                
                if (!recovered)
                {
                    // Show user notification with suggested actions
                    var actions = suggestedActions ?? GetDefaultRecoveryActions(exception);
                    var message = $"{GetUserFriendlyMessage(exception)}\n\nSuggested actions:\n{string.Join("\n", actions.Select(a => $"• {a}"))}";
                    
                    await ShowUserErrorAsync(
                        "Recovery Required",
                        message,
                        ErrorSeverity.Warning
                    );
                }
                else
                {
                    _logger.LogInformation("Automatic recovery successful for error: {ErrorCode}", exception.ErrorCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report recoverable error");
            }
        }

        /// <summary>
        /// Reports a critical error that may require app restart
        /// </summary>
        public async Task ReportCriticalErrorAsync(Exception exception, bool requiresRestart = false)
        {
            try
            {
                _logger.LogCritical(exception, "Critical error occurred. Requires restart: {RequiresRestart}", requiresRestart);

                // Enable degraded mode
                _isInDegradedMode = true;

                // Fire critical error event
                CriticalErrorOccurred?.Invoke(this, new CriticalErrorEventArgs(exception, "Critical system error", requiresRestart));

                // Show critical error to user
                var message = requiresRestart 
                    ? $"{GetUserFriendlyMessage(exception)}\n\nThe application will need to restart to recover."
                    : $"{GetUserFriendlyMessage(exception)}\n\nSome features may be temporarily unavailable.";

                await ShowUserErrorAsync(
                    "Critical Error",
                    message,
                    ErrorSeverity.Critical
                );

                // Log to crash report if enabled
                if (_crashReportingEnabled)
                {
                    await WriteCrashReportAsync(exception, requiresRestart);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to report critical error");
            }
        }

        /// <summary>
        /// Shows a user-friendly error message
        /// </summary>
        public async Task ShowUserErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Warning)
        {
            try
            {
                // Show system tray notification
                var timeout = severity switch
                {
                    ErrorSeverity.Information => 3000,
                    ErrorSeverity.Warning => 5000,
                    ErrorSeverity.Error => 8000,
                    ErrorSeverity.Critical => 10000,
                    _ => 5000
                };

                await _systemTrayService.ShowTrayNotificationAsync(title, message, timeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show user error notification");
            }
        }

        /// <summary>
        /// Attempts to recover from a recoverable error
        /// </summary>
        public async Task<bool> AttemptRecoveryAsync(YawdaException exception)
        {
            try
            {
                return exception switch
                {
                    DataServiceException => await RecoverDataServiceAsync(),
                    ConfigurationException => await RecoverConfigurationAsync(),
                    NotificationException => await RecoverNotificationServiceAsync(),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery attempt failed for {ExceptionType}", exception.GetType().Name);
                return false;
            }
        }

        /// <summary>
        /// Gets error statistics for debugging
        /// </summary>
        public async Task<ErrorStatistics> GetErrorStatisticsAsync()
        {
            try
            {
                await LoadRecentErrorsAsync();
                
                var today = DateTime.Today;
                var thisWeek = today.AddDays(-7);

                lock (_lockObject)
                {
                    var todaysErrors = _recentErrors.Where(e => e.Timestamp.Date == today).ToList();
                    var thisWeeksErrors = _recentErrors.Where(e => e.Timestamp >= thisWeek).ToList();

                    return new ErrorStatistics
                    {
                        TotalErrorsToday = todaysErrors.Count,
                        TotalErrorsThisWeek = thisWeeksErrors.Count,
                        CriticalErrorsToday = todaysErrors.Count(e => e.Severity == ErrorSeverity.Critical),
                        LastErrorTime = _recentErrors.LastOrDefault()?.Timestamp ?? DateTime.MinValue,
                        ErrorTypeFrequency = _recentErrors
                            .GroupBy(e => e.Exception?.GetType().Name ?? "Unknown")
                            .ToDictionary(g => g.Key, g => g.Count()),
                        IsAppStable = todaysErrors.Count < 5 && !_isInDegradedMode
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get error statistics");
                return new ErrorStatistics { IsAppStable = false };
            }
        }

        /// <summary>
        /// Clears old error logs
        /// </summary>
        public async Task ClearOldErrorLogsAsync(int olderThanDays = 30)
        {
            try
            {
                await LoadRecentErrorsAsync();
                
                var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
                
                lock (_lockObject)
                {
                    var originalCount = _recentErrors.Count;
                    _recentErrors.RemoveAll(e => e.Timestamp < cutoffDate);
                    var removedCount = originalCount - _recentErrors.Count;
                    
                    if (removedCount > 0)
                    {
                        _logger.LogInformation("Cleared {RemovedCount} old error log entries", removedCount);
                    }
                }

                await SaveErrorLogsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear old error logs");
            }
        }

        /// <summary>
        /// Enables or disables crash reporting
        /// </summary>
        public void SetCrashReportingEnabled(bool enabled)
        {
            _crashReportingEnabled = enabled;
            _logger.LogInformation("Crash reporting {Status}", enabled ? "enabled" : "disabled");
        }

        #region Private Methods

        private async Task LogErrorAsync(ErrorLogEntry errorEntry)
        {
            lock (_lockObject)
            {
                _recentErrors.Add(errorEntry);
                
                // Keep only last 1000 errors in memory
                if (_recentErrors.Count > 1000)
                {
                    _recentErrors.RemoveRange(0, _recentErrors.Count - 1000);
                }
            }

            await SaveErrorLogsAsync();

            // Log to system logger
            var logLevel = errorEntry.Severity switch
            {
                ErrorSeverity.Information => LogLevel.Information,
                ErrorSeverity.Warning => LogLevel.Warning,
                ErrorSeverity.Error => LogLevel.Error,
                ErrorSeverity.Critical => LogLevel.Critical,
                _ => LogLevel.Warning
            };

            _logger.Log(logLevel, errorEntry.Exception, 
                "Error reported: {Context} | Code: {ErrorCode}", 
                errorEntry.Context, 
                errorEntry.Exception is YawdaException yex ? yex.ErrorCode : "N/A");
        }

        private async Task LoadRecentErrorsAsync()
        {
            try
            {
                if (!File.Exists(_errorLogPath))
                    return;

                var json = await File.ReadAllTextAsync(_errorLogPath);
                
                // Try to load with new serializable format first
                try
                {
                    var serializableEntries = JsonSerializer.Deserialize<List<object>>(json);
                    if (serializableEntries != null)
                    {
                        // Parse the JSON object format into ErrorLogEntry objects
                        // For now, we'll skip loading old entries since they contain non-serializable exception data
                        // and focus on having a working error logging system going forward
                        _logger.LogInformation("Existing error log file found but will be replaced with new format");
                    }
                }
                catch
                {
                    // Failed to load with new format, might be old format
                    // We'll skip loading old entries to avoid serialization issues
                    _logger.LogInformation("Existing error log file incompatible with current format - starting fresh");
                }
                
                lock (_lockObject)
                {
                    _recentErrors.Clear();
                    // Start with empty list for new error logging format
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load error logs from file - starting with fresh error log");
                lock (_lockObject)
                {
                    _recentErrors.Clear();
                }
            }
        }

        private async Task SaveErrorLogsAsync()
        {
            try
            {
                List<ErrorLogEntry> toSave;
                lock (_lockObject)
                {
                    toSave = new List<ErrorLogEntry>(_recentErrors);
                }

                // Convert to serializable format
                var serializableEntries = toSave.Select(entry => new
                {
                    Exception = entry.Exception != null ? new
                    {
                        Type = entry.Exception.GetType().FullName,
                        Message = entry.Exception.Message,
                        StackTrace = entry.Exception.StackTrace,
                        InnerException = entry.Exception.InnerException?.ToString()
                    } : null,
                    Context = entry.Context,
                    Timestamp = entry.Timestamp,
                    Severity = entry.Severity.ToString()
                }).ToList();

                var json = JsonSerializer.Serialize(serializableEntries, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                
                await File.WriteAllTextAsync(_errorLogPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save error logs to file");
            }
        }

        private async Task WriteCrashReportAsync(Exception exception, bool requiresRestart)
        {
            try
            {
                var crashReportPath = Path.Combine(Path.GetDirectoryName(_errorLogPath)!, $"crash_report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                
                var crashReport = new
                {
                    Timestamp = DateTime.Now,
                    Exception = new
                    {
                        Type = exception.GetType().FullName,
                        Message = exception.Message,
                        StackTrace = exception.StackTrace,
                        InnerException = exception.InnerException?.ToString()
                    },
                    RequiresRestart = requiresRestart,
                    SystemInfo = new
                    {
                        OS = Environment.OSVersion.ToString(),
                        CLR = Environment.Version.ToString(),
                        ProcessorCount = Environment.ProcessorCount,
                        WorkingSet = Environment.WorkingSet,
                        Is64BitProcess = Environment.Is64BitProcess
                    },
                    AppInfo = new
                    {
                        Version = "1.0.0", // TODO: Get from assembly
                        StartTime = Process.GetCurrentProcess().StartTime,
                        IsInDegradedMode = _isInDegradedMode
                    }
                };

                var json = JsonSerializer.Serialize(crashReport, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(crashReportPath, json);
                
                _logger.LogInformation("Crash report written to {CrashReportPath}", crashReportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write crash report");
            }
        }

        private static ErrorSeverity DetermineErrorSeverity(Exception exception)
        {
            return exception switch
            {
                YawdaException yex when !yex.IsRecoverable => ErrorSeverity.Critical,
                InitializationException => ErrorSeverity.Critical,
                SystemIntegrationException => ErrorSeverity.Error,
                DataServiceException => ErrorSeverity.Warning,
                ConfigurationException => ErrorSeverity.Warning,
                ValidationException => ErrorSeverity.Information,
                _ => ErrorSeverity.Error
            };
        }

        private static string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                DataServiceException => "There was a problem saving your water intake data. Your data is safe, but some features may be temporarily unavailable.",
                NotificationException => "Water reminders may not be working properly. Please check your notification settings.",
                SystemIntegrationException => "There was a problem connecting to system features. Some advanced features may be unavailable.",
                ConfigurationException => "There was a problem with your settings. Please check your preferences.",
                ValidationException vex => $"Invalid input: {vex.Message.Replace("Validation Error: ", "")}",
                InitializationException => "The application failed to start properly. Please restart the application.",
                UserInterfaceException => "There was a display problem. Please try refreshing the window.",
                _ => "An unexpected error occurred. The application will continue to work, but some features may be temporarily unavailable."
            };
        }

        private static List<string> GetDefaultRecoveryActions(YawdaException exception)
        {
            return exception switch
            {
                DataServiceException => new List<string>
                {
                    "Restart the application",
                    "Check if you have enough disk space",
                    "Try logging water intake manually"
                },
                NotificationException => new List<string>
                {
                    "Check Windows notification settings",
                    "Restart the application",
                    "Use system tray for manual logging"
                },
                ConfigurationException => new List<string>
                {
                    "Reset settings to defaults",
                    "Check your input values",
                    "Restart the application"
                },
                _ => new List<string>
                {
                    "Restart the application",
                    "Check system resources",
                    "Contact support if problem persists"
                }
            };
        }

        private async Task<bool> RecoverDataServiceAsync()
        {
            try
            {
                // Attempt to reinitialize data service
                await _dataService.InitializeAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> RecoverConfigurationAsync()
        {
            try
            {
                // Load default settings
                var defaultSettings = Models.UserSettings.CreateDefault();
                await _dataService.SaveSettingsAsync(defaultSettings);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> RecoverNotificationServiceAsync()
        {
            try
            {
                // This would require notification service to have a reinitialize method
                // For now, just return false to indicate manual intervention needed
                await Task.Delay(100); // Placeholder
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckDegradedModeAsync()
        {
            try
            {
                var stats = await GetErrorStatisticsAsync();
                
                // Enter degraded mode if too many errors today
                if (stats.TotalErrorsToday > 10 || stats.CriticalErrorsToday > 2)
                {
                    if (!_isInDegradedMode)
                    {
                        _isInDegradedMode = true;
                        _logger.LogWarning("Application entering degraded mode due to error frequency");
                        
                        await ShowUserErrorAsync(
                            "Degraded Mode",
                            "The application is running in degraded mode due to recent errors. Some features may be limited.",
                            ErrorSeverity.Warning
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check degraded mode status");
            }
        }

        #endregion
    }

    /// <summary>
    /// Internal error log entry
    /// </summary>
    internal class ErrorLogEntry
    {
        public Exception? Exception { get; set; }
        public string Context { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public ErrorSeverity Severity { get; set; }
    }
} 