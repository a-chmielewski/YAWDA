using System;
using System.Threading.Tasks;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for smart water reminder features like idle detection, weather integration, and focus mode
    /// </summary>
    public interface ISmartFeaturesService
    {
        /// <summary>
        /// Gets the time since the user was last active (in seconds)
        /// </summary>
        /// <returns>Seconds since last user input, or -1 if unable to determine</returns>
        int GetSecondsSinceLastUserInput();

        /// <summary>
        /// Checks if the system has been idle for the specified duration
        /// </summary>
        /// <param name="idleThresholdMinutes">Idle threshold in minutes</param>
        /// <returns>True if system is idle beyond threshold</returns>
        bool IsSystemIdle(int idleThresholdMinutes = 10);

        /// <summary>
        /// Gets current weather data for temperature-based adjustments
        /// </summary>
        /// <returns>Weather data or null if unavailable</returns>
        Task<WeatherData?> GetCurrentWeatherAsync();

        /// <summary>
        /// Determines if reminders should be reduced based on circadian rhythm
        /// </summary>
        /// <returns>True if in evening/night mode (reduced reminders)</returns>
        bool IsCircadianNightMode();

        /// <summary>
        /// Checks if Windows Focus Assist is currently enabled
        /// </summary>
        /// <returns>True if Focus Assist is active</returns>
        bool IsFocusAssistEnabled();

        /// <summary>
        /// Detects if user might be in a presentation or meeting
        /// </summary>
        /// <returns>True if presentation mode is likely active</returns>
        bool IsPresentationModeActive();

        /// <summary>
        /// Calculates hydration adjustment factor based on weather conditions
        /// </summary>
        /// <param name="weather">Current weather data</param>
        /// <returns>Multiplier for hydration needs (1.0 = normal, >1.0 = increased need)</returns>
        double CalculateWeatherHydrationFactor(WeatherData weather);

        /// <summary>
        /// Determines if smart pause should be active based on all conditions
        /// </summary>
        /// <returns>True if reminders should be paused</returns>
        Task<bool> ShouldSmartPauseAsync();
    }

    /// <summary>
    /// Weather data for hydration calculations
    /// </summary>
    public class WeatherData
    {
        public double TemperatureCelsius { get; set; }
        public double Humidity { get; set; }
        public string Condition { get; set; } = string.Empty;
        public double HeatIndex { get; set; }
        public DateTime LastUpdated { get; set; }
    }
} 