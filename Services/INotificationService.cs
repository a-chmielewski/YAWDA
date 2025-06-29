using System;
using System.Threading.Tasks;

namespace YAWDA.Services
{
    /// <summary>
    /// Interface for managing notifications and system tray interactions
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a water reminder toast notification with action buttons
        /// </summary>
        /// <param name="message">The reminder message to display</param>
        /// <param name="escalationLevel">The current escalation level (1-4)</param>
        Task ShowWaterReminderAsync(string message, int escalationLevel = 1);

        /// <summary>
        /// Shows a confirmation toast when user logs water intake
        /// </summary>
        /// <param name="amount">Amount of water consumed in ml</param>
        Task ShowIntakeConfirmationAsync(int amount);

        /// <summary>
        /// Updates the system tray icon tooltip with current daily progress
        /// </summary>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        void UpdateTrayTooltip(int currentIntake, int dailyGoal);

        /// <summary>
        /// Shows an escalated reminder overlay (Level 2-4)
        /// </summary>
        /// <param name="level">The escalation level</param>
        /// <param name="message">The reminder message</param>
        Task ShowEscalatedReminderAsync(int level, string message);

        /// <summary>
        /// Initializes the system tray icon and context menu
        /// </summary>
        Task InitializeTrayIconAsync();

        /// <summary>
        /// Event fired when user interacts with notification actions
        /// </summary>
        event EventHandler<NotificationActionEventArgs> NotificationActionReceived;
    }

    /// <summary>
    /// Event arguments for notification actions
    /// </summary>
    public class NotificationActionEventArgs : EventArgs
    {
        public string Action { get; set; } = string.Empty;
        public int Amount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
} 