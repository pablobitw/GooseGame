using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.LobbyServiceReference;
using System.ServiceModel;

namespace GameClient.Views
{
    public partial class JoinMatchCodePage : Page
    {
        private string _username;
        private LobbyServiceClient _lobbyClient;

        public JoinMatchCodePage(string username)
        {
            InitializeComponent();
            _username = username;
            _lobbyClient = new LobbyServiceClient();
        }

        private async void JoinButton_Click(object sender, RoutedEventArgs e)
        {
            string code = LobbyCodeBox.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(code) || code.Length != 5)
            {
                MessageBox.Show("Por favor, ingresa un código válido de 5 letras.", "Código Inválido");
                return;
            }

            JoinButton.IsEnabled = false;

            try
            {
                var result = await _lobbyClient.JoinLobbyAsync(code, _username);

                if (result.Success)
                {
                    NavigationService.Navigate(new LobbyPage(_username, code, result));
                }
                else
                {
                    MessageBox.Show(result.ErrorMessage, "Error al unirse");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message, "Error");
            }
            finally
            {
                JoinButton.IsEnabled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}