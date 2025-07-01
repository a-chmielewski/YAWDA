using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using YAWDA.Utilities;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for handling unhandled exceptions globally
    /// </summary>
    public class GlobalExceptionHandler : IGlobalExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IErrorReportingService _errorReportingService;
        private bool _isHandlingCriticalError = false;
        private bool _isInitialized = false;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IErrorReportingService errorReportingService)
        {
            _logger = logger;
            _errorReportingService = errorReportingService;
        }

        /// <summary>
        /// Gets whether the handler is currently processing a critical error
        /// </summary>
        public bool IsHandlingCriticalError => _isHandlingCriticalError;

        /// <summary>
        /// Event fired when an unhandled exception is caught
        /// </summary>
        public event EventHandler<UnhandledExceptionEventArgs>? UnhandledExceptionCaught;

        /// <summary>
        /// Initializes the global exception handler
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                // Handle unhandled exceptions in the current AppDomain
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // Handle unhandled exceptions in tasks
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // NEW: capture WinUI 3 dispatcher (UI thread) exceptions
                // These exceptions previously terminated the process before reaching the other handlers.
                // By attaching here we can route them through the same reporting pipeline and keep the app alive.
                if (Microsoft.UI.Xaml.Application.Current is not null)
                {
                    Microsoft.UI.Xaml.Application.Current.UnhandledException += OnAppUnhandledException;
                }

                // Note: WinUI dispatcher exception handling is now explicitly covered above
                _logger.LogInformation("Global exception handlers attached for AppDomain, TaskScheduler and UI thread");

                _isInitialized = true;
                _logger.LogInformation("Global exception handler initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize global exception handler");
            }
        }

        /// <summary>
        /// Handles an unhandled exception
        /// </summary>
        public async Task HandleUnhandledExceptionAsync(Exception exception, string context)
        {
            if (_isHandlingCriticalError)
            {
                // Prevent recursive error handling
                _logger.LogCritical("Recursive critical error detected, terminating application");
                Environment.FailFast("Recursive critical error", exception);
                return;
            }

            try
            {
                _isHandlingCriticalError = true;

                _logger.LogCritical(exception, "Unhandled exception caught in context: {Context}", context);

                // Fire event for any listeners
                var eventArgs = new UnhandledExceptionEventArgs(exception, context, IsTerminatingException(exception));
                UnhandledExceptionCaught?.Invoke(this, eventArgs);

                // Determine if this is a critical error that requires restart
                var requiresRestart = ShouldRestartForException(exception);

                // Report to error reporting service
                await _errorReportingService.ReportCriticalErrorAsync(exception, requiresRestart);

                // If restart is required, give user option
                if (requiresRestart)
                {
                    await _errorReportingService.ShowUserErrorAsync(
                        "Critical Error - Restart Required",
                        "A critical error has occurred that requires the application to restart. Would you like to restart now?",
                        ErrorSeverity.Critical
                    );

                    // Give some time for user to see the message
                    await Task.Delay(5000);

                    // Restart application
                    RestartApplication();
                }
            }
            catch (Exception handlerEx)
            {
                // Last resort logging
                _logger.LogCritical(handlerEx, "Exception handler itself failed");
                
                // Force termination to prevent infinite loops
                Environment.FailFast("Exception handler failed", handlerEx);
            }
            finally
            {
                _isHandlingCriticalError = false;
            }
        }

        #region Private Event Handlers

        private async void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Mark as handled so the application does not crash automatically
            e.Handled = true;
            await HandleUnhandledExceptionAsync(e.Exception, "Application.UnhandledException");
        }

        private async void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                await HandleUnhandledExceptionAsync(exception, "AppDomain.UnhandledException");
            }
        }

        private async void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved(); // Mark as observed to prevent app termination
            
            var exception = e.Exception?.GetBaseException() ?? e.Exception;
            if (exception != null)
            {
                await HandleUnhandledExceptionAsync(exception, "TaskScheduler.UnobservedTaskException");
            }
        }

        #endregion

        #region Private Helper Methods

        private static bool IsTerminatingException(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => true,
                StackOverflowException => true,
                AccessViolationException => true,
                InvalidProgramException => true,
                BadImageFormatException => true,
                _ => false
            };
        }

        private static bool ShouldRestartForException(Exception exception)
        {
            return exception switch
            {
                // Always restart for these critical exceptions
                OutOfMemoryException => true,
                StackOverflowException => true,
                AccessViolationException => true,
                InvalidProgramException => true,
                BadImageFormatException => true,
                
                // Restart for initialization errors
                InitializationException => true,
                
                // Restart for non-recoverable YAWDA exceptions
                YawdaException yex when !yex.IsRecoverable => true,
                
                // Don't restart for recoverable errors
                DataServiceException => false,
                ConfigurationException => false,
                NotificationException => false,
                ValidationException => false,
                UserInterfaceException => false,
                
                // Default: restart for unknown exceptions
                _ => true
            };
        }

        private static void RestartApplication()
        {
            try
            {
                // Get current process information
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var executablePath = currentProcess.MainModule?.FileName;

                if (!string.IsNullOrEmpty(executablePath))
                {
                    // Start new instance
                    System.Diagnostics.Process.Start(executablePath);
                }

                // Exit current instance
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                // If restart fails, just exit
                System.Diagnostics.Debug.WriteLine($"Failed to restart application: {ex.Message}");
                Environment.Exit(1);
            }
        }

        #endregion
    }
} 