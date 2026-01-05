using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.GameServiceReference;
using System.ServiceModel;
using GameClient.Views;
using System.Windows.Threading;

namespace GameClient
{
    public partial class VerifyAccountPage : Page
    {
        private string userEmail;

        public VerifyAccountPage(string email)
        {
            InitializeComponent();
            userEmail = email;
        }

        public VerifyAccountPage()
        {
            InitializeComponent();
        }

        private async void VerifyButton(object sender, RoutedEventArgs e)
        {
            string codeTyped = CodeTextBox.Text.Trim();

            if (!IsCodeValid(codeTyped))
            {
                MessageBox.Show("El código de verificación debe tener 6 dígitos numéricos.", "Error de Formato");
                return;
            }

            var client = new GameServiceClient();
            bool verificationResult = false;
            bool connectionError = false;

            try
            {
                verificationResult = await client.VerifyAccountAsync(userEmail, codeTyped);
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
                HandleVerificationResult(verificationResult);
            }
        }

        private static bool IsCodeValid(string code)
        {
            return !string.IsNullOrEmpty(code) && code.Length == 6 && int.TryParse(code, out _);
        }

        private static bool HandleConnectionException(Exception ex)
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

            MessageBox.Show("Error al contactar el servidor: " + ex.Message, "Error");
            return true;
        }

        private static void CloseClientSafely(GameServiceClient client)
        {
            if (client.State == CommunicationState.Opened)
            {
                client.Close();
            }
        }

        private void HandleVerificationResult(bool verificationResult)
        {
            if (verificationResult)
            {
                MessageBox.Show("¡Cuenta verificada exitosamente! Ya puedes iniciar sesión.", "Éxito");
                NavigationService.Navigate(new LoginPage());
            }
            else
            {
                MessageBox.Show("El código es incorrecto, ha expirado o la cuenta ya ha sido verificada.", "Verificación Fallida");
            }
        }

        private async void ResendCodeButton(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
            }

            var client = new GameServiceClient();
            bool requestSent = false;
            bool connectionError = false;
            string errorMessage = string.Empty;

            try
            {
                requestSent = await client.ResendVerificationCodeAsync(userEmail);
            }
            catch (EndpointNotFoundException)
            {
                errorMessage = "No se pudo conectar al servidor.";
                connectionError = true;
            }
            catch (TimeoutException)
            {
                errorMessage = "El servidor tardó demasiado en responder.";
                connectionError = true;
            }
            catch (CommunicationException)
            {
                errorMessage = "Error de comunicación con el servidor.";
                connectionError = true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error inesperado: {ex.Message}";
                connectionError = true;
            }
            finally
            {
                CloseClientSafely(client);
                if (button != null)
                {
                    button.IsEnabled = true;
                }
            }

            if (connectionError)
            {
                MessageBox.Show(errorMessage, "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (requestSent)
            {
                MessageBox.Show($"Se ha enviado un nuevo código a {userEmail}. Tienes 15 minutos para usarlo.", "Código Reenviado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No se pudo reenviar el código. Verifica que la cuenta exista y esté pendiente.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BackButton(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new LoginPage());
        }

        private void OnCodeTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                var raw = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
                e.CancelCommand();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var sanitized = new string(raw.Where(char.IsDigit).ToArray());
                    if (sanitized.Length > CodeTextBox.MaxLength)
                    {
                        sanitized = sanitized.Substring(0, CodeTextBox.MaxLength);
                    }

                    CodeTextBox.Text = sanitized;
                    CodeTextBox.CaretIndex = CodeTextBox.Text.Length;
                }), DispatcherPriority.Background);
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}