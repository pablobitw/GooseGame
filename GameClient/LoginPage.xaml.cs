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
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            if (username == "admin" && password == "1234")
            {
                // Se crea una instancia de la nueva ventana del juego
                GameMainWindow gameMenu = new GameMainWindow();

                //  Se muestra la nueva ventana
                gameMenu.Show();

                // Busca la ventana de autenticación actual y ciérrala
                Window authWindow = Window.GetWindow(this);
                authWindow.Close();
            }
            else
            {
                // Si las credenciales son incorrectas, muestra un error
                MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Acceso");
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
                authWindow.ShowAuthButtons(); // esto limpia el Frame y muestra los botones
            }
        }
    }
}
