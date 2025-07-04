using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Navigation;
using YAWDA.Services;
using YAWDA.Views;
using YAWDA.ViewModels;
using YAWDA.Utilities;
using System.Threading;

namespace YAWDA
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private IServiceProvider? serviceProvider;

        /// <summary>
        /// Gets the current service provider instance
        /// </summary>
        public static IServiceProvider? Services { get; private set; }

        /// <summary>
        /// Gets the service provider for dependency injection
        /// </summary>
        public IServiceProvider ServiceProvider => serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

        /// <summary>
        /// Gets the main window instance
        /// </summary>
        /// <returns>The main window, or null if not available</returns>
        public Window? GetMainWindow()
        {
            return window;
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            try
            {
                // Add comprehensive startup diagnostics
                System.Diagnostics.Debug.WriteLine("=== YAWDA App Constructor Starting ===");
                System.Diagnostics.Debug.WriteLine($"OS Version: {Environment.OSVersion}");
                System.Diagnostics.Debug.WriteLine($".NET Version: {Environment.Version}");
                System.Diagnostics.Debug.WriteLine($"Working Directory: {Environment.CurrentDirectory}");
                
                // Initialize component first
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("✓ InitializeComponent completed");
                
                // Configure services (this must succeed)
                ConfigureServices();
                System.Diagnostics.Debug.WriteLine("✓ Services configured");
                
                // Check Windows App SDK runtime (non-blocking)
                CheckWindowsAppSdkRuntime();
                
                System.Diagnostics.Debug.WriteLine("=== YAWDA App Constructor Completed Successfully ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== CRITICAL: App Constructor Failed ===");
                System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Exception Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                // Ensure services are configured even if other initialization fails
                if (serviceProvider == null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Attempting emergency service configuration...");
                        ConfigureServices();
                        System.Diagnostics.Debug.WriteLine("✓ Emergency service configuration successful");
                    }
                    catch (Exception serviceEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Emergency service configuration failed: {serviceEx.Message}");
                    }
                }
                
                // Try to write to file as well
                try
                {
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YAWDA", "startup_error.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App Constructor Failed: {ex}");
                }
                catch { /* Ignore file write errors */ }
                
                // Don't rethrow - allow app to continue even with partial initialization
                System.Diagnostics.Debug.WriteLine("⚠️ App constructor completed with errors - app may have limited functionality");
            }
        }

        /// <summary>
        /// Checks if Windows App SDK runtime components are available
        /// </summary>
        private void CheckWindowsAppSdkRuntime()
        {
            try
            {
                // Test basic WinUI 3 components
                var testWindow = new Window();
                testWindow = null; // Dispose immediately
                System.Diagnostics.Debug.WriteLine("✓ Windows App SDK runtime appears to be available");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Windows App SDK runtime check failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("This may indicate missing Windows App SDK runtime components.");
                System.Diagnostics.Debug.WriteLine("Please install the Windows App SDK runtime from:");
                System.Diagnostics.Debug.WriteLine("https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads");
                
                // Don't throw - log the warning and continue
                System.Diagnostics.Debug.WriteLine("⚠️ Continuing with potentially limited functionality");
            }
        }

        /// <summary>
        /// Configures the dependency injection container
        /// </summary>
        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Configure enhanced logging with file output
            #if DEBUG
            services.ConfigureEnhancedLogging(isDebugMode: true);
            #else
            services.ConfigureEnhancedLogging(isDebugMode: false);
            #endif

            // Register error handling services
            services.AddSingleton<IErrorReportingService, ErrorReportingService>();
            services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();

            // Register core services
            services.AddSingleton<IDataService, DataService>();
            services.AddSingleton<ISmartFeaturesService, SmartFeaturesService>();
            services.AddSingleton<IReminderService, ReminderService>();
            services.AddSingleton<ISystemTrayService, SystemTrayService>();
            services.AddSingleton<IOverlayService, OverlayService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IStartupService, StartupService>();

            // Register ViewModels
            services.AddTransient<MainPageViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<StatsViewModel>();

            serviceProvider = services.BuildServiceProvider();
            Services = serviceProvider;

            // Initialize global exception handler
            InitializeGlobalExceptionHandler();

            // Cleanup old logs on startup
            Task.Run(() => LoggingConfiguration.CleanupOldLogs());
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== App OnLaunched Starting ===");
                
                // Initialize the main window
                window = new Window()
                {
                    Title = "YAWDA - Yet Another Water Drinking App"
                };
                
                // Configure window for modern, compact design
                ConfigureModernWindow(window);
                
                // IMPORTANT: Add window closing handler to prevent app exit when tray is disabled
                window.Closed += OnMainWindowClosed;
                
                // Initialize services (this can fail but shouldn't crash the app)
                ConfigureServices();
                InitializeGlobalExceptionHandler();
                
                // Set up frame navigation
                Frame? rootFrame = window.Content as Frame;
                if (rootFrame == null)
                {
                    System.Diagnostics.Debug.WriteLine("🔍 Creating new Frame for navigation");
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    window.Content = rootFrame;
                    System.Diagnostics.Debug.WriteLine("✓ Frame created and set as window content");
                }

                // Safely get launch arguments
                string launchArguments = "";
                try
                {
                    launchArguments = e.Arguments ?? "";
                    System.Diagnostics.Debug.WriteLine($"🔍 Launch arguments: '{launchArguments}'");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not access launch arguments: {ex.Message}");
                    launchArguments = ""; // Use empty string as fallback
                }

                // Navigate immediately to avoid black screen
                System.Diagnostics.Debug.WriteLine("🔍 About to navigate to MainPage");
                var navigated = rootFrame.Navigate(typeof(Views.MainPage), launchArguments);
                
                if (!navigated)
                {
                    var logger = ServiceProvider.GetService<ILogger<App>>();
                    logger?.LogError("Failed to navigate to MainPage");
                    System.Diagnostics.Debug.WriteLine("❌ Failed to navigate to MainPage");
                    throw new InvalidOperationException("Navigation to MainPage failed");
                }
                
                System.Diagnostics.Debug.WriteLine("✓ Successfully navigated to MainPage");

                // Initialize services in background AFTER navigation
                System.Diagnostics.Debug.WriteLine("🔍 About to start background service initialization");
                _ = Task.Run(async () => await InitializeServicesAsync(launchArguments));

                // Check if we're in startup mode to determine window visibility
                var startupService = ServiceProvider.GetRequiredService<IStartupService>();
                startupService.InitializeStartupMode(launchArguments);

                System.Diagnostics.Debug.WriteLine($"🔍 IsStartupMode: {startupService.IsStartupMode}");
                System.Diagnostics.Debug.WriteLine($"🔍 LaunchArguments: '{launchArguments}'");

                // SIMPLIFIED: For now, always show the window to ensure it's visible
                System.Diagnostics.Debug.WriteLine("🔍 SIMPLIFIED: Always showing window for debugging");
                window.Activate();
                System.Diagnostics.Debug.WriteLine("✓ Main window activated");

                /* 
                // TODO: Re-enable this logic once we get basic functionality working
                if (startupService.IsStartupMode)
                {
                    // In startup mode, check user settings to see if we should start minimized
                    var dataService = ServiceProvider.GetRequiredService<IDataService>();
                    try
                    {
                        // Load settings synchronously by using .Result (not ideal but needed for OnLaunched)
                        var settings = dataService.LoadSettingsAsync().GetAwaiter().GetResult();
                        System.Diagnostics.Debug.WriteLine($"🔍 StartMinimized setting: {settings.StartMinimized}");
                        
                        if (settings.StartMinimized)
                        {
                            // Don't show window if starting minimized to tray
                            System.Diagnostics.Debug.WriteLine("✓ App started in startup mode with minimized setting - window will remain hidden");
                        }
                        else
                        {
                            // Show window if user doesn't want to start minimized
                            window.Activate();
                            System.Diagnostics.Debug.WriteLine("✓ App started in startup mode but user prefers window visible");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Could not load settings for startup mode: {ex.Message}");
                        // Default to showing window if we can't load settings
                        window.Activate();
                    }
                }
                else
                {
                    // Normal startup - always show window
                    System.Diagnostics.Debug.WriteLine("🔍 Normal startup detected - showing window");
                    window.Activate();
                    System.Diagnostics.Debug.WriteLine("✓ Main window activated and should be visible");
                }
                */
                
                System.Diagnostics.Debug.WriteLine("🔍 OnLaunched completed successfully");
            }
            catch (Exception ex)
            {
                // Log the startup error and attempt to show error dialog
                try
                {
                    var logger = ServiceProvider.GetService<ILogger<App>>();
                    logger?.LogCritical(ex, "Critical startup failure in OnLaunched");
                }
                catch
                {
                    // If logging fails, write to debug output
                    System.Diagnostics.Debug.WriteLine($"Critical startup failure: {ex}");
                }
                
                // Rethrow to prevent silent failure
                throw;
            }
        }

        /// <summary>
        /// Handles main window closing - check user preference for close vs minimize to tray
        /// </summary>
        private void OnMainWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Main window closing event triggered");
                
                // For now, ALWAYS prevent close and hide to tray to avoid UI thread blocking
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine("✓ Window close prevented - attempting to hide to tray");
                
                // Hide the window immediately without loading settings to avoid UI thread freeze
                if (window != null)
                {
                    try
                    {
                        // Try to hide using AppWindow
                        if (window.AppWindow != null)
                        {
                            window.AppWindow.Hide();
                            System.Diagnostics.Debug.WriteLine("✓ Window hidden using AppWindow.Hide()");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("❌ AppWindow not available");
                        }
                        
                        // Show a simple message that the app is still running
                        System.Diagnostics.Debug.WriteLine("✓ App minimized to background - should be accessible via tray");
                    }
                    catch (Exception hideEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error hiding window: {hideEx.Message}");
                        // If hiding fails, allow the window to close
                        e.Handled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error during window close: {ex.Message}");
                // If there's an error, default to allowing the window to close
                e.Handled = false;
            }
        }

        /// <summary>
        /// Configures the window for modern, compact design
        /// </summary>
        private void ConfigureModernWindow(Window window)
        {
            try
            {
                // Get the AppWindow for advanced configuration
                var appWindow = window.AppWindow;
                
                // Set window size to fit content (800px content + 40px margins + some padding)
                // Calculate optimal size: content width + margins + title bar height
                int windowWidth = 900;  // 800px content + 40px margins + 60px extra padding for safety
                int windowHeight = 1000; // Increased from 580 to provide more vertical space
                
                // Center the window on screen - use simpler approach
                // Get primary display area
                var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                int centerX = (displayArea.OuterBounds.Width - windowWidth) / 2;
                int centerY = (displayArea.OuterBounds.Height - windowHeight) / 2;
                
                // Set size and position
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centerX, centerY, windowWidth, windowHeight));
                
                // Configure modern window properties
                if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    
                    // Enable custom title bar
                    titleBar.ExtendsContentIntoTitleBar = true;
                    
                    // Set title bar colors for modern look
                    titleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 243, 243, 243); // Light gray
                    titleBar.ForegroundColor = Windows.UI.Color.FromArgb(255, 32, 32, 32); // Dark text
                    titleBar.InactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 248, 248, 248);
                    titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                    
                    // Set button colors
                    titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0); // Transparent
                    titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 32, 32, 32);
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 229, 229, 229);
                    titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 32, 32, 32);
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 204, 204, 204);
                    titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 32, 32, 32);
                    
                    // Inactive button colors
                    titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                }
                
                // Set window presenter for modern look - use simpler approach
                try
                {
                    var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                    if (presenter != null)
                    {
                        // Disable resize and maximize for a fixed-size focused app experience
                        presenter.IsMaximizable = false;
                        presenter.IsMinimizable = true;
                        presenter.IsResizable = false; // Fixed size for consistent layout
                        
                        // Also disable resize mode to ensure it's truly fixed
                        presenter.SetBorderAndTitleBar(true, true);
                        
                        System.Diagnostics.Debug.WriteLine("✓ Window presenter configured: non-resizable, non-maximizable");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Could not cast presenter to OverlappedPresenter");
                    }
                }
                catch (Exception presenterEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Could not configure window presenter: {presenterEx.Message}");
                    // Continue without presenter customization
                }
                
                System.Diagnostics.Debug.WriteLine($"✓ Modern window configured: {windowWidth}x{windowHeight} at ({centerX}, {centerY})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Failed to configure modern window: {ex.Message}");
                // Continue without modern styling if configuration fails
            }
        }

        /// <summary>
        /// Initializes services asynchronously in the background
        /// </summary>
        private async Task InitializeServicesAsync(string arguments)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Starting Service Initialization ===");
                
                // Initialize startup service (this should be fast)
                var startupService = ServiceProvider.GetRequiredService<IStartupService>();
                startupService.InitializeStartupMode(arguments);
                System.Diagnostics.Debug.WriteLine("✓ StartupService initialized");

                // Initialize background services with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15 second timeout
                System.Diagnostics.Debug.WriteLine("🔍 About to call InitializeBackgroundServicesAsync");
                
                await InitializeBackgroundServicesAsync(cts.Token);
                
                System.Diagnostics.Debug.WriteLine("✓ InitializeBackgroundServicesAsync completed");
                
                // IMPORTANT: Keep app alive for background services
                // Since system tray is disabled, we need to prevent WinUI 3 from auto-exiting
                _ = Task.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("🔄 Background keep-alive task started - app will stay running for reminders");
                    // This task runs indefinitely to keep the app alive
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5)); // Check every 5 minutes
                        System.Diagnostics.Debug.WriteLine("⏰ App keep-alive check - reminders still active");
                    }
                });
                
                System.Diagnostics.Debug.WriteLine("=== Service Initialization Complete ===");
            }
            catch (OperationCanceledException)
            {
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogWarning("Service initialization timed out after 15 seconds - app will continue with limited functionality");
                System.Diagnostics.Debug.WriteLine("⚠️ Service initialization timed out - app will run with limited functionality");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to initialize services - app will continue with limited functionality");
                System.Diagnostics.Debug.WriteLine($"❌ Service initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Initializes background services required for operation
        /// </summary>
        private async Task InitializeBackgroundServicesAsync(CancellationToken cancellationToken = default)
        {
            IErrorReportingService? errorReportingService = null;
            
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 InitializeBackgroundServicesAsync started");
                
                errorReportingService = ServiceProvider.GetRequiredService<IErrorReportingService>();
                System.Diagnostics.Debug.WriteLine("✓ ErrorReportingService obtained");
                
                // Initialize DataService with timeout
                System.Diagnostics.Debug.WriteLine("🔍 About to initialize DataService");
                await InitializeServiceWithGracefulDegradation(
                    async () =>
                    {
                        var dataService = ServiceProvider.GetRequiredService<IDataService>();
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(8)); // DataService gets 8 seconds max
                        await dataService.InitializeAsync();
                        System.Diagnostics.Debug.WriteLine("✓ DataService initialized successfully");
                    },
                    "DataService",
                    errorReportingService,
                    cancellationToken
                );
                
                // Initialize other services (these should be faster)
                System.Diagnostics.Debug.WriteLine("🔍 About to initialize ReminderService");
                await InitializeServiceWithGracefulDegradation(
                    async () =>
                    {
                        var reminderService = ServiceProvider.GetRequiredService<IReminderService>();
                        await reminderService.StartAsync();
                        System.Diagnostics.Debug.WriteLine("✓ ReminderService started");
                    },
                    "ReminderService",
                    errorReportingService,
                    cancellationToken
                );

                System.Diagnostics.Debug.WriteLine("🔍 About to initialize SystemTrayService");
                await InitializeServiceWithGracefulDegradation(
                    async () =>
                    {
                        var systemTrayService = ServiceProvider.GetRequiredService<ISystemTrayService>();
                        await systemTrayService.InitializeAsync();
                        
                        // Wire up tray icon event handlers
                        systemTrayService.ShowMainWindowRequested += OnShowMainWindowRequested;
                        systemTrayService.ExitRequested += OnExitRequested;
                        systemTrayService.ManualLogRequested += OnManualLogRequested;
                        systemTrayService.ShowSettingsRequested += OnShowSettingsRequested;
                        systemTrayService.ShowStatsRequested += OnShowStatsRequested;
                        systemTrayService.PauseReminderRequested += OnPauseReminderRequested;
                        
                        System.Diagnostics.Debug.WriteLine("✓ SystemTrayService initialized");
                    },
                    "SystemTrayService",
                    errorReportingService,
                    cancellationToken
                );
                
                System.Diagnostics.Debug.WriteLine("✓ All background services initialized successfully");
            }
            catch (Exception ex)
            {
                errorReportingService?.ReportErrorAsync(ex, "Critical service initialization failure", false);
                System.Diagnostics.Debug.WriteLine($"❌ Critical error in background service initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Initializes a service with graceful degradation on failure
        /// </summary>
        private async Task InitializeServiceWithGracefulDegradation(
            Func<Task> serviceInitializer, 
            string serviceName, 
            IErrorReportingService errorReportingService,
            CancellationToken cancellationToken = default)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 Starting initialization of {serviceName}");
                cancellationToken.ThrowIfCancellationRequested();
                await serviceInitializer();
                System.Diagnostics.Debug.WriteLine($"✓ {serviceName} initialized successfully");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ {serviceName} initialization was cancelled");
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ {serviceName} initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ {serviceName} stack trace: {ex.StackTrace}");
                errorReportingService?.ReportErrorAsync(ex, $"{serviceName} initialization failed", false);
                // Don't rethrow - allow app to continue with degraded functionality
            }
        }

        /// <summary>
        /// Initializes the global exception handler
        /// </summary>
        private void InitializeGlobalExceptionHandler()
        {
            try
            {
                var globalExceptionHandler = ServiceProvider.GetRequiredService<IGlobalExceptionHandler>();
                globalExceptionHandler.Initialize();
            }
            catch (Exception ex)
            {
                // Fallback logging if global exception handler setup fails
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to initialize global exception handler");
            }
        }

        /// <summary>
        /// Handles system tray request to show main window
        /// </summary>
        private void OnShowMainWindowRequested(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("✓ Show main window requested from tray");
                if (window != null)
                {
                    window.Activate();
                    System.Diagnostics.Debug.WriteLine("✓ Main window activated from tray icon");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Main window reference is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error showing main window from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Emergency method to show window - can be called if tray icon fails
        /// </summary>
        public void EmergencyShowWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🆘 Emergency show window called");
                if (window != null)
                {
                    window.Activate();
                    System.Diagnostics.Debug.WriteLine("✓ Emergency window show successful");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Emergency window show failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles system tray request to exit the application
        /// </summary>
        private void OnExitRequested(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("✓ Exit requested from tray icon - closing application");
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error exiting application from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles system tray request to log water manually
        /// </summary>
        private async void OnManualLogRequested(object? sender, Services.ManualLogEventArgs e)
        {
            try
            {
                var dataService = ServiceProvider.GetRequiredService<IDataService>();
                await dataService.LogWaterIntakeAsync(e.Amount, "Manual - Tray Icon");
                System.Diagnostics.Debug.WriteLine($"✓ Manual water log from tray: {e.Amount}ml");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error logging water from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles system tray request to show settings
        /// </summary>
        private void OnShowSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                // Show main window first, then navigate to settings
                if (window != null)
                {
                    window.Activate();
                    
                    // Navigate to settings page
                    if (window.Content is Frame frame)
                    {
                        frame.Navigate(typeof(Views.SettingsPage));
                    }
                    
                    System.Diagnostics.Debug.WriteLine("✓ Settings page requested from tray icon");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error showing settings from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles system tray request to show statistics
        /// </summary>
        private void OnShowStatsRequested(object? sender, EventArgs e)
        {
            try
            {
                // Show main window first, then navigate to stats
                if (window != null)
                {
                    window.Activate();
                    
                    // For now, just show main window - stats navigation can be added later
                    System.Diagnostics.Debug.WriteLine("✓ Statistics page requested from tray icon");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error showing stats from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles system tray request to pause reminders
        /// </summary>
        private async void OnPauseReminderRequested(object? sender, Services.PauseReminderEventArgs e)
        {
            try
            {
                var reminderService = ServiceProvider.GetRequiredService<IReminderService>();
                await reminderService.PauseAsync(e.Duration);
                System.Diagnostics.Debug.WriteLine($"✓ Reminders paused from tray: {e.Duration.TotalMinutes} minutes");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error pausing reminders from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            var navigationException = new UserInterfaceException($"Failed to load Page {e.SourcePageType.FullName}", "NAV_001");
            
            // Report through error reporting service
            try
            {
                var errorReportingService = ServiceProvider.GetRequiredService<IErrorReportingService>();
                _ = errorReportingService.ReportErrorAsync(navigationException, "Navigation");
            }
            catch
            {
                // Fallback to direct exception if error reporting fails
                throw navigationException;
            }
        }
    }
}

