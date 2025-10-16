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
        private string userEmail; // para guardar el email del usuario

        public VerifyAccountPage(string email)
        {
            InitializeComponent();
            userEmail = email;
        }

        public VerifyAccountPage()
        {
            InitializeComponent();
        }

        private void VerifyButtonClick(object sender, RoutedEventArgs e)
        {
            string codeTyped = CodeTextBox.Text.Trim();

            if (string.IsNullOrEmpty(codeTyped) || codeTyped.Length != 6)
            {
                MessageBox.Show("El código de verificación debe tener 6 dígitos.", "Error de Formato");
                return;
            }

            try
            {
                var client = new GameServiceClient();
                bool verificationResult = client.VerifyAccount(userEmail, codeTyped);
                client.Close();

                if (verificationResult)
                {
                    MessageBox.Show("¡Cuenta verificada exitosamente! Ya puedes iniciar sesión.", "Éxito");
                    NavigationService.Navigate(new LoginPage());
                }
                else
                {
                    MessageBox.Show("El código es incorrecto o la cuenta ya ha sido verificada.", "Verificación Fallida");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al contactar el servidor: " + ex.Message, "Error");
            }
        }

        private void ResendCodeButtonClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funcionalidad para reenviar el código no implementada.", "Información");
        }

        private void BackButtonClick(object sender, RoutedEventArgs e)
        {
            // si el usuario vuelve, lo enviamos al Login para que no se quede atascado
            NavigationService.Navigate(new LoginPage());
        }
    }
}