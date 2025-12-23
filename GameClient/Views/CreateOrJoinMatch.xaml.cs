using GameClient.Views;
using GameClient.Helpers; // [IMPORTANTE] Necesario para UserSession
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameClient.Views
{
    public partial class CreateOrJoinMatchPage : Page
    {
        private string _username;

        public CreateOrJoinMatchPage(string username)
        {
            InitializeComponent();
            _username = username;
        }

        private void CreateMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.GetInstance().IsGuest)
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

                return;
            }

            NavigationService.Navigate(new LobbyPage(_username));
        }

        private void JoinMatchButton_Click(object sender, RoutedEventArgs e)
        {
           
            NavigationService.Navigate(new JoinMatchCodePage(_username));
        }

        private void ViewMatchesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ListMatchesPage(_username));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }
        }
    }
}