using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace YAWDA.Models
{
    /// <summary>
    /// User preferences and configuration settings for the water reminder app
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// User's body weight in kilograms for calculating daily water goal
        /// </summary>
        [Range(30, 300, ErrorMessage = "Body weight must be between 30kg and 300kg")]
        public double BodyWeightKilograms { get; set; } = 70.0;

        /// <summary>
        /// Custom daily water goal in milliliters (overrides calculated goal if set)
        /// </summary>
        [Range(500, 5000, ErrorMessage = "Daily goal must be between 500ml and 5000ml")]
        public int? CustomDailyGoalMilliliters { get; set; }

        /// <summary>
        /// Work hours start time for intelligent reminder scheduling
        /// </summary>
        public TimeSpan WorkHoursStart { get; set; } = new TimeSpan(9, 0, 0);

        /// <summary>
        /// Work hours end time for intelligent reminder scheduling
        /// </summary>
        public TimeSpan WorkHoursEnd { get; set; } = new TimeSpan(17, 0, 0);

        /// <summary>
        /// Base reminder interval in minutes
        /// </summary>
        [Range(15, 180, ErrorMessage = "Reminder interval must be between 15 and 180 minutes")]
        public int BaseReminderIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum disruption level (1-4): 1=Toast, 2=Banner, 3=Overlay, 4=Lock
        /// </summary>
        [Range(1, 4, ErrorMessage = "Disruption level must be between 1 and 4")]
        public int MaxDisruptionLevel { get; set; } = 3;

        /// <summary>
        /// Whether to enable smart pause during system idle
        /// </summary>
        public bool EnableSmartPause { get; set; } = true;

        /// <summary>
        /// Whether to reduce reminders after 6 PM (circadian consideration)
        /// </summary>
        public bool EnableCircadianAdjustment { get; set; } = true;

        /// <summary>
        /// Whether to enable weather-based adjustments
        /// </summary>
        public bool EnableWeatherAdjustment { get; set; } = false;

        /// <summary>
        /// Whether to play notification sounds
        /// </summary>
        public bool EnableNotificationSounds { get; set; } = true;

        /// <summary>
        /// Volume level for notification sounds (0.0 to 1.0)
        /// </summary>
        [Range(0.0, 1.0, ErrorMessage = "Volume must be between 0.0 and 1.0")]
        public double NotificationVolume { get; set; } = 0.7;

        /// <summary>
        /// Application theme preference
        /// </summary>
        public AppTheme Theme { get; set; } = AppTheme.System;

        /// <summary>
        /// Whether to start with Windows
        /// </summary>
        public bool StartWithWindows { get; set; } = true;

        /// <summary>
        /// Whether to start minimized to system tray
        /// </summary>
        public bool StartMinimized { get; set; } = true;

        /// <summary>
        /// Whether to minimize to tray when window is closed (true) or exit the app (false)
        /// </summary>
        public bool CloseToTray { get; set; } = true;

        /// <summary>
        /// Number of days to keep historical data
        /// </summary>
        [Range(30, 1095, ErrorMessage = "Data retention must be between 30 and 1095 days")]
        public int DataRetentionDays { get; set; } = 365;

        /// <summary>
        /// User's timezone for accurate time-based calculations
        /// </summary>
        public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

        /// <summary>
        /// Last time settings were modified
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Calculates the daily water goal based on body weight (33ml per kg)
        /// </summary>
        /// <returns>Daily water goal in milliliters</returns>
        [JsonIgnore]
        public int CalculatedDailyGoalMilliliters => (int)(BodyWeightKilograms * 33);

        /// <summary>
        /// Gets the effective daily goal (custom override or calculated)
        /// </summary>
        /// <returns>Daily goal in milliliters</returns>
        [JsonIgnore]
        public int EffectiveDailyGoalMilliliters => CustomDailyGoalMilliliters ?? CalculatedDailyGoalMilliliters;

        /// <summary>
        /// Gets the work hours duration for reminder distribution
        /// </summary>
        /// <returns>Work hours duration</returns>
        [JsonIgnore]
        public TimeSpan WorkHoursDuration
        {
            get
            {
                var duration = WorkHoursEnd - WorkHoursStart;
                return duration.TotalSeconds < 0 ? duration.Add(TimeSpan.FromDays(1)) : duration;
            }
        }

        /// <summary>
        /// Checks if the current time is within work hours
        /// </summary>
        /// <returns>True if within work hours</returns>
        public bool IsWithinWorkHours()
        {
            var now = DateTime.Now.TimeOfDay;
            if (WorkHoursStart <= WorkHoursEnd)
            {
                return now >= WorkHoursStart && now <= WorkHoursEnd;
            }
            else
            {
                // Handle overnight work hours
                return now >= WorkHoursStart || now <= WorkHoursEnd;
            }
        }

        /// <summary>
        /// Validates all settings values
        /// </summary>
        /// <returns>True if all settings are valid</returns>
        public bool IsValid()
        {
            return BodyWeightKilograms >= 30 && BodyWeightKilograms <= 300 &&
                   (CustomDailyGoalMilliliters == null || (CustomDailyGoalMilliliters >= 500 && CustomDailyGoalMilliliters <= 5000)) &&
                   BaseReminderIntervalMinutes >= 15 && BaseReminderIntervalMinutes <= 180 &&
                   MaxDisruptionLevel >= 1 && MaxDisruptionLevel <= 4 &&
                   NotificationVolume >= 0.0 && NotificationVolume <= 1.0 &&
                   DataRetentionDays >= 30 && DataRetentionDays <= 1095;
        }

        /// <summary>
        /// Creates default settings for a new user
        /// </summary>
        /// <returns>Default user settings</returns>
        public static UserSettings CreateDefault()
        {
            return new UserSettings
            {
                LastModified = DateTime.Now
            };
        }

        /// <summary>
        /// Updates the last modified timestamp
        /// </summary>
        public void UpdateLastModified()
        {
            LastModified = DateTime.Now;
        }
    }

    /// <summary>
    /// Application theme options
    /// </summary>
    public enum AppTheme
    {
        Light,
        Dark,
        System
    }
} 