using GameClient.Views;
using System.Windows;
using System.Windows.Controls;

namespace GameClient
{
    public partial class AuthWindow : Window
    {
        public AuthWindow()
        {
            InitializeComponent();
        }

        private void LoginButton (object sender, RoutedEventArgs e)
        {
            MainPanel.Visibility = Visibility.Collapsed;

            MainFrame.Navigate(new LoginPage());
        }

        private void RegisterButton (object sender, RoutedEventArgs e)
        {
            AuthButtonsPanel.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new RegisterPage());
        }

        private void AsGuestButton (object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Iniciando como invitado...");
        }

        public void ShowAuthButtons()
        {
            MainFrame.Content = null; 
            AuthButtonsPanel.Visibility = Visibility.Visible;
        }
    }
}