using Microsoft.UI.Xaml.Controls;
using YAWDA.ViewModels;
using YAWDA.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System;

namespace YAWDA.Views
{
    /// <summary>
    /// Main page for daily water intake tracking and logging
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPageViewModel ViewModel { get; private set; }

        public MainPage()
        {
            System.Diagnostics.Debug.WriteLine("MainPage constructor starting...");
            
            // Initialize ViewModel first before InitializeComponent for x:Bind
            try
            {
                // Get ViewModel from DI container
                var serviceProvider = ((App)App.Current).ServiceProvider;
                ViewModel = serviceProvider.GetRequiredService<MainPageViewModel>();
                
                System.Diagnostics.Debug.WriteLine("ViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainPage constructor error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                
                // Log detailed error before throwing
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainPage construction failed: {ex}";
                try
                {
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YAWDA", "mainpage_error.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    File.AppendAllText(logPath, logMessage + Environment.NewLine);
                }
                catch { /* Ignore file write errors */ }
                
                // Create a fallback ViewModel - use a simple approach to avoid dependency issues
                // For now, we'll throw the exception to force proper service registration
                throw new InvalidOperationException("Failed to initialize MainPage ViewModel from DI container", ex);
            }
            
            // Now initialize the component with ViewModel available
            this.InitializeComponent();
            
            // Set DataContext after InitializeComponent
            this.DataContext = ViewModel;
            
            System.Diagnostics.Debug.WriteLine("MainPage constructor completed successfully");
        }

        private void OnSettingsClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Navigate to settings page
            Frame.Navigate(typeof(SettingsPage));
        }

        private void OnStatsClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Navigate to statistics page
            // For now, we'll show a content dialog as a placeholder
            ShowStatsDialog();
        }



        private async void ShowStatsDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Statistics",
                Content = "Statistics page will be implemented in the next steps.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            
            await dialog.ShowAsync();
        }
    }
}
