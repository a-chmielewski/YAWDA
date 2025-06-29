using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YAWDA.Models;
using YAWDA.Services;

namespace YAWDA.ViewModels
{
    /// <summary>
    /// ViewModel for the settings page
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IReminderService _reminderService;
        private readonly IStartupService _startupService;

        [ObservableProperty]
        private UserSettings _settings = UserSettings.CreateDefault();

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isStatusMessageVisible = false;

        public SettingsViewModel(IDataService dataService, IReminderService reminderService, IStartupService startupService)
        {
            _dataService = dataService;
            _reminderService = reminderService;
            _startupService = startupService;

            // Subscribe to property changes to track unsaved changes
            PropertyChanged += OnPropertyChanged;

            // Initialize async
            Task.Run(LoadSettingsAsync);
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                if (!Settings.IsValid())
                {
                    ShowStatusMessage("Please check your settings - some values are invalid.", false);
                    return;
                }

                Settings.UpdateLastModified();
                await _dataService.SaveSettingsAsync(Settings);
                await _reminderService.UpdateSettingsAsync(Settings);

                // Update startup configuration if changed
                var startupEnabled = await _startupService.IsStartupEnabledAsync();
                if (startupEnabled != Settings.StartWithWindows)
                {
                    var startupSuccess = await _startupService.SetStartupEnabledAsync(Settings.StartWithWindows);
                    if (!startupSuccess)
                    {
                        ShowStatusMessage("Settings saved, but auto-startup configuration failed. Check permissions.", false);
                        return;
                    }
                }

                HasUnsavedChanges = false;
                ShowStatusMessage("Settings saved successfully!", true);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"Error saving settings: {ex.Message}", false);
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            try
            {
                Settings = UserSettings.CreateDefault();
                HasUnsavedChanges = true;
                ShowStatusMessage("Settings reset to defaults. Click Save to apply.", true);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"Error resetting settings: {ex.Message}", false);
                System.Diagnostics.Debug.WriteLine($"Error resetting settings: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                Settings = await _dataService.LoadSettingsAsync();
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"Error loading settings: {ex.Message}", false);
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                Settings = UserSettings.CreateDefault();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Track changes to settings properties
            if (e.PropertyName == nameof(Settings) && !IsLoading)
            {
                HasUnsavedChanges = true;
            }
            
            // Track changes to individual properties
            if (!IsLoading && e.PropertyName != nameof(IsLoading) && 
                e.PropertyName != nameof(HasUnsavedChanges) && 
                e.PropertyName != nameof(StatusMessage) && 
                e.PropertyName != nameof(IsStatusMessageVisible))
            {
                HasUnsavedChanges = true;
            }
        }

        private void ShowStatusMessage(string message, bool isSuccess)
        {
            StatusMessage = message;
            IsStatusMessageVisible = true;

            // Hide message after 3 seconds
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                IsStatusMessageVisible = false;
                StatusMessage = string.Empty;
            });
        }

        // Helper properties for UI binding
        public string CalculatedDailyGoalText => $"Calculated: {Settings.CalculatedDailyGoalMilliliters}ml";
        
        public string WorkHoursText => $"{Settings.WorkHoursStart:hh\\:mm} - {Settings.WorkHoursEnd:hh\\:mm}";

        public string[] AvailableThemes => new[] { "Light", "Dark", "System" };

        public int[] DisruptionLevels => new[] { 1, 2, 3, 4 };

        public string[] DisruptionLevelDescriptions => new[]
        {
            "Toast notifications only",
            "Enhanced toasts + banners",
            "Overlays + enhanced disruption",
            "Maximum disruption (screen lock)"
        };

        public int[] ReminderIntervals => new[] { 15, 30, 45, 60, 90, 120, 180 };

        public int[] DataRetentionOptions => new[] { 30, 90, 180, 365, 730, 1095 };

        // Startup method information
        public string StartupMethodText => $"Method: {_startupService.GetStartupMethod()}";

        // Theme management helpers
        public string SelectedTheme
        {
            get => Settings.Theme.ToString();
            set
            {
                if (Enum.TryParse<AppTheme>(value, out var theme) && Settings.Theme != theme)
                {
                    Settings.Theme = theme;
                    OnPropertyChanged();
                    if (!IsLoading)
                    {
                        HasUnsavedChanges = true;
                    }
                }
            }
        }

        // Custom daily goal text helper
        public string CustomDailyGoalText
        {
            get => Settings.CustomDailyGoalMilliliters?.ToString() ?? string.Empty;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Settings.CustomDailyGoalMilliliters = null;
                }
                else if (int.TryParse(value, out var goal) && goal >= 500 && goal <= 5000)
                {
                    Settings.CustomDailyGoalMilliliters = goal;
                }
                OnPropertyChanged();
                if (!IsLoading)
                {
                    HasUnsavedChanges = true;
                }
            }
        }
    }
} 