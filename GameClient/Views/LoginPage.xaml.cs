using GameClient.GameServiceReference;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void Login(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid())
            {
                return;
            }

            string usernameOrEmail = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            bool loginSuccessful = await AttemptLoginAsync(usernameOrEmail, password);

            if (loginSuccessful)
            {
                GameMainWindow gameMenu = new GameMainWindow(usernameOrEmail);
                gameMenu.Show();
                Window.GetWindow(this).Close();
            }
            else
            {
                string errorMsg = GameClient.Resources.Strings.LoginErrorLabel;
                ShowError(UsernameBorder, errorMsg);
                ShowError(PasswordBorder, errorMsg);
            }
        }

        private async System.Threading.Tasks.Task<bool> AttemptLoginAsync(string username, string password)
        {
            var serviceClient = new GameServiceClient();
            bool isSuccess = false;

            try
            {
                isSuccess = await serviceClient.LogInAsync(username, password);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, GameClient.Resources.Strings.ErrorTitle);
            }
            finally
            {
                if (serviceClient.State == CommunicationState.Opened)
                {
                    serviceClient.Close();
                }
            }

            return isSuccess;
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowError(UsernameBorder, GameClient.Resources.Strings.EmptyUsernameLabel);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowError(PasswordBorder, GameClient.Resources.Strings.EmptyPasswordLabel);
                isValid = false;
            }

            return isValid;
        }

        private void ShowError(Border field, string errorMessage)
        {
            field.BorderBrush = new SolidColorBrush(Colors.Red);
            field.ToolTip = new ToolTip { Content = errorMessage };
        }

        private void ClearAllErrors()
        {
            UsernameBorder.ClearValue(Border.BorderBrushProperty);
            UsernameBorder.ToolTip = null;
            PasswordBorder.ClearValue(Border.BorderBrushProperty);
            PasswordBorder.ToolTip = null;
        }

        private void ForgotPass(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new ForgotPassPage());
            }
        }

        private void OnUsernameTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            UsernameBorder.ClearValue(Border.BorderBrushProperty);
            UsernameBorder.ToolTip = null;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;

            PasswordBorder.ClearValue(Border.BorderBrushProperty);
            PasswordBorder.ToolTip = null;
        }

        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons();
            }
        }
    }
}