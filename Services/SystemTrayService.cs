using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using H.NotifyIcon;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for managing system tray icon and user interactions
    /// Provides quick access to app functionality without showing main window
    /// </summary>
    public class SystemTrayService : ISystemTrayService, IDisposable
    {
        private readonly ILogger<SystemTrayService> _logger;
        private readonly IDataService _dataService;
        private TaskbarIcon? _trayIcon;
        private Window? _mainWindow;
        private bool _disposed = false;

        // Event declarations
        public event EventHandler? ShowMainWindowRequested;
        public event EventHandler<ManualLogEventArgs>? ManualLogRequested;
        public event EventHandler? ShowSettingsRequested;
        public event EventHandler? ShowStatsRequested;
        public event EventHandler<PauseReminderEventArgs>? PauseReminderRequested;
        public event EventHandler? ExitRequested;

        public bool IsMainWindowVisible => _mainWindow?.Visible ?? false;

        public SystemTrayService(ILogger<SystemTrayService> logger, IDataService dataService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing SystemTrayService...");

                // Get the main window and UI dispatcher from the main window
                if (Application.Current is App app)
                {
                    _mainWindow = app.GetMainWindow();
                    
                    // Get the DispatcherQueue from the main window
                    if (_mainWindow?.DispatcherQueue != null)
                    {
                        var dispatcherQueue = _mainWindow.DispatcherQueue;
                        
                        // Create TaskbarIcon on UI thread
                        var tcs = new TaskCompletionSource<bool>();
                        
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // Create the taskbar icon on UI thread
                                _trayIcon = new TaskbarIcon
                                {
                                    ToolTipText = "YAWDA - Yet Another Water Drinking App"
                                };

                                // Create context menu
                                CreateContextMenu();

                                // Set initial basic tooltip
                                UpdateTooltip(0, 2310, "starting...");
                                
                                tcs.SetResult(true);
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(ex);
                            }
                        });
                        
                        // Wait for UI thread operation to complete
                        await tcs.Task;
                        
                        _logger.LogInformation("SystemTrayService initialized successfully with tray icon");
                    }
                    else
                    {
                        _logger.LogWarning("Could not get DispatcherQueue from main window - tray icon will not be created");
                    }
                }
                else
                {
                    _logger.LogWarning("Could not get main window - tray icon will not be created");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SystemTrayService");
                throw;
            }
        }

        /// <inheritdoc />
        public void UpdateTooltip(int currentIntake, int dailyGoal, string nextReminderTime)
        {
            try
            {
                if (_trayIcon == null) 
                {
                    _logger.LogDebug("Tray icon not available, skipping tooltip update");
                    return;
                }

                var progress = dailyGoal > 0 ? (double)currentIntake / dailyGoal * 100 : 0;
                var progressBar = GenerateProgressBar(progress);
                
                var tooltip = $"YAWDA - Water Reminder\n" +
                             $"{progressBar} {progress:F0}%\n" +
                             $"{currentIntake}ml / {dailyGoal}ml today\n" +
                             $"Next reminder: {nextReminderTime}";

                _trayIcon.ToolTipText = tooltip;
                _logger.LogDebug("Tray tooltip updated: {Progress:F0}%, Next: {NextReminder}", progress, nextReminderTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray tooltip");
            }
        }

        /// <inheritdoc />
        public Task ShowTrayNotificationAsync(string title, string message, int timeout = 3000)
        {
            try
            {
                if (_trayIcon == null) 
                {
                    _logger.LogDebug("Tray icon not available, skipping notification: {Title} - {Message}", title, message);
                    return Task.CompletedTask;
                }

                // For now, simplified balloon notification
                // TODO: Implement proper balloon notifications with H.NotifyIcon
                _logger.LogInformation("Tray notification would show: {Title} - {Message}", title, message);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show tray notification: {Title}", title);
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public void SetMainWindowVisibility(bool show)
        {
            try
            {
                if (_mainWindow == null) 
                {
                    _logger.LogDebug("Main window not available");
                    return;
                }

                if (show)
                {
                    _mainWindow.Activate();
                    // Note: WinUI 3 Window doesn't have WindowState, so we just activate
                    _logger.LogDebug("Main window shown");
                }
                else
                {
                    // Note: Since there's no tray icon to hide to, just minimize instead of closing
                    _logger.LogDebug("Main window hide requested (keeping open since no tray icon)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set main window visibility: {Show}", show);
            }
        }

        /// <summary>
        /// Creates the context menu for the tray icon
        /// </summary>
        private void CreateContextMenu()
        {
            try
            {
                if (_trayIcon == null)
                {
                    _logger.LogWarning("Cannot create context menu - tray icon not initialized");
                    return;
                }

                // Create MenuFlyout for the tray icon
                var contextMenu = new Microsoft.UI.Xaml.Controls.MenuFlyout();

                // Quick Log Water submenu
                var quickLogMenu = new Microsoft.UI.Xaml.Controls.MenuFlyoutSubItem
                {
                    Text = "💧 Quick Log Water"
                };

                var log200ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "200ml" };
                log200ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 200 });

                var log350ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "350ml" };
                log350ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 350 });

                var log500ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "500ml" };
                log500ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 500 });

                quickLogMenu.Items.Add(log200ml);
                quickLogMenu.Items.Add(log350ml);
                quickLogMenu.Items.Add(log500ml);

                // Pause Reminders submenu
                var pauseMenu = new Microsoft.UI.Xaml.Controls.MenuFlyoutSubItem
                {
                    Text = "⏸️ Pause Reminders"
                };

                var pause15min = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "15 minutes" };
                pause15min.Click += (_, _) => PauseReminderRequested?.Invoke(this, 
                    new PauseReminderEventArgs { Duration = TimeSpan.FromMinutes(15), Reason = "User request - 15 min" });

                var pause30min = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "30 minutes" };
                pause30min.Click += (_, _) => PauseReminderRequested?.Invoke(this, 
                    new PauseReminderEventArgs { Duration = TimeSpan.FromMinutes(30), Reason = "User request - 30 min" });

                var pause1hour = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "1 hour" };
                pause1hour.Click += (_, _) => PauseReminderRequested?.Invoke(this, 
                    new PauseReminderEventArgs { Duration = TimeSpan.FromHours(1), Reason = "User request - 1 hour" });

                pauseMenu.Items.Add(pause15min);
                pauseMenu.Items.Add(pause30min);
                pauseMenu.Items.Add(pause1hour);

                // Separator
                var separator1 = new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator();

                // Main menu items
                var showMain = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "📊 Show Dashboard" 
                };
                showMain.Click += (_, _) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);

                var showStats = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "📈 Statistics" 
                };
                showStats.Click += (_, _) => ShowStatsRequested?.Invoke(this, EventArgs.Empty);

                var showSettings = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "⚙️ Settings" 
                };
                showSettings.Click += (_, _) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);

                // Separator
                var separator2 = new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator();

                var exitApp = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "❌ Exit YAWDA" 
                };
                exitApp.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

                // Add all items to menu
                contextMenu.Items.Add(quickLogMenu);
                contextMenu.Items.Add(pauseMenu);
                contextMenu.Items.Add(separator1);
                contextMenu.Items.Add(showMain);
                contextMenu.Items.Add(showStats);
                contextMenu.Items.Add(showSettings);
                contextMenu.Items.Add(separator2);
                contextMenu.Items.Add(exitApp);

                // Note: Context menu assignment will be implemented in a future update
                // For now, we'll handle right-click through event handlers
                
                _logger.LogDebug("Context menu created with {ItemCount} items", contextMenu.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create context menu");
                throw;
            }
        }

        /// <summary>
        /// Updates the tooltip with current daily progress
        /// </summary>
        private async Task UpdateTooltipAsync()
        {
            try
            {
                // Try to get data, but gracefully handle if DataService isn't ready yet
                var currentIntake = 0;
                var dailyGoal = 2000; // Default value
                
                try
                {
                    currentIntake = await _dataService.GetTodaysTotalIntakeAsync();
                    var settings = await _dataService.LoadSettingsAsync();
                    dailyGoal = settings.EffectiveDailyGoalMilliliters;
                }
                catch (InvalidOperationException)
                {
                    // DataService not initialized yet, use defaults
                    _logger.LogDebug("DataService not ready during tooltip update, using defaults");
                }
                
                UpdateTooltip(currentIntake, dailyGoal, "calculating...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tooltip with current data");
            }
        }

        /// <summary>
        /// Handles left click on tray icon (toggle main window)
        /// </summary>
        private void OnTrayIconLeftClick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogDebug("Tray icon left clicked");
                ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tray icon left click");
            }
        }

        /// <summary>
        /// Handles right click on tray icon (show context menu)
        /// </summary>
        private void OnTrayIconRightClick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogDebug("Tray icon right clicked - showing context menu");
                ShowContextMenu();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tray icon right click");
            }
        }

        /// <summary>
        /// Handles double click on tray icon (show main window)
        /// </summary>
        private void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogDebug("Tray icon double clicked");
                ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tray icon double click");
            }
        }

        /// <summary>
        /// Shows the context menu with available actions
        /// </summary>
        private void ShowContextMenu()
        {
            try
            {
                // Create a simple context menu using WinUI MenuFlyout
                var contextMenu = new Microsoft.UI.Xaml.Controls.MenuFlyout();

                // Quick Log Water submenu
                var quickLogMenu = new Microsoft.UI.Xaml.Controls.MenuFlyoutSubItem
                {
                    Text = "💧 Quick Log Water"
                };

                var log200ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "200ml" };
                log200ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 200 });

                var log350ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "350ml" };
                log350ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 350 });

                var log500ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "500ml" };
                log500ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 500 });

                quickLogMenu.Items.Add(log200ml);
                quickLogMenu.Items.Add(log350ml);
                quickLogMenu.Items.Add(log500ml);

                // Pause Reminders submenu
                var pauseMenu = new Microsoft.UI.Xaml.Controls.MenuFlyoutSubItem
                {
                    Text = "⏸️ Pause Reminders"
                };

                var pause15min = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "15 minutes" };
                pause15min.Click += (_, _) => PauseReminderRequested?.Invoke(this, 
                    new PauseReminderEventArgs { Duration = TimeSpan.FromMinutes(15), Reason = "User request - 15 min" });

                var pause30min = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "30 minutes" };
                pause30min.Click += (_, _) => PauseReminderRequested?.Invoke(this, 
                    new PauseReminderEventArgs { Duration = TimeSpan.FromMinutes(30), Reason = "User request - 30 min" });

                var pause1hour = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "1 hour" };
                pause1hour.Click += (_, _) => PauseReminderRequested?.Invoke(this, 
                    new PauseReminderEventArgs { Duration = TimeSpan.FromHours(1), Reason = "User request - 1 hour" });

                pauseMenu.Items.Add(pause15min);
                pauseMenu.Items.Add(pause30min);
                pauseMenu.Items.Add(pause1hour);

                // Separator
                var separator1 = new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator();

                // Main menu items
                var showMain = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "📊 Show Dashboard" 
                };
                showMain.Click += (_, _) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);

                var showStats = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "📈 Statistics" 
                };
                showStats.Click += (_, _) => ShowStatsRequested?.Invoke(this, EventArgs.Empty);

                var showSettings = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "⚙️ Settings" 
                };
                showSettings.Click += (_, _) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);

                // Separator
                var separator2 = new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator();

                var exitApp = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem 
                { 
                    Text = "❌ Exit YAWDA" 
                };
                exitApp.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

                // Add all items to menu
                contextMenu.Items.Add(quickLogMenu);
                contextMenu.Items.Add(pauseMenu);
                contextMenu.Items.Add(separator1);
                contextMenu.Items.Add(showMain);
                contextMenu.Items.Add(showStats);
                contextMenu.Items.Add(showSettings);
                contextMenu.Items.Add(separator2);
                contextMenu.Items.Add(exitApp);

                // For now, we'll simplify the context menu implementation
                // In a full implementation, we'd show this at the cursor position
                // contextMenu.ShowAt(someFrameworkElement);
                _logger.LogDebug("Context menu created with {ItemCount} items", contextMenu.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show context menu");
            }
        }

        /// <summary>
        /// Generates a simple ASCII progress bar for tooltip
        /// </summary>
        private static string GenerateProgressBar(double percentage)
        {
            const int barLength = 10;
            var filledLength = (int)(percentage / 100.0 * barLength);
            var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
            return $"[{bar}]";
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    if (_trayIcon != null)
                    {
                        // TODO: Remove event handlers when we add them
                        _trayIcon.Dispose();
                        _trayIcon = null;
                    }

                    _logger.LogDebug("SystemTrayService disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during SystemTrayService disposal");
                }

                _disposed = true;
            }
        }
    }
} 