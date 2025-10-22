using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using GameClient.GameServiceReference; 
using System.ServiceModel;
using System.Net.Mail;

namespace GameClient.Views
{
    public partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        // es un detector de movimiento para el texto
        private void OnGenericTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
    
            // obtenemos el TextBlock (placeholder) que enlazamos en el 'Tag'
            var placeholder = textBox.Tag as TextBlock;

             if (placeholder != null)
            {
                placeholder.Visibility = string.IsNullOrEmpty(textBox.Text) 
                 ? Visibility.Visible 
                 : Visibility.Collapsed;
            }
        }

        // Se dispara en el instante en que el usuario hace click dentro de un control
        private void OnGenericPasswordFocus(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;
            if (placeholder != null)
            {
                      placeholder.Visibility = Visibility.Collapsed;
            }
        }

        // se dispara en el instante en que el usuario hace clic fuera de ese control
        private void OnGenericPasswordLost(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var placeholder = passwordBox.Tag as TextBlock;
            if (placeholder != null)
            {
                if (string.IsNullOrWhiteSpace(passwordBox.Password))
                {
                    placeholder.Visibility = Visibility.Visible;
                }
            }
        }

        // es un detector de movimiento para el texto.
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
        }

        private async void CreateAccount(object sender, RoutedEventArgs e)
        {
            // validar el formulario ANTES de llamar al servidor
            if (!IsFormValid())
            {
                return; // detiene la ejecución si hay errores de validación
            }

            // si es válido, recoger datos
            string email = EmailBox.Text;
            string username = UserBox.Text;
            string password = PasswordBox.Password;

            // llamar al servidor
            GameServiceClient serviceClient = new GameServiceClient();
            try
            {
                bool registerSuccesful = await serviceClient.RegisterUserAsync(username, email, password);

                if (registerSuccesful)
                {
                    MessageBox.Show("Registro casi completo. Revisa tu correo para obtener el código de verificación (Spam).", "Revisa tu Correo");
                    NavigationService.Navigate(new VerifyAccountPage(email));
                }
                else
                {
                    ShowError(EmailBox, "El correo ya está en uso.");
                    ShowError(UserBox, "El nombre de usuario ya está en uso.");
                }
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar al servidor. Asegúrate de que el servidor esté en ejecución.", "Error de Conexión");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ocurrió un error inesperado: " + ex.Message, "Error");
            }
        }
        private bool IsFormValid()
        {
            ClearAllErrors();
            bool isValid = true;

            string email = EmailBox.Text;
            string username = UserBox.Text;
            string password = PasswordBox.Password;
            string repeatPassword = RepeatBox.Password;

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError(EmailBox, "El correo no puede estar vacio");
                isValid = false;
            }
            else if (!IsValidEmail(email))
            {
                ShowError(EmailBox, "El formato del correo no es valido");
                isValid = false;
            }
            if(string.IsNullOrWhiteSpace(username))
            {
                ShowError(UserBox, "El nombre de usuario no puede estar vacio");
                isValid = false;
            }
            if(string.IsNullOrWhiteSpace (password))
            {
                ShowError(PasswordBox, "La contraseña no puede estar vacia");
                isValid = false;
                    
            }
            if (string.IsNullOrWhiteSpace(repeatPassword))
            {
                ShowError(RepeatBox, "Debes repetir la contraseña");
                isValid = false;
            }
            else if (password != repeatPassword)
            {
                ShowError(PasswordBox, "Las contraseñas no coinciden");
                ShowError(RepeatBox, "Las contraseñas no coinciden");
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
            EmailBox.ClearValue(Border.BorderBrushProperty);
            EmailBox.ToolTip = null;

            UserBox.ClearValue(Border.BorderBrushProperty);
            UserBox.ToolTip = null;

            PasswordBox.ClearValue(Border.BorderBrushProperty);
            PasswordBox.ToolTip = null;

            RepeatBox.ClearValue(Border.BorderBrushProperty);
            RepeatBox.ToolTip = null;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }                  
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



        private void GoToLogin(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new LoginPage());
            }
        }

        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons();
            }
        }
    }
}