using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        // Guardamos los textos de placeholder para compararlos
        private readonly string _usernamePlaceholder;
        private readonly string _passwordPlaceholder;

        public LoginPage()
        {
            InitializeComponent();

            // Obtenemos los textos desde tu archivo de recursos
            _usernamePlaceholder = GameClient.Resources.Strings.UsernameLabel;
            _passwordPlaceholder = GameClient.Resources.Strings.PasswordLabel;

            SetupPlaceholders();
        }

        private void SetupPlaceholders()
        {
            // Placeholder para el nombre de usuario
            UsernameTextBox.Text = _usernamePlaceholder;
            UsernameTextBox.Foreground = Brushes.Gray;

            // Placeholder para la contraseña
            PasswordBox.Password = _passwordPlaceholder;
            PasswordBox.Foreground = Brushes.Gray;
        }

        private void BtnLogin(object sender, RoutedEventArgs e)
        {
            // Aquí va tu lógica de inicio de sesión
            MessageBox.Show("Botón de Login presionado!");
        }

        private void BtnForgotPass(object sender, RoutedEventArgs e)
        {
            // Lógica para el botón de olvidar contraseña
            MessageBox.Show("Botón de Olvidé Contraseña presionado!");
        }

        // --- LÓGICA DE PLACEHOLDERS ---

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UsernameTextBox.Text == _usernamePlaceholder)
            {
                UsernameTextBox.Text = "";
                UsernameTextBox.Foreground = Brushes.Black;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernameTextBox.Text = _usernamePlaceholder;
                UsernameTextBox.Foreground = Brushes.Gray;
            }
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password == _passwordPlaceholder)
            {
                PasswordBox.Password = "";
                PasswordBox.Foreground = Brushes.Black;
            }
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                PasswordBox.Password = _passwordPlaceholder;
                PasswordBox.Foreground = Brushes.Gray;
            }
        }
    }
}