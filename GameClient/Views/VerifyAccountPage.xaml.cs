using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.GameServiceReference;
using System.ServiceModel;
using GameClient.Views;

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

            if (string.IsNullOrEmpty(codeTyped) || codeTyped.Length != 6 || !int.TryParse(codeTyped, out _))
            {
                MessageBox.Show("El código de verificación debe tener 6 dígitos numéricos.", "Error de Formato");
            }
            else
            {
                var client = new GameServiceClient();
                bool verificationResult = false;
                bool connectionError = false;

                try
                {
                    verificationResult = await client.VerifyAccountAsync(userEmail, codeTyped);
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
                    MessageBox.Show("Error al contactar el servidor: " + ex.Message, "Error");
                    connectionError = true;
                }
                finally
                {
                    if (client.State == CommunicationState.Opened)
                    {
                        client.Close();
                    }
                }

                if (!connectionError)
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
            }
        }

        private void ResendCodeButton(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funcionalidad para reenviar el código no implementada.", "Información");
        }

        private void BackButton(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new LoginPage());
        }
    }
}