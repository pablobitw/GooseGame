using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class CreditsPage : Page
    {
        public CreditsPage()
        {
            InitializeComponent();
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                await mainWindow.ShowMainMenu();
            }
            else
            {
                if (NavigationService != null && NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
        }
    }
}