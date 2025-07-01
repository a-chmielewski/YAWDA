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
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    window.Content = rootFrame;
                }

                // Safely get launch arguments
                string launchArguments = "";
                try
                {
                    launchArguments = e.Arguments ?? "";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not access launch arguments: {ex.Message}");
                    launchArguments = ""; // Use empty string as fallback
                }

                // Navigate immediately to avoid black screen
                var navigated = rootFrame.Navigate(typeof(Views.MainPage), launchArguments);
                
                if (!navigated)
                {
                    var logger = ServiceProvider.GetService<ILogger<App>>();
                    logger?.LogError("Failed to navigate to MainPage");
                    throw new InvalidOperationException("Navigation to MainPage failed");
                }

                // Initialize services in background AFTER navigation
                _ = Task.Run(async () => await InitializeServicesAsync(launchArguments));

                // Always activate and show the window (since tray icon is disabled)
                window.Activate();
                System.Diagnostics.Debug.WriteLine("✓ Main window activated and should be visible");
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
        /// Handles main window closing - allow window to close but keep background services running
        /// </summary>
        private void OnMainWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Main window closing - background services will continue running");
                
                // Allow the window to close normally
                // Don't set e.Handled = true, let the window close
                
                // Set window reference to null since it's closing
                window = null;
                
                // Background services (reminders, system tray) will continue running
                // The app process will stay alive due to the background keep-alive task
                System.Diagnostics.Debug.WriteLine("✓ Main window closed, background services still active");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during window close: {ex.Message}");
                // Don't prevent closing even if there's an error
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
                await InitializeBackgroundServicesAsync(cts.Token);
                
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
                errorReportingService = ServiceProvider.GetRequiredService<IErrorReportingService>();
                
                // Initialize DataService with timeout
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

                await InitializeServiceWithGracefulDegradation(
                    async () =>
                    {
                        var systemTrayService = ServiceProvider.GetRequiredService<ISystemTrayService>();
                        await systemTrayService.InitializeAsync();
                        System.Diagnostics.Debug.WriteLine("✓ SystemTrayService initialized");
                    },
                    "SystemTrayService",
                    errorReportingService,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                errorReportingService?.ReportErrorAsync(ex, "Critical service initialization failure", false);
                System.Diagnostics.Debug.WriteLine($"❌ Critical error in background service initialization: {ex.Message}");
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
                cancellationToken.ThrowIfCancellationRequested();
                await serviceInitializer();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ {serviceName} initialization was cancelled");
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ {serviceName} initialization failed: {ex.Message}");
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
