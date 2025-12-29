using GameClient.GameServiceReference;
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
                bool sent = await client.SendPasswordChangeCodeAsync(_userEmail);

                if (!sent)
                {
                    MessageBox.Show("No se pudo enviar el código. Verifica tu conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show("El tiempo de espera se ha agotado.", "Error de Tiempo");
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor.", "Error de Conexión");
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de comunicación con el servidor.", "Error de Red");
            }
            finally
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
        }

        private async void VerifyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();

            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                ShowError(CodeBorder, CodeErrorLabel, "El código debe tener 6 dígitos.");
                return;
            }

            VerifyCodeButton.IsEnabled = false;
            var client = new GameServiceClient();

            try
            {
                bool isValid = await client.VerifyRecoveryCodeAsync(_userEmail, code);

                if (isValid)
                {
                    Step1_VerifyCode.Visibility = Visibility.Collapsed;
                    Step2_ChangeName.Visibility = Visibility.Visible;

                    NewUsernameTextBox.Focus();
                }
                else
                {
                    ShowError(CodeBorder, CodeErrorLabel, "Código incorrecto o expirado.");
                }
            }
            catch (TimeoutException)
            {
                ShowError(CodeBorder, CodeErrorLabel, "El servidor tardó en responder.");
            }
            catch (CommunicationException)
            {
                ShowError(CodeBorder, CodeErrorLabel, "Error de conexión al verificar código.");
            }
            finally
            {
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
                else
                {
                    client.Abort();
                }
                VerifyCodeButton.IsEnabled = true;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newName = NewUsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(newName) || newName.Length < 3)
            {
                ShowError(UsernameBorder, UsernameErrorLabel, "El nombre es muy corto (mínimo 3).");
                return;
            }

            SaveButton.IsEnabled = false;
            var client = new UserProfileServiceClient();

            try
            {
                var result = await client.ChangeUsernameAsync(_userEmail, newName);

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

                    case UsernameChangeResult.CooldownActive:
                        ShowError(UsernameBorder, UsernameErrorLabel, "Debes esperar para cambiarlo de nuevo.");
                        break;

                    default:
                        ShowError(UsernameBorder, UsernameErrorLabel, "Error al actualizar. Intenta más tarde.");
                        break;
                }
            }
            catch (TimeoutException)
            {
                ShowError(UsernameBorder, UsernameErrorLabel, "Tiempo de espera agotado.");
            }
            catch (FaultException ex)
            {
                ShowError(UsernameBorder, UsernameErrorLabel, $"Error del servidor: {ex.Message}");
            }
            catch (CommunicationException)
            {
                ShowError(UsernameBorder, UsernameErrorLabel, "Error de red al guardar.");
            }
            finally
            {
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
                else
                {
                    client.Abort();
                }
                SaveButton.IsEnabled = true;
            }
        }

        private async void ResendCode_Click(object sender, RoutedEventArgs e)
        {
            await SendVerificationCode();
            MessageBox.Show("Código reenviado a tu correo.", "Información");
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
    }
}