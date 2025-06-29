using System;
using System.Threading.Tasks;

namespace YAWDA.Services
{
    /// <summary>
    /// Interface for managing progressive disruption overlays
    /// </summary>
    public interface IOverlayService
    {
        /// <summary>
        /// Shows a banner overlay (Level 2 disruption)
        /// </summary>
        /// <param name="message">Reminder message to display</param>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        Task ShowBannerOverlayAsync(string message, int currentIntake = 0, int dailyGoal = 2310);

        /// <summary>
        /// Shows a full-screen overlay (Level 3 disruption)
        /// </summary>
        /// <param name="message">Reminder message to display</param>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        Task ShowFullScreenOverlayAsync(string message, int currentIntake = 0, int dailyGoal = 2310);

        /// <summary>
        /// Hides any currently visible overlays
        /// </summary>
        Task HideAllOverlaysAsync();

        /// <summary>
        /// Checks if any overlay is currently visible
        /// </summary>
        bool IsOverlayVisible { get; }

        /// <summary>
        /// Event fired when user takes an action from an overlay
        /// </summary>
        event EventHandler<OverlayActionEventArgs> OverlayActionReceived;
    }

    /// <summary>
    /// Event arguments for overlay actions
    /// </summary>
    public class OverlayActionEventArgs : EventArgs
    {
        public OverlayActionType ActionType { get; set; }
        public int Amount { get; set; } = 0;
        public TimeSpan SnoozeDuration { get; set; } = TimeSpan.Zero;
        public int DisruptionLevel { get; set; } = 1;
    }

    /// <summary>
    /// Types of actions that can be performed from overlays
    /// </summary>
    public enum OverlayActionType
    {
        Drink,
        Snooze,
        Dismiss,
        Timeout
    }
} 