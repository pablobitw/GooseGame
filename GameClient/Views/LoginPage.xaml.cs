using GameClient.GameServiceReference;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private void Login(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.", "Empty Fields");
                return;
            }

            GameServiceClient serviceClient = new GameServiceClient();
            try
            {
                bool loginSuccessful = serviceClient.LogIn(username, password);

                if (loginSuccessful)
                {
                    GameMainWindow gameMenu = new GameMainWindow(username);
                    gameMenu.Show();

                    Window.GetWindow(this).Close();
                }
                else
                {
                    MessageBox.Show("Invalid username or password.", "Login Failed");
                }
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("Could not connect to the server. Please ensure the server is running.", "Connection Error");
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unexpected error occurred: " + ex.Message, "Error");
            }
        }

        private void ForgotPass(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(new ForgotPassPage());
            }
        }

        private void OnUsernameTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons(); // esto limpia el frame y muestra los botones
            }
        }
    }
}
