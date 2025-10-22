using GameClient.GameServiceReference;
using GameClient.Views; 
using System;
using System.Net.Mail;  
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;  
using System.Windows.Navigation;

namespace GameClient
{
    public partial class ForgotPassPage : Page
    {
        public ForgotPassPage()
        {
            InitializeComponent();
        }


        private async void OnSendButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsFormValid())
            {
                return; // detiene si hay errores
            }
            string email = EmailTextBox.Text;

            // llamar al servidor 
            var client = new GameServiceClient();
            bool requestSent = false;
            try
            {

                requestSent = await client.RequestPasswordResetAsync(email);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar al servidor. Asegúrate de que el servidor esté en ejecución.", "Error de Conexión");
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conectando al servidor: " + ex.Message, "Error");
                return;
            }
            finally
            {
                // siempre cierra el cliente 
                if (client.State == System.ServiceModel.CommunicationState.Opened)
                {
                    client.Close();
                }
            }

            // analizar la respuesta del servidor
            if (requestSent)
            {
                // el servidor envió el correo (o fingió hacerlo)
                MessageBox.Show("Si tu correo está registrado, recibirás un código de verificación.", "Revisa tu Correo");


                NavigationService.Navigate(new VerifyRecoveryCodePage(email));
            }
            else
            {
                // El servidor devolvió 'false' (un error inesperado en el servidor)
                ShowError(EmailTextBox, "Hubo un error al procesar tu solicitud. Intenta más tarde.");
            }
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new LoginPage());
            }
        }


        private void OnGenericTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var placeholder = textBox.Tag as TextBlock;
            if (placeholder != null)
            {
                placeholder.Visibility = string.IsNullOrEmpty(textBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }


        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            string email = EmailTextBox.Text;
            string repeatEmail = RepeatEmailTextBox.Text;

            // 1. Validar Email
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError(EmailTextBox, "El correo no puede estar vacío.");
                isValid = false;
            }
            else if (!IsValidEmail(email))
            {
                ShowError(EmailTextBox, "El formato del correo no es válido.");
                isValid = false;
            }

            
            if (string.IsNullOrWhiteSpace(repeatEmail))
            {
                ShowError(RepeatEmailTextBox, "Debes repetir tu correo.");
                isValid = false;
            }
            else if (email != repeatEmail)
            {
                ShowError(EmailTextBox, "Los correos no coinciden.");
                ShowError(RepeatEmailTextBox, "Los correos no coinciden.");
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
            EmailTextBox.ClearValue(Border.BorderBrushProperty);
            EmailTextBox.ToolTip = null;

            RepeatEmailTextBox.ClearValue(Border.BorderBrushProperty);
            RepeatEmailTextBox.ToolTip = null;
        }
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}