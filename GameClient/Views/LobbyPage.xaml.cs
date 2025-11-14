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
using GameClient.LobbyServiceReference;
using System.ServiceModel;

namespace GameClient.Views
{

    public partial class LobbyPage : Page
    {
        private bool _isLobbyCreated = false;
        private string _username;
        private string _lobbyCode;
        private LobbyServiceClient _lobbyClient;
        private int _playerCount = 4;

        public LobbyPage(string username)
        {
            InitializeComponent();
            _username = username;
            _lobbyClient = new LobbyServiceClient();

            if (LobbyTabControl.Items.Count > 1)
            {
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLobbyCreated)
            {

            }

            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }

            await CloseClientAsync();
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
        }

        private void DecreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playerCount > 2)
            {
                _playerCount--;
                PlayerCountBlock.Text = _playerCount.ToString();
            }
        }

        private void IncreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playerCount < 4)
            {
                _playerCount++;
                PlayerCountBlock.Text = _playerCount.ToString();
            }
        }

        private void VisibilityPublicButton_Click(object sender, RoutedEventArgs e)
        {
            VisibilityPublicButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            VisibilityPrivateButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
        }

        private void VisibilityPrivateButton_Click(object sender, RoutedEventArgs e)
        {
            VisibilityPrivateButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            VisibilityPublicButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
        }

        private void SendChatMessageButton_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private async void StartMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLobbyCreated == false)
            {
                StartMatchButton.IsEnabled = false;

                var settings = new LobbySettingsDTO
                {
                    IsPublic = (VisibilityPublicButton.Style == (Style)FindResource("LobbyToggleActiveStyle")),
                    MaxPlayers = _playerCount,
                    BoardId = (BoardTypeSpecialButton.Style == (Style)FindResource("LobbyToggleActiveStyle")) ? 1 : 0
                };

                try
                {
                    var result = await _lobbyClient.CreateLobbyAsync(settings, _username);
                    if (result.Success)
                    {
                        _lobbyCode = result.LobbyCode;
                        LockLobbySettings(result.LobbyCode);
                    }
                    else
                    {
                        MessageBox.Show($"Error al crear lobby: {result.ErrorMessage}", "Error");
                        StartMatchButton.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error de conexión al crear lobby: " + ex.Message, "Error");
                    StartMatchButton.IsEnabled = true;
                }
            }
            else
            {
                try
                {
                    bool started = await _lobbyClient.StartGameAsync(_lobbyCode);
                    if (started)
                    {
                        MessageBox.Show("¡PARTIDA INICIADA! (Navegar a GameBoardPage aquí)");
                    }
                    else
                    {
                        MessageBox.Show("Error: No se pudo iniciar la partida.", "Error");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error de conexión al iniciar partida: " + ex.Message, "Error");
                }
            }
        }

        private void LockLobbySettings(string lobbyCode)
        {
            LobbySettingsPanel.IsEnabled = false;

            StartMatchButton.Content = GameClient.Resources.Strings.StartMatchButton;
            StartMatchButton.IsEnabled = true;

            if (LobbyTabControl.Items.Count > 1)
            {
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = true;
            }

            _isLobbyCreated = true;
            TitleBlock.Text = $"CÓDIGO DE PARTIDA: {lobbyCode}";
        }

        private async Task CloseClientAsync()
        {
            try
            {
                if (_lobbyClient.State == CommunicationState.Opened)
                {
                    _lobbyClient.Close();
                }
            }
            catch (Exception)
            {
                _lobbyClient.Abort();
            }
            await Task.CompletedTask; 
        }
    }
}