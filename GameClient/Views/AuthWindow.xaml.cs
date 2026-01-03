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

                    MessageBox.Show(result.Message, "Bienvenido", MessageBoxButton.OK, MessageBoxImage.Information);

                    GameMainWindow mainMenu = new GameMainWindow(result.Username);

                    mainMenu.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                client.Close();
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("El servidor tardó demasiado en responder.", "Tiempo de espera", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ocurrió un error inesperado: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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