using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Views
{
    public partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        // --- Email ---
        private void EmailBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // --- Username ---
        private void UserBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UserBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // --- Password ---
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
            PassPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // --- Repeat Password ---
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
            RepeatPlaceholder.Visibility = string.IsNullOrEmpty(RepeatBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // --- Botones ---
        private void CreateAccount(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Cuenta creada (ejemplo).");
        }

        private void GoToLogin(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void OnBackButton(object sender, RoutedEventArgs e)
        {
            // Llamamos al método de la ventana padre
            if (Window.GetWindow(this) is AuthWindow authWindow)
            {
                authWindow.ShowAuthButtons();
            }
        }

    }
}
