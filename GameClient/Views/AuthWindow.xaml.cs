using GameClient.GameServiceReference;
using GameClient.Helpers;
using GameClient.Views;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading.Tasks;
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

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, icon);
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
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                var client = new GameServiceClient();

                try
                {
                    GuestLoginResult result = await client.LoginAsGuestAsync();

                    if (result.Success)
                    {
                        UserSession.GetInstance().SetSession(result.Username, true);

                        MessageBox.Show(result.Message,
                                        GameClient.Resources.Strings.Auth_Title_Welcome,
                                        MessageBoxButton.OK, MessageBoxImage.Information);

                        GameMainWindow mainMenu = new GameMainWindow(result.Username);
                        mainMenu.Show();
                        this.Close();
                    }
                    else
                    {
                        if (result.Message == "DbError")
                        {
                            ShowTranslatedMessageBox("Auth_Error_Database", "Auth_Title_Error", MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show(result.Message,
                                            GameClient.Resources.Strings.Auth_Title_Error,
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (EndpointNotFoundException)
                {
                    ShowTranslatedMessageBox("Auth_Error_ServerDown", "Auth_Title_Error", MessageBoxImage.Error);
                }
                catch (TimeoutException)
                {
                    ShowTranslatedMessageBox("Auth_Error_Timeout", "Auth_Title_Error", MessageBoxImage.Warning);
                }
                catch (FaultException)
                {
                    ShowTranslatedMessageBox("Auth_Error_Database", "Auth_Title_Error", MessageBoxImage.Error);
                }
                catch (CommunicationException)
                {
                    ShowTranslatedMessageBox("Auth_Error_Communication", "Auth_Title_Error", MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    string generalError = GameClient.Resources.Strings.Auth_Error_General;
                    string title = GameClient.Resources.Strings.Auth_Title_Error;
                    MessageBox.Show($"{generalError}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    CloseServiceClient(client);
                    if (btn != null) btn.IsEnabled = true;
                }
            }
            else
            {
                ShowTranslatedMessageBox("Auth_Error_NoInternet", "Auth_Title_Error", MessageBoxImage.Error);
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private static void CloseServiceClient(GameServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
                else
                {
                    client.Abort();
                }
            }
            catch (Exception)
            {
                client.Abort();
            }
        }

        public void ShowAuthButtons()
        {
            MainFrame.Content = null;
            AuthButtonsPanel.Visibility = Visibility.Visible;
        }
    }
}