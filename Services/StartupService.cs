using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for managing application auto-startup functionality
    /// Supports both MSIX startup tasks and registry-based startup
    /// </summary>
    public class StartupService : IStartupService
    {
        private readonly ILogger<StartupService> _logger;
        private readonly IDataService _dataService;
        private bool _isStartupMode;
        private StartupMethod _startupMethod;

        // Registry key path for non-MSIX startup
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryValueName = "YAWDA";
        private const string StartupTaskId = "YAWDAStartupTask";

        public StartupService(ILogger<StartupService> logger, IDataService dataService)
        {
            _logger = logger;
            _dataService = dataService;
            _startupMethod = DetermineStartupMethod();
        }

        /// <summary>
        /// Gets whether the application is running in startup mode (minimal UI)
        /// </summary>
        public bool IsStartupMode => _isStartupMode;

        /// <summary>
        /// Initializes startup mode detection from launch arguments
        /// </summary>
        /// <param name="arguments">Launch arguments</param>
        public void InitializeStartupMode(string arguments)
        {
            _isStartupMode = !string.IsNullOrEmpty(arguments) && 
                           (arguments.Contains("--startup") || arguments.Contains("/startup"));
            
            _logger.LogInformation("Startup mode initialized: {IsStartupMode}, Arguments: {Arguments}", 
                _isStartupMode, arguments);
        }

        /// <summary>
        /// Gets whether auto-startup is currently enabled
        /// </summary>
        /// <returns>True if auto-startup is enabled</returns>
        public async Task<bool> IsStartupEnabledAsync()
        {
            try
            {
                switch (_startupMethod)
                {
                    case StartupMethod.MSIX:
                        return await IsStartupTaskEnabledAsync();
                    
                    case StartupMethod.Registry:
                        return IsRegistryStartupEnabled();
                    
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking startup status");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables auto-startup based on user settings
        /// </summary>
        /// <param name="enabled">Whether to enable auto-startup</param>
        /// <returns>True if operation was successful</returns>
        public async Task<bool> SetStartupEnabledAsync(bool enabled)
        {
            try
            {
                bool success;
                
                switch (_startupMethod)
                {
                    case StartupMethod.MSIX:
                        success = await SetStartupTaskEnabledAsync(enabled);
                        break;
                    
                    case StartupMethod.Registry:
                        success = SetRegistryStartupEnabled(enabled);
                        break;
                    
                    default:
                        _logger.LogWarning("No startup method available");
                        return false;
                }

                if (success)
                {
                    _logger.LogInformation("Startup {Action} successfully using {Method}", 
                        enabled ? "enabled" : "disabled", _startupMethod);
                }
                else
                {
                    _logger.LogWarning("Failed to {Action} startup using {Method}", 
                        enabled ? "enable" : "disable", _startupMethod);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting startup state to {Enabled}", enabled);
                return false;
            }
        }

        /// <summary>
        /// Gets the startup method being used (MSIX, Registry, or None)
        /// </summary>
        public StartupMethod GetStartupMethod() => _startupMethod;

        /// <summary>
        /// Validates startup configuration and performs maintenance
        /// </summary>
        /// <returns>True if startup is properly configured</returns>
        public async Task<bool> ValidateStartupConfigurationAsync()
        {
            try
            {
                // Get user settings to check desired state
                var settings = await _dataService.LoadSettingsAsync();
                var desiredState = settings.StartWithWindows;
                var currentState = await IsStartupEnabledAsync();

                // If states don't match, try to fix it
                if (desiredState != currentState)
                {
                    _logger.LogInformation("Startup state mismatch. Desired: {Desired}, Current: {Current}. Attempting to fix...", 
                        desiredState, currentState);
                    
                    return await SetStartupEnabledAsync(desiredState);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating startup configuration");
                return false;
            }
        }

        /// <summary>
        /// Determines which startup method to use based on app packaging
        /// </summary>
        private StartupMethod DetermineStartupMethod()
        {
            try
            {
                // Check if running as MSIX package
                var package = Package.Current;
                if (package != null && !string.IsNullOrEmpty(package.Id.Name))
                {
                    _logger.LogInformation("MSIX package detected, using StartupTask method");
                    return StartupMethod.MSIX;
                }
            }
            catch (InvalidOperationException)
            {
                // Not running as MSIX package
                _logger.LogInformation("Not running as MSIX package, using Registry method");
                return StartupMethod.Registry;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error determining package state, falling back to Registry method");
                return StartupMethod.Registry;
            }

            return StartupMethod.Registry;
        }

        /// <summary>
        /// Checks if MSIX startup task is enabled
        /// </summary>
        private async Task<bool> IsStartupTaskEnabledAsync()
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                // Check if the state is enabled (using ToString to avoid namespace issues)
                return startupTask.State.ToString() == "Enabled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking MSIX startup task state");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables MSIX startup task
        /// </summary>
        private async Task<bool> SetStartupTaskEnabledAsync(bool enabled)
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                
                if (enabled)
                {
                    var requestResult = await startupTask.RequestEnableAsync();
                    var resultString = requestResult.ToString();
                    
                    _logger.LogInformation("MSIX startup task enable request result: {Result}", resultString);
                    
                    // Check if enabled successfully
                    if (resultString == "Enabled")
                    {
                        _logger.LogInformation("MSIX startup task enabled successfully");
                        return true;
                    }
                    else if (resultString == "DisabledByUser")
                    {
                        _logger.LogWarning("MSIX startup task disabled by user");
                        return false;
                    }
                    else if (resultString == "DisabledByPolicy")
                    {
                        _logger.LogWarning("MSIX startup task disabled by policy");
                        return false;
                    }
                    else
                    {
                        _logger.LogWarning("MSIX startup task enable request returned unexpected result: {Result}", resultString);
                        return false;
                    }
                }
                else
                {
                    startupTask.Disable();
                    _logger.LogInformation("MSIX startup task disabled successfully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting MSIX startup task state");
                return false;
            }
        }

        /// <summary>
        /// Checks if registry startup is enabled
        /// </summary>
        private bool IsRegistryStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                var value = key?.GetValue(RegistryValueName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking registry startup state");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables registry startup
        /// </summary>
        private bool SetRegistryStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                
                if (key == null)
                {
                    _logger.LogError("Cannot open registry key for startup configuration");
                    return false;
                }

                if (enabled)
                {
                    var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(executablePath))
                    {
                        _logger.LogError("Cannot determine executable path for startup");
                        return false;
                    }

                    // Add startup argument to distinguish startup launches
                    var startupCommand = $"\"{executablePath}\" --startup";
                    key.SetValue(RegistryValueName, startupCommand);
                    _logger.LogInformation("Registry startup enabled with command: {Command}", startupCommand);
                }
                else
                {
                    key.DeleteValue(RegistryValueName, false);
                    _logger.LogInformation("Registry startup disabled");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting registry startup state");
                return false;
            }
        }
    }
} 