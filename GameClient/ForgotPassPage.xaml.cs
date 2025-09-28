using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient
{
    public partial class ForgotPassPage : Page
    {
        public ForgotPassPage()
        {
            InitializeComponent();
        }

        private void BackButton(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void OnSendButtonClick(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;
            string repeatEmail = RepeatEmailTextBox.Text;

            if (email != repeatEmail)
            {
                MessageBox.Show("Los correos electrónicos no coinciden.", "Error");
                return;
            }

            // TODO: Lógica para llamar al servidor y recuperar la contraseña
            MessageBox.Show($"Se enviaría un correo de recuperación a: {email}", "Lógica Pendiente");
        }
    }
}