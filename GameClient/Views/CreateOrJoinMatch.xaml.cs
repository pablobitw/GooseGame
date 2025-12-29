using GameClient.Helpers;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class CreateOrJoinMatchPage : Page
    {
        private const string ErrorTitle = "Error"; 
        private readonly string _username;

        public CreateOrJoinMatchPage(string username)
        {
            InitializeComponent();
            _username = username;
        }

        private void CreateMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.GetInstance().IsGuest)
            {
                HandleGuestAccess();
                return;
            }

            try
            {
                NavigationService.Navigate(new LobbyPage(_username));
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show($"Tiempo de espera agotado al crear el lobby: {ex.Message}", "Error de Tiempo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Error de comunicación al crear el lobby: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleGuestAccess()
        {
            string message = "Crear partidas solo está disponible para usuarios registrados.\n\n" +
                             "¿Te gustaría crear una cuenta ahora?\n" +
                             "(Se cerrará tu sesión actual)";

            var result = MessageBox.Show(message, "Modo Invitado", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                AuthWindow authWindow = new AuthWindow();
                authWindow.Show();
                authWindow.NavigateToRegister();

                Window.GetWindow(this)?.Close();
            }
        }

        private void JoinMatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new JoinMatchCodePage(_username));
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show($"El servidor no responde: {ex.Message}", ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Error de conexión: {ex.Message}", ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewMatchesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new ListMatchesPage(_username));
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show($"El servidor no responde: {ex.Message}", ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Error de conexión: {ex.Message}", ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                await mainWindow.ShowMainMenu();
            }
        }
    }
}