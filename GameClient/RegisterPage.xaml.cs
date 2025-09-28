using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            InitializeComponent();

            // Inicializar placeholders
            PasswordPlaceholder.Visibility = Visibility.Visible;
            RepeatPasswordPlaceholder.Visibility = Visibility.Visible;

            EmailTextBox.Text = "Correo electrónico";
            UsernameTextBox.Text = "Usuario";
        }

        private void OnCreateAccount(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;
            string repeatPassword = RepeatPasswordBox.Password;

            if (password != repeatPassword)
            {
                MessageBox.Show("Las contraseñas no coinciden.", "Error de Validación");
                return;
            }

            MessageBox.Show("Lógica de registro aquí...", "TODO");
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnRepeatPasswordChanged(object sender, RoutedEventArgs e)
        {
            RepeatPasswordPlaceholder.Visibility = string.IsNullOrEmpty(RepeatPasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void OnGoToLogin(object sender, RoutedEventArgs e)
        {
            // NavigationService.Navigate(new LoginPage());
        }

        // Para los TextBox: placeholder de Email y Username
        private void RemoveText(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && (tb.Text == "Correo electrónico" || tb.Text == "Usuario"))
            {
                tb.Text = "";
                tb.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void AddText(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
            {
                if (tb.Name == "EmailTextBox")
                {
                    tb.Text = "Correo electrónico";
                }
                else if (tb.Name == "UsernameTextBox")
                {
                    tb.Text = "Usuario";
                }
                tb.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
    }
}
