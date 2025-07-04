using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using H.NotifyIcon;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

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
                System.Diagnostics.Debug.WriteLine("🔍 SystemTrayService.InitializeAsync called");

                // Get the main window and UI dispatcher from the main window
                if (Application.Current is App app)
                {
                    _mainWindow = app.GetMainWindow();
                    System.Diagnostics.Debug.WriteLine($"🔍 Main window obtained: {_mainWindow != null}");
                    
                    // Get the DispatcherQueue from the main window - H.NotifyIcon.WinUI requires UI thread
                    if (_mainWindow?.DispatcherQueue != null)
                    {
                        var dispatcherQueue = _mainWindow.DispatcherQueue;
                        System.Diagnostics.Debug.WriteLine("🔍 DispatcherQueue obtained successfully");
                        
                        // Create TaskbarIcon on UI thread with proper icon resource
                        var tcs = new TaskCompletionSource<bool>();
                        
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("🔍 Creating tray icon on UI thread with icon resource...");
                                
                                // Create the taskbar icon with icon resource
                                _trayIcon = new TaskbarIcon
                                {
                                    ToolTipText = "YAWDA - Water Reminder App",
                                    // Use the app's square icon for the tray
                                    IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                                        new Uri("ms-appx:///Assets/Square44x44Logo.scale-200.png")
                                    ),
                                    // Make sure the icon is visible
                                    Visibility = Microsoft.UI.Xaml.Visibility.Visible
                                };

                                System.Diagnostics.Debug.WriteLine("✓ TaskbarIcon created with icon resource");
                                
                                // Wire up event handlers for tray icon interactions
                                _trayIcon.LeftClickCommand = new RelayCommand(() => 
                                {
                                    System.Diagnostics.Debug.WriteLine("✓ Tray icon left clicked");
                                    OnTrayIconLeftClick(null, EventArgs.Empty);
                                });
                                
                                _trayIcon.RightClickCommand = new RelayCommand(() => 
                                {
                                    System.Diagnostics.Debug.WriteLine("✓ Tray icon right clicked");
                                    OnTrayIconRightClick(null, EventArgs.Empty);
                                });
                                
                                _trayIcon.DoubleClickCommand = new RelayCommand(() => 
                                {
                                    System.Diagnostics.Debug.WriteLine("✓ Tray icon double clicked");
                                    OnTrayIconDoubleClick(null, EventArgs.Empty);
                                });

                                System.Diagnostics.Debug.WriteLine("✓ Tray icon event handlers wired");

                                // Create and set up context menu
                                CreateContextMenu();
                                System.Diagnostics.Debug.WriteLine("✓ Context menu created");

                                // Set initial tooltip with current stats
                                UpdateTooltip(0, 2300, "starting...");
                                System.Diagnostics.Debug.WriteLine("✓ Initial tooltip set");
                                
                                // Make sure the icon is definitely visible in the system tray
                                _trayIcon.ForceCreate();
                                System.Diagnostics.Debug.WriteLine("✓ Tray icon forced to create and show");
                                
                                tcs.SetResult(true);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Error creating tray icon: {ex.Message}");
                                tcs.SetException(ex);
                            }
                        });
                        
                        // Wait for UI thread operation to complete
                        await tcs.Task;
                        
                        _logger.LogInformation("SystemTrayService initialized successfully with tray icon");
                        System.Diagnostics.Debug.WriteLine("✓ SystemTrayService initialization completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Could not get DispatcherQueue from main window - tray icon will not be created");
                        System.Diagnostics.Debug.WriteLine("❌ Could not get DispatcherQueue from main window");
                    }
                }
                else
                {
                    _logger.LogWarning("Could not get main window - tray icon will not be created");
                    System.Diagnostics.Debug.WriteLine("❌ Could not get App instance");
                }
                
                System.Diagnostics.Debug.WriteLine("✓ SystemTrayService initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SystemTrayService");
                System.Diagnostics.Debug.WriteLine($"❌ SystemTrayService initialization failed: {ex}");
                // Don't throw - allow app to continue without tray icon
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
                System.Diagnostics.Debug.WriteLine($"SystemTrayService.SetMainWindowVisibility called: show={show}");
                
                if (_mainWindow == null) 
                {
                    _logger.LogDebug("Main window not available");
                    System.Diagnostics.Debug.WriteLine("❌ Main window reference is null");
                    return;
                }

                if (show)
                {
                    _mainWindow.Activate();
                    _logger.LogDebug("Main window shown");
                    System.Diagnostics.Debug.WriteLine("✓ Main window activated");
                }
                else
                {
                    // Hide the window by setting its visibility to collapsed
                    // In WinUI 3, we need to use the AppWindow API for proper hiding
                    if (_mainWindow.AppWindow != null)
                    {
                        _mainWindow.AppWindow.Hide();
                        _logger.LogDebug("Main window hidden to tray");
                        System.Diagnostics.Debug.WriteLine("✓ Main window hidden using AppWindow.Hide()");
                    }
                    else
                    {
                        _logger.LogDebug("AppWindow not available - cannot hide window");
                        System.Diagnostics.Debug.WriteLine("❌ AppWindow not available - cannot hide window");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set main window visibility: {Show}", show);
                System.Diagnostics.Debug.WriteLine($"❌ Exception in SetMainWindowVisibility: {ex.Message}");
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

                var log300ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "300ml" };
                log300ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 300 });

                var log500ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "500ml" };
                log500ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 500 });

                var log750ml = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "750ml" };
                log750ml.Click += (_, _) => ManualLogRequested?.Invoke(this, new ManualLogEventArgs { Amount = 750 });

                quickLogMenu.Items.Add(log200ml);
                quickLogMenu.Items.Add(log300ml);
                quickLogMenu.Items.Add(log500ml);
                quickLogMenu.Items.Add(log750ml);

                // Pause reminders submenu
                var pauseMenu = new Microsoft.UI.Xaml.Controls.MenuFlyoutSubItem
                {
                    Text = "⏸️ Pause Reminders"
                };

                var pause30min = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "30 minutes" };
                pause30min.Click += (_, _) => PauseReminderRequested?.Invoke(this, new PauseReminderEventArgs { Duration = TimeSpan.FromMinutes(30), Reason = "User requested" });

                var pause1hr = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "1 hour" };
                pause1hr.Click += (_, _) => PauseReminderRequested?.Invoke(this, new PauseReminderEventArgs { Duration = TimeSpan.FromHours(1), Reason = "User requested" });

                var pause2hrs = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "2 hours" };
                pause2hrs.Click += (_, _) => PauseReminderRequested?.Invoke(this, new PauseReminderEventArgs { Duration = TimeSpan.FromHours(2), Reason = "User requested" });

                pauseMenu.Items.Add(pause30min);
                pauseMenu.Items.Add(pause1hr);
                pauseMenu.Items.Add(pause2hrs);

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

                // Assign the context menu to the tray icon
                _trayIcon.ContextFlyout = contextMenu;
                
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