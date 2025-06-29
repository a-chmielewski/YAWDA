using Microsoft.UI.Xaml.Controls;
using YAWDA.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace YAWDA.Views
{
    /// <summary>
    /// Settings page for configuring water reminder preferences
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; private set; }

        public SettingsPage()
        {
            this.InitializeComponent();
            
            // Get ViewModel from DI container
            var serviceProvider = ((App)App.Current).ServiceProvider;
            ViewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
            
            // Set DataContext
            this.DataContext = ViewModel;
        }

        private void OnBackToMainClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Navigate back to main page
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(MainPage));
            }
        }
    }
} 