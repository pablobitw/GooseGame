using GameClient.Views; // Para LoginPage
using System.Net.Mail;  // Para MailAddress
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;  // Para SolidColorBrush
using System.Windows.Navigation;
// (Añade 'using GameClient.GameServiceReference;' cuando vayas a llamar al servidor)

namespace GameClient
{
    public partial class ForgotPassPage : Page
    {
        public ForgotPassPage()
        {
            InitializeComponent();
        }

        private void OnSendButtonClick(object sender, RoutedEventArgs e)
        {

            if (!IsFormValid())
            {
                return; 
            }

            string email = EmailTextBox.Text;

            MessageBox.Show($"Se enviaría un correo de recuperación a: {email}", "Lógica Pendiente");


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