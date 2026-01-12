using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class JoinMatchCodePage : Page
    {
        private const int LOBBY_CODE_LENGTH = 5;
        private const int NAVIGATION_DELAY_MS = 200;
        private readonly string username;

        public JoinMatchCodePage(string username)
        {
            InitializeComponent();
            this.username = username;
        }

        private async void JoinButton_Click(object sender, RoutedEventArgs e)
        {
            string code = LobbyCodeBox.Text.Trim().ToUpper();

            if (!IsValidLobbyCode(code))
            {
                ShowMessage(
                    GameClient.Resources.Strings.InvalidCodeMessage,
                    GameClient.Resources.Strings.InvalidCodeTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowErrorMessage(GameClient.Resources.Strings.Error_NoInternet);
                return;
            }

            await ProcessJoinLobbyAsync(code);
        }

        private bool IsValidLobbyCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code) && code.Length == LOBBY_CODE_LENGTH;
        }

        private async Task ProcessJoinLobbyAsync(string code)
        {
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
                    await Task.Delay(NAVIGATION_DELAY_MS);
                    NavigationService.Navigate(new LobbyPage(username, code, result));
                }
                else
                {
                    HandleLobbyError(result.ErrorType, result.ErrorMessage);
                }
            }
            catch (TimeoutException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.TimeoutLabel);
            }
            catch (EndpointNotFoundException)
            {
                ShowMessage(
                    GameClient.Resources.Strings.EndpointNotFoundLabel,
                    GameClient.Resources.Strings.EndpointNotFoundTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (CommunicationException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ComunicationLabel);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(
                    string.Format(GameClient.Resources.Strings.UnexpectedErrorMessage, ex.Message)
                );
            }
            finally
            {
                JoinButton.IsEnabled = true;
            }
        }

        private void HandleLobbyError(LobbyErrorType errorType, string fallbackMessage)
        {
            string message = GetErrorMessage(errorType, fallbackMessage);
            ShowWarningMessage(message);
        }

        private string GetErrorMessage(LobbyErrorType errorType, string fallbackMessage)
        {
            switch (errorType)
            {
                case LobbyErrorType.DatabaseError:
                    return GameClient.Resources.Strings.SafeZone_DatabaseError;
                case LobbyErrorType.ServerTimeout:
                    return GameClient.Resources.Strings.SafeZone_ServerTimeout;
                case LobbyErrorType.GameFull:
                    return GameClient.Resources.Strings.LobbyError_Full;
                case LobbyErrorType.GameStarted:
                    return GameClient.Resources.Strings.LobbyError_Started;
                case LobbyErrorType.GameNotFound:
                    return GameClient.Resources.Strings.LobbyError_NotFoundByCode;
                case LobbyErrorType.PlayerAlreadyInGame:
                    return GameClient.Resources.Strings.LobbyError_AlreadyInGame;
                default:
                    return fallbackMessage;
            }
        }

        private void ShowMessage(string message, string title, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            MessageBox.Show(message, title, button, icon);
        }

        private void ShowErrorMessage(string message)
        {
            ShowMessage(message, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowWarningMessage(string message)
        {
            ShowMessage(message, GameClient.Resources.Strings.DialogWarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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