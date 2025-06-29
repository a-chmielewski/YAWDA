using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Navigation;
using YAWDA.Services;
using YAWDA.Views;
using YAWDA.ViewModels;
using YAWDA.Utilities;

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
            InitializeComponent();
            ConfigureServices();
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
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Initialize startup service and check for startup mode
            var startupService = ServiceProvider.GetRequiredService<IStartupService>();
            startupService.InitializeStartupMode(e.Arguments);

            // Get user settings to determine window behavior
            var dataService = ServiceProvider.GetRequiredService<IDataService>();
            var settings = await dataService.LoadSettingsAsync();

            // Handle startup mode (background-only operation)
            if (startupService.IsStartupMode && settings.StartMinimized)
            {
                await InitializeBackgroundServicesAsync();
                
                // Start system tray without showing main window
                var systemTrayService = ServiceProvider.GetRequiredService<ISystemTrayService>();
                await systemTrayService.InitializeAsync();
                
                // Validate and fix startup configuration if needed
                _ = Task.Run(async () => await startupService.ValidateStartupConfigurationAsync());
                
                return; // Don't create main window for startup mode
            }

            // Normal launch - create main window
            window ??= new Window();

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            _ = rootFrame.Navigate(typeof(Views.MainPage), e.Arguments);
            
            // Show window unless starting minimized
            if (!settings.StartMinimized || !startupService.IsStartupMode)
            {
                window.Activate();
            }

            // Initialize background services
            await InitializeBackgroundServicesAsync();
        }

        /// <summary>
        /// Initializes background services required for operation
        /// </summary>
        private async Task InitializeBackgroundServicesAsync()
        {
            IErrorReportingService? errorReportingService = null;
            
            try
            {
                errorReportingService = ServiceProvider.GetRequiredService<IErrorReportingService>();
                
                // Initialize core services with graceful degradation
                await InitializeServiceWithGracefulDegradation(
                    async () =>
                    {
                        var reminderService = ServiceProvider.GetRequiredService<IReminderService>();
                        await reminderService.StartAsync();
                    },
                    "ReminderService",
                    errorReportingService
                );

                await InitializeServiceWithGracefulDegradation(
                    async () =>
                    {
                        var systemTrayService = ServiceProvider.GetRequiredService<ISystemTrayService>();
                        await systemTrayService.InitializeAsync();
                    },
                    "SystemTrayService",
                    errorReportingService
                );
            }
            catch (Exception ex)
            {
                // Report critical initialization error
                if (errorReportingService != null)
                {
                    await errorReportingService.ReportCriticalErrorAsync(
                        new InitializationException("Failed to initialize background services", ex, "INIT_001"),
                        false
                    );
                }
                else
                {
                    // Fallback logging if error reporting service is not available
                    var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                    logger.LogCritical(ex, "Critical: Failed to initialize background services and error reporting is unavailable");
                }
            }
        }

        /// <summary>
        /// Initializes a service with graceful degradation on failure
        /// </summary>
        private async Task InitializeServiceWithGracefulDegradation(
            Func<Task> serviceInitializer, 
            string serviceName, 
            IErrorReportingService errorReportingService)
        {
            try
            {
                await serviceInitializer();
            }
            catch (Exception ex)
            {
                // Report as recoverable error - app can continue without this service
                var serviceException = new SystemIntegrationException(
                    $"Failed to initialize {serviceName}",
                    ex,
                    $"SERVICE_INIT_{serviceName.ToUpper()}"
                );

                await errorReportingService.ReportRecoverableErrorAsync(serviceException, new List<string>
                {
                    $"Restart the application to retry {serviceName} initialization",
                    "Some features may be limited without this service",
                    "Check system resources and permissions"
                });
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
                _ = Task.Run(async () => await errorReportingService.ReportErrorAsync(navigationException, "Navigation"));
            }
            catch
            {
                // Fallback to direct exception if error reporting fails
                throw navigationException;
            }
        }
    }
}
