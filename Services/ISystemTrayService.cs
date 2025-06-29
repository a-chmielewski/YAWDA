using System;
using System.Threading.Tasks;

namespace YAWDA.Services
{
    /// <summary>
    /// Interface for managing system tray icon and interactions
    /// </summary>
    public interface ISystemTrayService
    {
        /// <summary>
        /// Initializes the system tray icon and context menu
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Updates the tray icon tooltip with current daily progress
        /// </summary>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        /// <param name="nextReminderTime">Time until next reminder</param>
        void UpdateTooltip(int currentIntake, int dailyGoal, string nextReminderTime);

        /// <summary>
        /// Shows a balloon notification from the tray icon
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="timeout">Display timeout in milliseconds</param>
        Task ShowTrayNotificationAsync(string title, string message, int timeout = 3000);

        /// <summary>
        /// Shows or hides the main application window
        /// </summary>
        /// <param name="show">True to show, false to hide</param>
        void SetMainWindowVisibility(bool show);

        /// <summary>
        /// Gets whether the main window is currently visible
        /// </summary>
        bool IsMainWindowVisible { get; }

        /// <summary>
        /// Event fired when user requests to show the main window
        /// </summary>
        event EventHandler ShowMainWindowRequested;

        /// <summary>
        /// Event fired when user requests to log water intake manually
        /// </summary>
        event EventHandler<ManualLogEventArgs> ManualLogRequested;

        /// <summary>
        /// Event fired when user requests to show settings
        /// </summary>
        event EventHandler ShowSettingsRequested;

        /// <summary>
        /// Event fired when user requests to show statistics
        /// </summary>
        event EventHandler ShowStatsRequested;

        /// <summary>
        /// Event fired when user requests to pause reminders
        /// </summary>
        event EventHandler<PauseReminderEventArgs> PauseReminderRequested;

        /// <summary>
        /// Event fired when user requests to exit the application
        /// </summary>
        event EventHandler ExitRequested;
    }

    /// <summary>
    /// Event arguments for manual log requests
    /// </summary>
    public class ManualLogEventArgs : EventArgs
    {
        public int Amount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event arguments for pause reminder requests
    /// </summary>
    public class PauseReminderEventArgs : EventArgs
    {
        public TimeSpan Duration { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
} 