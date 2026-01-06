using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class JoinMatchCodePage : Page
    {
        private string username;

        public JoinMatchCodePage(string username)
        {
            InitializeComponent();
            this.username = username;
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

                var result = await LobbyServiceManager.Instance.JoinLobbyAsync(request);

                if (result.Success)
                {
                    await Task.Delay(200);
                    NavigationService.Navigate(new LobbyPage(username, code, result));
                }
                else
                {
                    HandleLobbyError(result.ErrorType, result.ErrorMessage);
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
            }
        }

        private void HandleLobbyError(LobbyErrorType errorType, string fallbackMessage)
        {
            string message = fallbackMessage;
            string title = GameClient.Resources.Strings.ErrorTitle;

            switch (errorType)
            {
                case LobbyErrorType.DatabaseError:
                    message = GameClient.Resources.Strings.SafeZone_DatabaseError;
                    break;
                case LobbyErrorType.ServerTimeout:
                    message = GameClient.Resources.Strings.SafeZone_ServerTimeout;
                    break;
                case LobbyErrorType.GameFull:
                    message = "La sala está llena.";
                    break;
                case LobbyErrorType.GameStarted:
                    message = "La partida ya ha comenzado.";
                    break;
                case LobbyErrorType.GameNotFound:
                    message = "No se encontró una partida con ese código.";
                    break;
                case LobbyErrorType.PlayerAlreadyInGame:
                    message = "El sistema indica que ya estás en una partida activa.";
                    break;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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