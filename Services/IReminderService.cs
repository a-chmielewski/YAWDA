using System;
using System.Threading.Tasks;
using YAWDA.Models;

namespace YAWDA.Services
{
    /// <summary>
    /// Interface for managing adaptive water reminder scheduling and escalation
    /// </summary>
    public interface IReminderService
    {
        /// <summary>
        /// Starts the reminder service with adaptive scheduling
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the reminder service
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Pauses reminders for a specified duration
        /// </summary>
        /// <param name="duration">Duration to pause</param>
        Task PauseAsync(TimeSpan duration);

        /// <summary>
        /// Resumes reminders if paused
        /// </summary>
        Task ResumeAsync();

        /// <summary>
        /// Records that user drank water (resets escalation)
        /// </summary>
        /// <param name="amount">Amount consumed in ml</param>
        Task RecordWaterIntakeAsync(int amount);

        /// <summary>
        /// Records that user dismissed/ignored a reminder
        /// </summary>
        Task RecordReminderDismissedAsync();

        /// <summary>
        /// Gets the current reminder state
        /// </summary>
        Task<ReminderState> GetCurrentStateAsync();

        /// <summary>
        /// Calculates the next reminder time based on current adherence
        /// </summary>
        Task<DateTime> CalculateNextReminderTimeAsync();

        /// <summary>
        /// Updates reminder frequency based on user settings
        /// </summary>
        /// <param name="settings">User settings with reminder preferences</param>
        Task UpdateSettingsAsync(UserSettings settings);

        /// <summary>
        /// Checks if reminders should be paused based on system state
        /// </summary>
        /// <returns>True if reminders should be paused</returns>
        Task<bool> ShouldPauseForSystemStateAsync();

        /// <summary>
        /// Event fired when it's time to show a reminder
        /// </summary>
        event EventHandler<ReminderEventArgs> ReminderTriggered;

        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets whether reminders are currently paused
        /// </summary>
        bool IsPaused { get; }
    }

    /// <summary>
    /// Event arguments for reminder triggers
    /// </summary>
    public class ReminderEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public int EscalationLevel { get; set; }
        public DateTime ScheduledTime { get; set; }
        public TimeSpan TimeSinceLastIntake { get; set; }
    }
} 