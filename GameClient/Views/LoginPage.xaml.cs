using GameClient.GameServiceReference;
using GameClient.Views;
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
            if (IsFormValid())
            {
                string username = UsernameTextBox.Text;
                string password = PasswordBox.Password;

                GameServiceClient serviceClient = new GameServiceClient();
                bool loginSuccessful = false;
                bool connectionError = false;

                try
                {
                    loginSuccessful = await serviceClient.LogInAsync(username, password);
                }
                catch (EndpointNotFoundException)
                {
                    MessageBox.Show("No se pudo conectar al servidor. Asegúrate de que el servidor esté en ejecución.", "Error de Conexión");
                    connectionError = true;
                }
                catch (TimeoutException)
                {
                    MessageBox.Show("La solicitud tardó demasiado en responder. Revisa tu conexión.", "Error de Red");
                    connectionError = true;
                }
                catch (CommunicationException)
                {
                    MessageBox.Show("Error de comunicación con el servidor. Revisa tu conexión.", "Error de Red");
                    connectionError = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ocurrió un error inesperado: " + ex.Message, "Error");
                    connectionError = true;
                }
                finally
                {
                    if (serviceClient.State == CommunicationState.Opened)
                    {
                        serviceClient.Close();
                    }
                }

                if (!connectionError)
                {
                    if (loginSuccessful)
                    {
                        GameMainWindow gameMenu = new GameMainWindow(username);
                        gameMenu.Show();
                        Window.GetWindow(this).Close();
                    }
                    else
                    {
                        ShowError(UsernameTextBox, "Usuario o contraseña incorrectos.");
                        ShowError(PasswordBox, "Usuario o contraseña incorrectos.");
                    }
                }
            }
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowError(UsernameTextBox, "El nombre de usuario no puede estar vacío.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowError(PasswordBox, "La contraseña no puede estar vacía.");
                isValid = false;
            }

            return isValid;
        }

        private void ShowError(Control field, string errorMessage)
        {
            field.BorderBrush = new SolidColorBrush(Colors.Red);
            field.ToolTip = new ToolTip { Content = errorMessage };
        }

        private void ClearAllErrors()
        {
            UsernameTextBox.ClearValue(Border.BorderBrushProperty);
            UsernameTextBox.ToolTip = null;
            PasswordBox.ClearValue(Border.BorderBrushProperty);
            PasswordBox.ToolTip = null;
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
                authWindow.ShowAuthButtons();
            }
        }
    }
}