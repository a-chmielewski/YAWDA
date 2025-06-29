using Microsoft.UI.Xaml.Controls;
using YAWDA.ViewModels;
using YAWDA.Services;
using Microsoft.Extensions.DependencyInjection;

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
            this.InitializeComponent();
            
            // Get ViewModel from DI container
            var serviceProvider = ((App)App.Current).ServiceProvider;
            ViewModel = serviceProvider.GetRequiredService<MainPageViewModel>();
            
            // Set DataContext
            this.DataContext = ViewModel;
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
