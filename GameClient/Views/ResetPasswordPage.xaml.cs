using GameClient.GameServiceReference;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class ResetPasswordPage : Page
    {
        private string _username;

        public ResetPasswordPage(string username)
        {
            InitializeComponent();
            this._username = username;
        }

        public ResetPasswordPage() : this(string.Empty)
        {
        }

        private async void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid()) return;

            string currentPassword = CurrentPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;

            var client = new GameServiceClient();
            bool updateSuccess = false;
            bool connectionError = false;

            try
            {
                updateSuccess = await client.ChangeUserPasswordAsync(_username, currentPassword, newPassword);
            }
            catch (Exception ex)
            {
                connectionError = HandleConnectionException(ex);
            }
            finally
            {
                CloseClientSafely(client);
            }

            if (!connectionError)
            {
                HandleUpdateResult(updateSuccess);
            }
        }

        private bool HandleConnectionException(Exception ex)
        {
            if (ex is EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar al servidor. Asegúrate de que el servidor esté en ejecución.", "Error de Conexión");
                return true;
            }
            if (ex is TimeoutException)
            {
                MessageBox.Show("La solicitud tardó demasiado en responder. Revisa tu conexión.", "Error de Red");
                return true;
            }
            if (ex is CommunicationException)
            {
                MessageBox.Show("Error de comunicación con el servidor. Revisa tu conexión.", "Error de Red");
                return true;
            }

            MessageBox.Show("Ocurrió un error inesperado: " + ex.Message, "Error");
            return true;
        }

        private void CloseClientSafely(GameServiceClient client)
        {
            if (client.State == CommunicationState.Opened)
            {
                client.Close();
            }
        }

        private void HandleUpdateResult(bool updateSuccess)
        {
            if (updateSuccess)
            {
                MessageBox.Show("¡Contraseña actualizada con éxito!", "Éxito");
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
            else
            {
                ShowError(CurrentPasswordBox, "La contraseña actual es incorrecta o hubo un error.");
            }
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            string currentPass = CurrentPasswordBox.Password;
            string newPass = NewPasswordBox.Password;
            string repeatPass = RepeatNewPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPass))
            {
                ShowError(CurrentPasswordBox, "Debes ingresar tu contraseña actual.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(newPass))
            {
                ShowError(NewPasswordBox, "La contraseña nueva no puede estar vacía.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(repeatPass))
            {
                ShowError(RepeatNewPasswordBox, "Debes repetir la contraseña nueva.");
                isValid = false;
            }
            else if (newPass != repeatPass)
            {
                ShowError(NewPasswordBox, "Las contraseñas nuevas no coinciden.");
                ShowError(RepeatNewPasswordBox, "Las contraseñas nuevas no coinciden.");
                isValid = false;
            }

            if (isValid && currentPass == newPass)
            {
                ShowError(NewPasswordBox, "La nueva contraseña no puede ser igual a la actual.");
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
            CurrentPasswordBox.ClearValue(Border.BorderBrushProperty);
            CurrentPasswordBox.ToolTip = null;

            NewPasswordBox.ClearValue(Border.BorderBrushProperty);
            NewPasswordBox.ToolTip = null;

            RepeatNewPasswordBox.ClearValue(Border.BorderBrushProperty);
            RepeatNewPasswordBox.ToolTip = null;
        }

        private void OnGenericPasswordFocus(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;

            if (placeholder != null)
            {
                placeholder.Visibility = Visibility.Collapsed;
            }
        }

        private void OnGenericPasswordLost(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;

            if (placeholder != null && string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                placeholder.Visibility = Visibility.Visible;
            }
        }

        private void OnPasswordBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("Por seguridad, el pegado está deshabilitado en campos de contraseña.",
                                "Acción bloqueada",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }


        private void OnGenericPasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;

            if (placeholder != null)
            {
                placeholder.Visibility = string.IsNullOrEmpty(passwordBox.Password)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            passwordBox.ClearValue(Border.BorderBrushProperty);
            passwordBox.ToolTip = null;
        }
    }
}