using System;
using System.Threading.Tasks;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for handling unhandled exceptions globally
    /// </summary>
    public interface IGlobalExceptionHandler
    {
        /// <summary>
        /// Initializes the global exception handler
        /// </summary>
        void Initialize();

        /// <summary>
        /// Handles an unhandled exception
        /// </summary>
        /// <param name="exception">The unhandled exception</param>
        /// <param name="context">Context where the exception occurred</param>
        Task HandleUnhandledExceptionAsync(Exception exception, string context);

        /// <summary>
        /// Gets whether the handler is currently processing a critical error
        /// </summary>
        bool IsHandlingCriticalError { get; }

        /// <summary>
        /// Event fired when an unhandled exception is caught
        /// </summary>
        event EventHandler<UnhandledExceptionEventArgs> UnhandledExceptionCaught;
    }

    /// <summary>
    /// Event arguments for unhandled exceptions
    /// </summary>
    public class UnhandledExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Context { get; }
        public DateTime Timestamp { get; }
        public bool IsTerminating { get; set; }

        public UnhandledExceptionEventArgs(Exception exception, string context, bool isTerminating = false)
        {
            Exception = exception;
            Context = context;
            IsTerminating = isTerminating;
            Timestamp = DateTime.Now;
        }
    }
} 