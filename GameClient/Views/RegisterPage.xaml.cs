using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using GameClient.GameServiceReference; 
using System.ServiceModel;          

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

        // la logica de los botones (conexion al servidor agregada) 
        private void CreateAccount(object sender, RoutedEventArgs e)
        {
            // rcoger datos de la interfaz
            string email = EmailBox.Text;
            string username = UserBox.Text;
            string password = PasswordBox.Password;
            string repeatPassword = RepeatBox.Password;

            //  validaciones en el cliente
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Por favor, completa todos los campos.");
                return;
            }

            if (password != repeatPassword)
            {
                MessageBox.Show("Las contraseñas no coinciden.");
                return;
            }

            //  llamada al servidor
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
                    MessageBox.Show("El nombre de usuario o correo ya está en uso.", "Error de Registro");
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