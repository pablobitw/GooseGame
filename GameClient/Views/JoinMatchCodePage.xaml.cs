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
                MessageBox.Show(GameClient.Resources.Strings.InvalidCodeMessage, GameClient.Resources.Strings.InvalidCodeTitle);
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
                    MessageBox.Show(result.ErrorMessage, GameClient.Resources.Strings.ErrorTitle);
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.EndpointNotFoundTitle);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(GameClient.Resources.Strings.UnexpectedErrorMessage, ex.Message), GameClient.Resources.Strings.ErrorTitle);
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