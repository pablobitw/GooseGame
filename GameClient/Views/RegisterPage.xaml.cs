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

        //  metodos de Placeholders 
        private void EmailBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UserBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UserBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PassBoxFocus(object sender, RoutedEventArgs e)
        {
            PassPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PassBoxLost(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                PassPlaceholder.Visibility = Visibility.Visible;
        }

        private void PassBoxChanged(object sender, RoutedEventArgs e)
        {
            PassPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RepeatBoxFocus(object sender, RoutedEventArgs e)
        {
            RepeatPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void RepeatBoxLost(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RepeatBox.Password))
                RepeatPlaceholder.Visibility = Visibility.Visible;
        }

        private void RepeatBoxChanged(object sender, RoutedEventArgs e)
        {
            RepeatPlaceholder.Visibility = string.IsNullOrEmpty(RepeatBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CreateAccount(object sender, RoutedEventArgs e)
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
                bool registerSuccesful = serviceClient.RegisterUser(username, email, password);

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
                        if (NavigationService.CanGoBack)
                            NavigationService.GoBack();
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