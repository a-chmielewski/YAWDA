using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
        private string _nextReminderText = "Initializing...";

        [ObservableProperty]
        private int _todaysTotalIntake = 0;

        [ObservableProperty]
        private int _dailyGoal = 2310; // Default based on 70kg person

        [ObservableProperty]
        private double _progressPercentage = 0.0;

        [ObservableProperty]
        private string _progressText = "0ml / 2310ml";

        [ObservableProperty]
        private string _remainingText = "2310ml remaining";

        [ObservableProperty]
        private bool _goalAchieved = false;

        [ObservableProperty]
        private UserSettings _userSettings = UserSettings.CreateDefault();

        [ObservableProperty]
        private DailyStats? _todaysStats;

        [ObservableProperty]
        private ObservableCollection<WaterIntakeRecord> _todaysIntakeHistory = new();

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
            NextReminderText = "Initializing...";
            ProgressText = "0ml / 2310ml";
            RemainingText = "2310ml remaining";
            UpdateProgressDisplay(); // Show default progress

            // Start a background task to wait for services to be ready and then load data
            _ = Task.Run(async () => await WaitForServicesAndLoadDataAsync());

            System.Diagnostics.Debug.WriteLine("MainPageViewModel constructor completed - waiting for service initialization");
        }

        /// <summary>
        /// Waits for DataService to be ready, then loads initial data
        /// </summary>
        private async Task WaitForServicesAndLoadDataAsync()
        {
            try
            {
                // Wait up to 30 seconds for DataService to be initialized
                var timeout = TimeSpan.FromSeconds(30);
                var checkInterval = TimeSpan.FromMilliseconds(500); // Check every 500ms
                var endTime = DateTime.Now + timeout;

                while (DateTime.Now < endTime)
                {
                    if (_dataService.IsInitialized)
                    {
                        System.Diagnostics.Debug.WriteLine("DataService is ready - loading initial data");
                        
                        // Load data on the current thread (should be safe now that service is ready)
                        await RefreshDataAsync();
                        return;
                    }

                    await Task.Delay(checkInterval);
                }

                // Timeout reached
                System.Diagnostics.Debug.WriteLine("DataService initialization timeout - continuing with defaults");
                NextReminderText = "Service initialization timeout - using defaults";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error waiting for services: {ex.Message}");
                NextReminderText = "Service initialization error - using defaults";
            }
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
                if (!IsDataServiceReady())
                {
                    System.Diagnostics.Debug.WriteLine("DataService not ready immediately - using defaults");
                    NextReminderText = "Using default values";
                    UpdateProgressDisplay();
                    return;
                }

                // If service is ready, try to load data quickly
                cancellationToken.ThrowIfCancellationRequested();

                // Load data with individual error handling
                try
                {
                    TodaysTotalIntake = await _dataService.GetTodaysTotalIntakeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading total intake: {ex.Message}");
                    TodaysTotalIntake = 0; // Use default
                }

                try
                {
                    var todaysRecords = await _dataService.GetDailyIntakeAsync(DateTime.Today);
                    
                    // Update history safely
                    TodaysIntakeHistory.Clear();
                    foreach (var record in todaysRecords.OrderByDescending(r => r.Timestamp))
                    {
                        TodaysIntakeHistory.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading intake history: {ex.Message}");
                    TodaysIntakeHistory.Clear(); // Use empty list
                }

                try
                {
                    // Load user settings
                    UserSettings = await _dataService.LoadSettingsAsync();
                    DailyGoal = UserSettings.EffectiveDailyGoalMilliliters;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading user settings: {ex.Message}");
                    // Keep existing settings
                }

                // Update progress calculations
                UpdateProgressDisplay();

                try
                {
                    // Load today's stats
                    TodaysStats = await _dataService.GetDailyStatsAsync(DateTime.Today);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading daily stats: {ex.Message}");
                    TodaysStats = null; // Use null
                }

                try
                {
                    // Update next reminder info on UI thread
                    await UpdateNextReminderInfoAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating reminder info: {ex.Message}");
                    NextReminderText = "Reminder info unavailable";
                }

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
        private bool IsDataServiceReady()
        {
            try
            {
                // Use the new IsInitialized property for a safe, fast check
                return _dataService.IsInitialized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataService readiness check failed: {ex.Message}");
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
                
                // Update reminder info on UI thread
                await UpdateNextReminderInfoAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pausing reminders: {ex.Message}");
                
                // Safely update UI on error
                NextReminderText = "Error pausing reminders";
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
            try
            {
                // Refresh data when reminders are triggered to update UI
                // Note: This event handler should already be called on the UI thread by the ReminderService
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnReminderTriggered: {ex.Message}");
            }
        }

        // Quick intake amounts
        public int[] QuickIntakeAmounts => new[] { 200, 300, 500, 750 };

        // Pause duration options in minutes
        public int[] PauseDurations => new[] { 15, 30, 60, 120 };
    }
} 