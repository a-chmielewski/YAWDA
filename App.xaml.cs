using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Navigation;
using YAWDA.Services;
using YAWDA.Views;
using YAWDA.ViewModels;

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

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Register service interfaces - implementations will be added in later steps
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
            try
            {
                // Initialize core services
                var reminderService = ServiceProvider.GetRequiredService<IReminderService>();
                var systemTrayService = ServiceProvider.GetRequiredService<ISystemTrayService>();

                // Start reminder service
                await reminderService.StartAsync();

                // Initialize system tray
                await systemTrayService.InitializeAsync();
            }
            catch (Exception ex)
            {
                // Log error but continue - app should still function
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Error initializing background services");
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
