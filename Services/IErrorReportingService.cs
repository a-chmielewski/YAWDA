using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YAWDA.Utilities;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for error reporting, user notifications, and crash recovery
    /// </summary>
    public interface IErrorReportingService
    {
        /// <summary>
        /// Reports an error with appropriate user notification
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="context">Additional context information</param>
        /// <param name="showToUser">Whether to show user-friendly notification</param>
        Task ReportErrorAsync(Exception exception, string? context = null, bool showToUser = true);

        /// <summary>
        /// Reports a recoverable error with suggested actions
        /// </summary>
        /// <param name="exception">The recoverable exception</param>
        /// <param name="suggestedActions">List of actions user can take</param>
        Task ReportRecoverableErrorAsync(YawdaException exception, List<string>? suggestedActions = null);

        /// <summary>
        /// Reports a critical error that may require app restart
        /// </summary>
        /// <param name="exception">The critical exception</param>
        /// <param name="requiresRestart">Whether app restart is recommended</param>
        Task ReportCriticalErrorAsync(Exception exception, bool requiresRestart = false);

        /// <summary>
        /// Shows a user-friendly error message
        /// </summary>
        /// <param name="title">Error title</param>
        /// <param name="message">User-friendly error message</param>
        /// <param name="severity">Error severity level</param>
        Task ShowUserErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Warning);

        /// <summary>
        /// Attempts to recover from a recoverable error
        /// </summary>
        /// <param name="exception">The recoverable exception</param>
        /// <returns>True if recovery was successful</returns>
        Task<bool> AttemptRecoveryAsync(YawdaException exception);

        /// <summary>
        /// Gets error statistics for debugging
        /// </summary>
        Task<ErrorStatistics> GetErrorStatisticsAsync();

        /// <summary>
        /// Clears old error logs
        /// </summary>
        /// <param name="olderThanDays">Clear errors older than specified days</param>
        Task ClearOldErrorLogsAsync(int olderThanDays = 30);

        /// <summary>
        /// Enables or disables crash reporting
        /// </summary>
        /// <param name="enabled">Whether to enable crash reporting</param>
        void SetCrashReportingEnabled(bool enabled);

        /// <summary>
        /// Gets whether the app is in degraded mode due to errors
        /// </summary>
        bool IsInDegradedMode { get; }

        /// <summary>
        /// Event fired when a critical error occurs
        /// </summary>
        event EventHandler<CriticalErrorEventArgs> CriticalErrorOccurred;
    }

    /// <summary>
    /// Error severity levels
    /// </summary>
    public enum ErrorSeverity
    {
        Information,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Error statistics for monitoring
    /// </summary>
    public class ErrorStatistics
    {
        public int TotalErrorsToday { get; set; }
        public int TotalErrorsThisWeek { get; set; }
        public int CriticalErrorsToday { get; set; }
        public DateTime LastErrorTime { get; set; }
        public Dictionary<string, int> ErrorTypeFrequency { get; set; } = new();
        public bool IsAppStable { get; set; }
    }

    /// <summary>
    /// Event arguments for critical errors
    /// </summary>
    public class CriticalErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Context { get; }
        public bool RequiresRestart { get; }
        public DateTime Timestamp { get; }

        public CriticalErrorEventArgs(Exception exception, string context, bool requiresRestart = false)
        {
            Exception = exception;
            Context = context;
            RequiresRestart = requiresRestart;
            Timestamp = DateTime.Now;
        }
    }
} 