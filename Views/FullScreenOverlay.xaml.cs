using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;

namespace YAWDA.Views
{
    /// <summary>
    /// Level 3 disruption: Full-screen overlay for water reminders
    /// </summary>
    public sealed partial class FullScreenOverlay : UserControl
    {
        private readonly DispatcherTimer _autoHideTimer;
        private bool _isVisible = false;
        private readonly string[] _hydrationTips = new[]
        {
            "Regular water intake improves focus, energy levels, and overall productivity.",
            "Even mild dehydration can affect your concentration and mood.",
            "Drinking water helps maintain healthy skin and supports immune function.",
            "Proper hydration aids digestion and helps regulate body temperature.",
            "Your brain is 75% water - stay hydrated to keep thinking clearly!",
            "Water helps transport nutrients to cells and remove waste products.",
            "Staying hydrated can help prevent headaches and fatigue.",
            "Good hydration supports healthy blood pressure and heart function."
        };

        public event EventHandler<FullScreenActionEventArgs>? ActionRequested;

        public FullScreenOverlay()
        {
            this.InitializeComponent();
            
            // Set up auto-hide timer (2 minutes)
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(2)
            };
            _autoHideTimer.Tick += OnAutoHideTimerTick;

            // Set up render transforms for animations
            WaterDropIcon.RenderTransform = new ScaleTransform();
            this.RenderTransform = new ScaleTransform();
        }

        /// <summary>
        /// Shows the full-screen overlay with the specified reminder message and progress
        /// </summary>
        /// <param name="message">Reminder message to display</param>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        public Task ShowOverlayAsync(string message, int currentIntake = 0, int dailyGoal = 2310)
        {
            if (_isVisible) return Task.CompletedTask;

            // Update content
            ReminderMessageText.Text = message;
            UpdateProgress(currentIntake, dailyGoal);
            
            // Show random hydration tip
            var random = new Random();
            HydrationTipText.Text = _hydrationTips[random.Next(_hydrationTips.Length)];

            this.Visibility = Visibility.Visible;
            _isVisible = true;

            // Start entrance animations
            FadeInAnimation.Begin();
            
            // Start pulsing animation for water drop
            PulseAnimation.Begin();
            
            // Start auto-hide timer
            _autoHideTimer.Start();

            // Play attention-grabbing sound
            try
            {
                var player = new Windows.Media.Playback.MediaPlayer();
                player.Source = Windows.Media.Core.MediaSource.CreateFromUri(
                    new Uri("ms-winsoundevent:Notification.Mail"));
                player.Play();
            }
            catch
            {
                // Ignore audio errors
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Hides the full-screen overlay with fade-out animation
        /// </summary>
        public async Task HideOverlayAsync()
        {
            if (!_isVisible) return;

            _autoHideTimer.Stop();
            _isVisible = false;

            // Stop pulsing animation
            PulseAnimation.Stop();

            // Start fade-out animation
            FadeOutAnimation.Begin();

            // Wait for animation to complete
            await Task.Delay(300);
            
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the progress display
        /// </summary>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        public void UpdateProgress(int currentIntake, int dailyGoal)
        {
            var percentage = dailyGoal > 0 ? (double)currentIntake / dailyGoal * 100 : 0;
            ProgressBar.Value = Math.Min(100, percentage);
            ProgressText.Text = $"{currentIntake}ml / {dailyGoal}ml ({percentage:F0}% of daily goal)";
        }

        private async void OnDrinkAmountClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int amount))
            {
                ActionRequested?.Invoke(this, new FullScreenActionEventArgs 
                { 
                    Action = FullScreenAction.Drink,
                    Amount = amount
                });
                
                await HideOverlayAsync();
            }
        }

        private async void OnSnoozeClicked(object sender, RoutedEventArgs e)
        {
            ActionRequested?.Invoke(this, new FullScreenActionEventArgs 
            { 
                Action = FullScreenAction.Snooze,
                SnoozeDuration = TimeSpan.FromMinutes(15)
            });
            
            await HideOverlayAsync();
        }

        private async void OnDismissClicked(object sender, RoutedEventArgs e)
        {
            ActionRequested?.Invoke(this, new FullScreenActionEventArgs 
            { 
                Action = FullScreenAction.Dismiss
            });
            
            await HideOverlayAsync();
        }

        private async void OnAutoHideTimerTick(object? sender, object e)
        {
            // Auto-hide after 2 minutes without interaction
            ActionRequested?.Invoke(this, new FullScreenActionEventArgs 
            { 
                Action = FullScreenAction.Timeout
            });
            
            await HideOverlayAsync();
        }

        /// <summary>
        /// Extends the auto-hide timer when user interacts with the overlay
        /// </summary>
        public void ExtendVisibility()
        {
            if (_isVisible && _autoHideTimer.IsEnabled)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start(); // Reset the timer
            }
        }

        /// <summary>
        /// Handles pointer interactions to extend visibility
        /// </summary>
        protected override void OnPointerMoved(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            ExtendVisibility();
        }

        /// <summary>
        /// Handles keyboard interactions to extend visibility
        /// </summary>
        protected override void OnKeyDown(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);
            ExtendVisibility();

            // Allow Escape key to dismiss
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                OnDismissClicked(this, new RoutedEventArgs());
            }
        }
    }

    /// <summary>
    /// Event arguments for full-screen overlay actions
    /// </summary>
    public class FullScreenActionEventArgs : EventArgs
    {
        public FullScreenAction Action { get; set; }
        public int Amount { get; set; } = 0;
        public TimeSpan SnoozeDuration { get; set; } = TimeSpan.Zero;
    }

    /// <summary>
    /// Types of actions that can be performed from the full-screen overlay
    /// </summary>
    public enum FullScreenAction
    {
        Drink,
        Snooze,
        Dismiss,
        Timeout
    }
} 