using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameClient.UserProfileServiceReference;
using GameClient.GameServiceReference; 

namespace GameClient.Views
{
    public partial class ChangeUsernameWindow : Window
    {
        private readonly string _userEmail;

        public ChangeUsernameWindow(string email)
        {
            InitializeComponent();
            _userEmail = email;

            SendVerificationCode();
        }

        private async void SendVerificationCode()
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar: {ex.Message}");
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
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
            catch (Exception)
            {
                ShowError(CodeBorder, CodeErrorLabel, "Error de conexión.");
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
                SaveButton.IsEnabled = true;
            }
        }

        private void ResendCode_Click(object sender, RoutedEventArgs e)
        {
            SendVerificationCode();
            MessageBox.Show("Código reenviado a tu correo.", "Información");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowError(Border border, TextBlock label, string message)
        {
            border.BorderBrush = new SolidColorBrush(Colors.Red);
            border.BorderThickness = new Thickness(2);
            label.Text = message;
            label.Visibility = Visibility.Visible;
        }

        private void ClearError(Border border, TextBlock label)
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