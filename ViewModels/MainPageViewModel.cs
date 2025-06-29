using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YAWDA.Models;
using YAWDA.Services;

namespace YAWDA.ViewModels
{
    /// <summary>
    /// ViewModel for the main page containing daily water intake tracking
    /// </summary>
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IReminderService _reminderService;
        private readonly INotificationService _notificationService;

        [ObservableProperty]
        private int _todaysTotalIntake = 0;

        [ObservableProperty]
        private int _dailyGoal = 2310;

        [ObservableProperty]
        private double _progressPercentage = 0.0;

        [ObservableProperty]
        private string _progressText = "0ml / 2310ml";

        [ObservableProperty]
        private string _remainingText = "2310ml remaining";

        [ObservableProperty]
        private bool _goalAchieved = false;

        [ObservableProperty]
        private string _nextReminderText = "Next reminder in: --";

        [ObservableProperty]
        private DailyStats? _todaysStats;

        [ObservableProperty]
        private ObservableCollection<WaterIntakeRecord> _todaysIntakeHistory = new();

        [ObservableProperty]
        private UserSettings _userSettings = UserSettings.CreateDefault();

        [ObservableProperty]
        private bool _isLoading = true;

        public MainPageViewModel(IDataService dataService, IReminderService reminderService, INotificationService notificationService)
        {
            _dataService = dataService;
            _reminderService = reminderService;
            _notificationService = notificationService;

            // Subscribe to reminder service events
            _reminderService.ReminderTriggered += OnReminderTriggered;

            // Initialize async
            Task.Run(InitializeAsync);
        }

        [RelayCommand]
        private async Task LogWaterIntakeAsync(int amount)
        {
            try
            {
                await _dataService.LogWaterIntakeAsync(amount, "manual");
                await _reminderService.RecordWaterIntakeAsync(amount);
                await RefreshDataAsync();
                
                // Show confirmation
                await _notificationService.ShowIntakeConfirmationAsync(amount);
            }
            catch (Exception ex)
            {
                // Log error - in production would use proper logging
                System.Diagnostics.Debug.WriteLine($"Error logging water intake: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;

                // Load today's data
                TodaysTotalIntake = await _dataService.GetTodaysTotalIntakeAsync();
                var todaysRecords = await _dataService.GetDailyIntakeAsync(DateTime.Today);
                
                // Update history
                TodaysIntakeHistory.Clear();
                foreach (var record in todaysRecords.OrderByDescending(r => r.Timestamp))
                {
                    TodaysIntakeHistory.Add(record);
                }

                // Load user settings
                UserSettings = await _dataService.LoadSettingsAsync();
                DailyGoal = UserSettings.EffectiveDailyGoalMilliliters;

                // Update progress calculations
                UpdateProgressDisplay();

                // Load today's stats
                TodaysStats = await _dataService.GetDailyStatsAsync(DateTime.Today);

                // Update next reminder info
                await UpdateNextReminderInfoAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task PauseRemindersAsync(int minutes)
        {
            try
            {
                await _reminderService.PauseAsync(TimeSpan.FromMinutes(minutes));
                await UpdateNextReminderInfoAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pausing reminders: {ex.Message}");
            }
        }

        private async Task InitializeAsync()
        {
            await RefreshDataAsync();
        }

        private void UpdateProgressDisplay()
        {
            ProgressPercentage = DailyGoal == 0 ? 0 : Math.Min(100.0, (TodaysTotalIntake * 100.0) / DailyGoal);
            ProgressText = $"{TodaysTotalIntake}ml / {DailyGoal}ml";
            
            var remaining = Math.Max(0, DailyGoal - TodaysTotalIntake);
            RemainingText = remaining == 0 ? "Goal achieved! 🎉" : $"{remaining}ml remaining";
            GoalAchieved = TodaysTotalIntake >= DailyGoal;
        }

        private async Task UpdateNextReminderInfoAsync()
        {
            try
            {
                if (_reminderService.IsPaused)
                {
                    NextReminderText = "Reminders paused";
                }
                else if (_reminderService.IsRunning)
                {
                    var nextTime = await _reminderService.CalculateNextReminderTimeAsync();
                    var timeUntil = nextTime - DateTime.Now;
                    
                    if (timeUntil.TotalSeconds <= 0)
                    {
                        NextReminderText = "Reminder due now";
                    }
                    else if (timeUntil.TotalHours >= 1)
                    {
                        NextReminderText = $"Next reminder in: {timeUntil.Hours}h {timeUntil.Minutes}m";
                    }
                    else
                    {
                        NextReminderText = $"Next reminder in: {timeUntil.Minutes}m";
                    }
                }
                else
                {
                    NextReminderText = "Reminders stopped";
                }
            }
            catch (Exception ex)
            {
                NextReminderText = "Unable to calculate next reminder";
                System.Diagnostics.Debug.WriteLine($"Error updating reminder info: {ex.Message}");
            }
        }

        private async void OnReminderTriggered(object? sender, ReminderEventArgs e)
        {
            // Refresh data when reminders are triggered to update UI
            await RefreshDataAsync();
        }

        // Quick intake amounts
        public int[] QuickIntakeAmounts => new[] { 200, 300, 500, 750 };

        // Pause duration options in minutes
        public int[] PauseDurations => new[] { 15, 30, 60, 120 };
    }
} 