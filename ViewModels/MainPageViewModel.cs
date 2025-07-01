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
using System.Threading;

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

            // Set initial values immediately to show UI fast
            IsLoading = false; // Start with loading false
            NextReminderText = "Loading...";
            ProgressText = "0ml / 2310ml";
            RemainingText = "2310ml remaining";
            UpdateProgressDisplay(); // Show default progress

            // Try to load data with very short timeout - if it fails, just show defaults
            _ = Task.Run(async () => 
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Very short timeout
                    await InitializeAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("MainPageViewModel initialization timed out - using defaults");
                    NextReminderText = "Using default values";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainPageViewModel initialization error: {ex.Message}");
                    NextReminderText = "Data unavailable - showing defaults";
                }
            });
        }

        [RelayCommand]
        private async Task LogWaterIntakeAsync(object parameter)
        {
            try
            {
                int amount;
                if (parameter is int intValue)
                {
                    amount = intValue;
                }
                else if (parameter is string strValue && int.TryParse(strValue, out int parsedValue))
                {
                    amount = parsedValue;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid water intake amount parameter: {parameter}");
                    return;
                }

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
            await RefreshDataAsync(CancellationToken.None);
        }

        private async Task RefreshDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                // Quick check - if service isn't immediately ready, use defaults
                if (!await IsDataServiceReadyAsync())
                {
                    System.Diagnostics.Debug.WriteLine("DataService not ready immediately - using defaults");
                    NextReminderText = "Using default values";
                    UpdateProgressDisplay();
                    return;
                }

                // If service is ready, try to load data quickly
                cancellationToken.ThrowIfCancellationRequested();

                // Load data with individual timeouts
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

                // Update next reminder info (don't await to avoid blocking)
                _ = Task.Run(async () => await UpdateNextReminderInfoAsync());

                NextReminderText = "Data loaded successfully";
            }
            catch (OperationCanceledException)
            {
                NextReminderText = "Load timeout - using defaults";
                UpdateProgressDisplay(); // Show defaults
                System.Diagnostics.Debug.WriteLine("Data refresh was cancelled");
            }
            catch (Exception ex)
            {
                NextReminderText = "Load error - using defaults";
                UpdateProgressDisplay(); // Show defaults
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks if the DataService is ready for operations
        /// </summary>
        private async Task<bool> IsDataServiceReadyAsync()
        {
            try
            {
                // Try a simple operation to test if DataService is ready
                await _dataService.GetTodaysTotalIntakeAsync();
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Database not initialized"))
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [RelayCommand]
        private async Task PauseRemindersAsync(object parameter)
        {
            try
            {
                int minutes;
                if (parameter is int intValue)
                {
                    minutes = intValue;
                }
                else if (parameter is string strValue && int.TryParse(strValue, out int parsedValue))
                {
                    minutes = parsedValue;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid pause duration parameter: {parameter}");
                    return;
                }

                await _reminderService.PauseAsync(TimeSpan.FromMinutes(minutes));
                await UpdateNextReminderInfoAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pausing reminders: {ex.Message}");
            }
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await RefreshDataAsync(cancellationToken);
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