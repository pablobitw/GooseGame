using GameClient.Helpers;
using GameClient.UserProfileServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Views
{
    public partial class ChangeUsernameWindow : Window
    {
        private readonly string _userEmail;

        public ChangeUsernameWindow(string email)
        {
            InitializeComponent();
            _userEmail = email;

            Step1_VerifyCode.Visibility = Visibility.Visible;
            Step2_ChangeName.Visibility = Visibility.Visible;
            VerifyCodeButton.Visibility = Visibility.Collapsed;

            this.Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await SendVerificationCode();
        }

        private async Task SendVerificationCode()
        {
            var client = new UserProfileServiceClient();

            try
            {
                bool sent = await client.SendUsernameChangeCodeAsync(_userEmail);

                if (!sent)
                {
                    MessageBox.Show("No se pudo enviar el código. Intenta más tarde.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor tardó demasiado en enviar el código.");
            }
            catch (EndpointNotFoundException)
            {
                SessionManager.ForceLogout("No se pudo conectar con el servidor.");
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Error de comunicación al solicitar el código.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                MessageBox.Show("Ocurrió un error inesperado al enviar el código.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseClient(client);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();
            string newName = NewUsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                ShowError(CodeBorder, CodeErrorLabel, "El código debe tener 6 dígitos.");
                return;
            }

            if (string.IsNullOrEmpty(newName) || newName.Length < 3)
            {
                ShowError(UsernameBorder, UsernameErrorLabel, "El nombre es muy corto (mínimo 3).");
                return;
            }

            SaveButton.IsEnabled = false;
            var client = new UserProfileServiceClient();

            try
            {
                var result = await client.ChangeUsernameAsync(_userEmail, newName, code);

                switch (result)
                {
                    case UsernameChangeResult.Success:
                        MessageBox.Show("¡Nombre cambiado exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close();
                        break;

                    case UsernameChangeResult.UsernameAlreadyExists:
                        ShowError(UsernameBorder, UsernameErrorLabel, "Este nombre ya está en uso.");
                        break;

                    case UsernameChangeResult.LimitReached:
                        ShowError(UsernameBorder, UsernameErrorLabel, "Límite de cambios alcanzado (3/3).");
                        break;

                    case UsernameChangeResult.UserNotFound:
                        SessionManager.ForceLogout("Usuario no encontrado. Sesión inválida.");
                        break;

                    case UsernameChangeResult.FatalError:
                        ShowError(CodeBorder, CodeErrorLabel, "Código incorrecto o error del servidor.");
                        break;

                    default:
                        ShowError(UsernameBorder, UsernameErrorLabel, "Error al actualizar. Intenta más tarde.");
                        break;
                }
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
            }
            catch (EndpointNotFoundException)
            {
                SessionManager.ForceLogout("No se pudo conectar con el servidor.");
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Se perdió la conexión al intentar guardar.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ShowError(UsernameBorder, UsernameErrorLabel, "Ocurrió un error inesperado.");
            }
            finally
            {
                CloseClient(client);
                SaveButton.IsEnabled = true;
            }
        }

        private async void ResendCode_Click(object sender, RoutedEventArgs e)
        {
            await SendVerificationCode();
            MessageBox.Show("Código reenviado a tu correo.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void VerifyCodeButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CloseClient(UserProfileServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened)
                    client.Close();
                else
                    client.Abort();
            }
            catch (Exception)
            {
                client.Abort();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private static void ShowError(Border border, TextBlock label, string message)
        {
            border.BorderBrush = new SolidColorBrush(Colors.Red);
            border.BorderThickness = new Thickness(2);
            label.Text = message;
            label.Visibility = Visibility.Visible;
        }

        private static void ClearError(Border border, TextBlock label)
        {
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            border.BorderThickness = new Thickness(1);
            if (label != null) label.Visibility = Visibility.Collapsed;
        }

        private void OnCodeTextChanged(object sender, TextChangedEventArgs e)
        {
            CodePlaceholder.Visibility = string.IsNullOrEmpty(CodeTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ClearError(CodeBorder, CodeErrorLabel);
        }

        private void OnUsernameTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(NewUsernameTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ClearError(UsernameBorder, UsernameErrorLabel);
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("Por seguridad, el pegado está deshabilitado en este campo.",
                                "Acción bloqueada",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
