using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

namespace YAWDA.Views
{
    /// <summary>
    /// Level 2 disruption: Top-screen banner overlay for water reminders
    /// </summary>
    public sealed partial class BannerOverlay : UserControl
    {
        private readonly DispatcherTimer _autoHideTimer;
        private bool _isVisible = false;

        public event EventHandler<BannerActionEventArgs>? ActionRequested;

        public BannerOverlay()
        {
            this.InitializeComponent();
            
            // Set up auto-hide timer (30 seconds)
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _autoHideTimer.Tick += OnAutoHideTimerTick;
        }

        /// <summary>
        /// Shows the banner with the specified reminder message and progress
        /// </summary>
        /// <param name="message">Reminder message to display</param>
        /// <param name="progressText">Current daily progress text</param>
        public Task ShowBannerAsync(string message, string progressText = "")
        {
            if (_isVisible) return Task.CompletedTask;

            ReminderMessageText.Text = message;
            if (!string.IsNullOrEmpty(progressText))
            {
                ProgressText.Text = progressText;
                ProgressText.Visibility = Visibility.Visible;
            }
            else
            {
                ProgressText.Visibility = Visibility.Collapsed;
            }

            this.Visibility = Visibility.Visible;
            _isVisible = true;

            // Start slide-in animation
            SlideInAnimation.Begin();
            
            // Start auto-hide timer
            _autoHideTimer.Start();

            // Play system notification sound
            try
            {
                var player = new Windows.Media.Playback.MediaPlayer();
                player.Source = Windows.Media.Core.MediaSource.CreateFromUri(
                    new Uri("ms-winsoundevent:Notification.Reminder"));
                player.Play();
            }
            catch
            {
                // Ignore audio errors
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Hides the banner with slide-out animation
        /// </summary>
        public async Task HideBannerAsync()
        {
            if (!_isVisible) return;

            _autoHideTimer.Stop();
            _isVisible = false;

            // Start slide-out animation
            SlideOutAnimation.Begin();

            // Wait for animation to complete
            await Task.Delay(300);
            
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the progress text display
        /// </summary>
        /// <param name="currentIntake">Current daily intake in ml</param>
        /// <param name="dailyGoal">Daily goal in ml</param>
        public void UpdateProgress(int currentIntake, int dailyGoal)
        {
            var percentage = dailyGoal > 0 ? (int)((double)currentIntake / dailyGoal * 100) : 0;
            ProgressText.Text = $"Daily progress: {currentIntake}ml / {dailyGoal}ml ({percentage}%)";
        }

        private async void OnDrinkClicked(object sender, RoutedEventArgs e)
        {
            ActionRequested?.Invoke(this, new BannerActionEventArgs 
            { 
                Action = BannerAction.Drink,
                Amount = 250 // Default amount
            });
            
            await HideBannerAsync();
        }

        private async void OnSnoozeClicked(object sender, RoutedEventArgs e)
        {
            ActionRequested?.Invoke(this, new BannerActionEventArgs 
            { 
                Action = BannerAction.Snooze,
                SnoozeDuration = TimeSpan.FromMinutes(10)
            });
            
            await HideBannerAsync();
        }

        private async void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            ActionRequested?.Invoke(this, new BannerActionEventArgs 
            { 
                Action = BannerAction.Dismiss
            });
            
            await HideBannerAsync();
        }

        private async void OnAutoHideTimerTick(object? sender, object e)
        {
            // Auto-hide after 30 seconds without interaction
            ActionRequested?.Invoke(this, new BannerActionEventArgs 
            { 
                Action = BannerAction.Timeout
            });
            
            await HideBannerAsync();
        }

        /// <summary>
        /// Extends the auto-hide timer when user interacts with the banner
        /// </summary>
        public void ExtendVisibility()
        {
            if (_isVisible && _autoHideTimer.IsEnabled)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start(); // Reset the timer
            }
        }
    }

    /// <summary>
    /// Event arguments for banner actions
    /// </summary>
    public class BannerActionEventArgs : EventArgs
    {
        public BannerAction Action { get; set; }
        public int Amount { get; set; } = 0;
        public TimeSpan SnoozeDuration { get; set; } = TimeSpan.Zero;
    }

    /// <summary>
    /// Types of actions that can be performed from the banner
    /// </summary>
    public enum BannerAction
    {
        Drink,
        Snooze,
        Dismiss,
        Timeout
    }
} 