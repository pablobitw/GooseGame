using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.LobbyServiceReference; 

namespace GameClient.Views
{
    public partial class JoinMatchCodePage : Page
    {
        private string username;
        private LobbyServiceClient lobbyClient;

        public JoinMatchCodePage(string username)
        {
            InitializeComponent();
            this.username = username;
            lobbyClient = new LobbyServiceClient();
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
                var request = new JoinLobbyRequest
                {
                    LobbyCode = code,
                    Username = username
                };

                var result = await lobbyClient.JoinLobbyAsync(request);

                if (result.Success)
                {
                    NavigationService.Navigate(new LobbyPage(username, code, result));
                }
                else
                {
                    MessageBox.Show(result.ErrorMessage, "Error");
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show("El servidor tardó demasiado en responder.", "Error de Tiempo");
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor.", "Error de Conexión");
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de comunicación con el servicio de lobby.", "Error de Red");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ocurrió un error inesperado: " + ex.Message, "Error");
            }
            finally
            {
                JoinButton.IsEnabled = true;
                if (lobbyClient.State == CommunicationState.Opened)
                {
                    lobbyClient.Close();
                }
                else
                {
                    lobbyClient.Abort();
                    lobbyClient = new LobbyServiceClient();
                }
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