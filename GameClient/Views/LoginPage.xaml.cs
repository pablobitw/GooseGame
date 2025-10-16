using GameClient.GameServiceReference;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private void Login(object sender, RoutedEventArgs e)
        {
            // recoger datos
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            // validar que los campos no estén vacíos
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Por favor, ingresa tu usuario y contraseña.", "Campos Vacíos");
                return;
            }

            //llamar al servidor para iniciar sesión
            GameServiceClient serviceClient = new GameServiceClient();
            try
            {
                bool loginExitoso = serviceClient.LogIn(username, password);

                if (loginExitoso)
                {
                    // si el login es exitoso, se abre la ventana principal del juego
                    GameMainWindow gameMenu = new GameMainWindow();
                    gameMenu.Show();

                    // y cierra la ventana de autenticación actual
                    Window.GetWindow(this).Close();
                }
                else
                {
                    MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Acceso");
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

        private void ForgotPass(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(new ForgotPassPage());
            }
        }

        private void OnUsernameTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            // Llamamos al método de la ventana padre
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons(); // esto limpia el frame y muestra los botones
            }
        }
    }
}
