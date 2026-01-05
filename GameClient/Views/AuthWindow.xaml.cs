using GameClient.GameServiceReference;
using GameClient.Helpers;
using GameClient.Views;
using System;
using System.ServiceModel;
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

        private void LoginButton(object sender, RoutedEventArgs e)
        {
            AuthButtonsPanel.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new LoginPage());
        }

        public void NavigateToRegister()
        {
            AuthButtonsPanel.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new RegisterPage());
        }

        private void RegisterButton(object sender, RoutedEventArgs e)
        {
            AuthButtonsPanel.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new RegisterPage());
        }

        private async void AsGuestButton(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn != null) btn.IsEnabled = false;

                GameServiceClient client = new GameServiceClient();

                GuestLoginResult result = await client.LoginAsGuestAsync();

                if (result.Success)
                {
                    UserSession.GetInstance().SetSession(result.Username, true);

                    MessageBox.Show(result.Message,
                                    GameClient.Resources.Strings.WelcomeTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                    GameMainWindow mainMenu = new GameMainWindow(result.Username);

                    mainMenu.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(result.Message,
                                    GameClient.Resources.Strings.DialogErrorTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }

                client.Close();
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorAuthConnection,
                                GameClient.Resources.Strings.ConnectionErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorAuthTimeout,
                                GameClient.Resources.Strings.TimeoutTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(GameClient.Resources.Strings.ErrorAuthUnexpected, ex.Message),
                                GameClient.Resources.Strings.DialogErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                var btn = sender as Button;
                if (btn != null) btn.IsEnabled = true;
            }
        }

        public void ShowAuthButtons()
        {
            MainFrame.Content = null;
            AuthButtonsPanel.Visibility = Visibility.Visible;
        }
    }
}