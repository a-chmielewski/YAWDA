using System.Threading.Tasks;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for managing application auto-startup functionality
    /// Supports both MSIX startup tasks and registry-based startup
    /// </summary>
    public interface IStartupService
    {
        /// <summary>
        /// Gets whether auto-startup is currently enabled
        /// </summary>
        /// <returns>True if auto-startup is enabled</returns>
        Task<bool> IsStartupEnabledAsync();

        /// <summary>
        /// Enables or disables auto-startup based on user settings
        /// </summary>
        /// <param name="enabled">Whether to enable auto-startup</param>
        /// <returns>True if operation was successful</returns>
        Task<bool> SetStartupEnabledAsync(bool enabled);

        /// <summary>
        /// Gets whether the application is running in startup mode (minimal UI)
        /// </summary>
        bool IsStartupMode { get; }

        /// <summary>
        /// Initializes startup mode detection from launch arguments
        /// </summary>
        /// <param name="arguments">Launch arguments</param>
        void InitializeStartupMode(string arguments);

        /// <summary>
        /// Gets the startup method being used (MSIX, Registry, or None)
        /// </summary>
        StartupMethod GetStartupMethod();

        /// <summary>
        /// Validates startup configuration and performs maintenance
        /// </summary>
        /// <returns>True if startup is properly configured</returns>
        Task<bool> ValidateStartupConfigurationAsync();
    }

    /// <summary>
    /// Startup method types
    /// </summary>
    public enum StartupMethod
    {
        None,
        MSIX,
        Registry
    }
} 