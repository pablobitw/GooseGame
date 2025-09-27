using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Views
{
    public partial class LoginPage : Page
    {
        private readonly string _usernamePlaceholder;
        private readonly string _passwordPlaceholder;

        public LoginPage()
        {
            InitializeComponent();

            _usernamePlaceholder = GameClient.Resources.Strings.UsernameLabel;
            _passwordPlaceholder = GameClient.Resources.Strings.PasswordLabel;

            SetupPlaceholders();
        }

        private void SetupPlaceholders()
        {
            UsernameTextBox.Text = _usernamePlaceholder;
            UsernameTextBox.Foreground = Brushes.Gray;

            PasswordBox.Password = _passwordPlaceholder;
            PasswordBox.Foreground = Brushes.Gray;
        }

        private void BtnLogin(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Botón de Login presionado!");
        }

        private void BtnForgotPass(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new ForgotPassPage());
            
        }
        private void RemoveUsernamePlaceholder(object sender, RoutedEventArgs e)
        {
            if (UsernameTextBox.Text == _usernamePlaceholder)
            {
                UsernameTextBox.Text = "";
                UsernameTextBox.Foreground = Brushes.Black;
            }
        }

        private void RestoreUsernamePlaceholder(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernameTextBox.Text = _usernamePlaceholder;
                UsernameTextBox.Foreground = Brushes.Gray;
            }
        }

        private void RemovePasswordPlaceholder(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password == _passwordPlaceholder)
            {
                PasswordBox.Password = "";
                PasswordBox.Foreground = Brushes.Black;
            }
        }

        private void RestorePasswordPlaceholder(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                PasswordBox.Password = _passwordPlaceholder;
                PasswordBox.Foreground = Brushes.Gray;
            }
        }
    }
}