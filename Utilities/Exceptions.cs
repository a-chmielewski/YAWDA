using System;

namespace YAWDA.Utilities
{
    /// <summary>
    /// Base exception for all YAWDA-specific exceptions
    /// </summary>
    public class YawdaException : Exception
    {
        public string? ErrorCode { get; }
        public DateTime Timestamp { get; }
        public bool IsRecoverable { get; }

        public YawdaException(string message, string? errorCode = null, bool isRecoverable = true) 
            : base(message)
        {
            ErrorCode = errorCode;
            Timestamp = DateTime.Now;
            IsRecoverable = isRecoverable;
        }

        public YawdaException(string message, Exception innerException, string? errorCode = null, bool isRecoverable = true) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Timestamp = DateTime.Now;
            IsRecoverable = isRecoverable;
        }
    }

    /// <summary>
    /// Exception for data service related errors
    /// </summary>
    public class DataServiceException : YawdaException
    {
        public DataServiceException(string message, string? errorCode = null) 
            : base($"Data Service Error: {message}", errorCode, true) { }

        public DataServiceException(string message, Exception innerException, string? errorCode = null) 
            : base($"Data Service Error: {message}", innerException, errorCode, true) { }
    }

    /// <summary>
    /// Exception for notification system related errors
    /// </summary>
    public class NotificationException : YawdaException
    {
        public NotificationException(string message, string? errorCode = null) 
            : base($"Notification Error: {message}", errorCode, true) { }

        public NotificationException(string message, Exception innerException, string? errorCode = null) 
            : base($"Notification Error: {message}", innerException, errorCode, true) { }
    }

    /// <summary>
    /// Exception for system integration related errors
    /// </summary>
    public class SystemIntegrationException : YawdaException
    {
        public SystemIntegrationException(string message, string? errorCode = null) 
            : base($"System Integration Error: {message}", errorCode, false) { }

        public SystemIntegrationException(string message, Exception innerException, string? errorCode = null) 
            : base($"System Integration Error: {message}", innerException, errorCode, false) { }
    }

    /// <summary>
    /// Exception for user settings and configuration errors
    /// </summary>
    public class ConfigurationException : YawdaException
    {
        public ConfigurationException(string message, string? errorCode = null) 
            : base($"Configuration Error: {message}", errorCode, true) { }

        public ConfigurationException(string message, Exception innerException, string? errorCode = null) 
            : base($"Configuration Error: {message}", innerException, errorCode, true) { }
    }

    /// <summary>
    /// Exception for startup and initialization errors
    /// </summary>
    public class InitializationException : YawdaException
    {
        public InitializationException(string message, string? errorCode = null) 
            : base($"Initialization Error: {message}", errorCode, false) { }

        public InitializationException(string message, Exception innerException, string? errorCode = null) 
            : base($"Initialization Error: {message}", innerException, errorCode, false) { }
    }

    /// <summary>
    /// Exception for UI and user interaction errors
    /// </summary>
    public class UserInterfaceException : YawdaException
    {
        public UserInterfaceException(string message, string? errorCode = null) 
            : base($"UI Error: {message}", errorCode, true) { }

        public UserInterfaceException(string message, Exception innerException, string? errorCode = null) 
            : base($"UI Error: {message}", innerException, errorCode, true) { }
    }

    /// <summary>
    /// Exception for validation errors
    /// </summary>
    public class ValidationException : YawdaException
    {
        public string? PropertyName { get; }

        public ValidationException(string message, string? propertyName = null, string? errorCode = null) 
            : base($"Validation Error: {message}", errorCode, true)
        {
            PropertyName = propertyName;
        }

        public ValidationException(string message, Exception innerException, string? propertyName = null, string? errorCode = null) 
            : base($"Validation Error: {message}", innerException, errorCode, true)
        {
            PropertyName = propertyName;
        }
    }
} 