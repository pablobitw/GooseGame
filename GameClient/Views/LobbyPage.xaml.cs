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
using System.Windows.Threading;
using FontAwesome.WPF;
using GameClient.ChatServiceReference;

namespace GameClient.Views
{
    public partial class LobbyPage : Page, IChatServiceCallback
    {
        private bool _isLobbyCreated = false;
        private bool _isHost = false;
        private string _username;
        private string _lobbyCode;
        private LobbyServiceClient _lobbyClient;
        private ChatServiceClient _chatClient;
        private int _playerCount = 4;
        private int _boardId = 1;
        private DispatcherTimer _pollingTimer;

        public LobbyPage(string username)
        {
            InitializeComponent();
            _username = username;
            _isHost = true;
            _lobbyClient = new LobbyServiceClient();

            if (LobbyTabControl.Items.Count > 1)
            {
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;
            }
        }

        public LobbyPage(string username, string lobbyCode, JoinLobbyResultDTO joinResult)
        {
            InitializeComponent();
            _username = username;
            _lobbyCode = lobbyCode;
            _boardId = joinResult.BoardId;
            _playerCount = joinResult.MaxPlayers;
            _isHost = false;
            _lobbyClient = new LobbyServiceClient();

            LockLobbySettings(lobbyCode);
            StartMatchButton.Visibility = Visibility.Collapsed;

            UpdatePlayerListUI(joinResult.PlayersInLobby);
            InitializeTimer();
            ConnectToChatService();
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _pollingTimer?.Stop();

            if (_isLobbyCreated)
            {
                try
                {
                    if (_isHost)
                    {
                        await _lobbyClient.DisbandLobbyAsync(_username);
                    }
                    _chatClient.LeaveLobbyChat(_username, _lobbyCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error al salir del lobby: " + ex.Message);
                }
            }

            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }

            await CloseClientAsync();
            await CloseChatClientAsync();
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            _boardId = 2;
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            _boardId = 1;
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
            SendMessage();
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
                    BoardId = _boardId
                };

                try
                {
                    var result = await _lobbyClient.CreateLobbyAsync(settings, _username);
                    if (result.Success)
                    {
                        _lobbyCode = result.LobbyCode;
                        LockLobbySettings(result.LobbyCode);

                        var initialPlayers = new PlayerLobbyDTO[] { new PlayerLobbyDTO { Username = _username, IsHost = true } };
                        UpdatePlayerListUI(initialPlayers);
                        InitializeTimer();
                        ConnectToChatService();
                    }
                    else
                    {
                        MessageBox.Show($"Error al crear lobby: {result.ErrorMessage}", "Error");
                        if (result.ErrorMessage.Contains("already in a game"))
                        {
                            try { await _lobbyClient.DisbandLobbyAsync(_username); } catch { }
                        }
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
                        _pollingTimer.Stop();
                        NavigationService.Navigate(new BoardPage(_lobbyCode, _boardId));
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

        private void ConnectToChatService()
        {
            try
            {
                InstanceContext context = new InstanceContext(this);
                _chatClient = new ChatServiceClient(context);
                _chatClient.JoinLobbyChat(_username, _lobbyCode);

                ChatMessageTextBox.KeyDown += ChatMessageTextBox_KeyDown;
            }
            catch (Exception ex)
            {
                AddMessageToUI("[Sistema]:", "No se pudo conectar al chat: " + ex.Message);
            }
        }

        private void ChatMessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessage();
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(ChatMessageTextBox.Text) || _chatClient == null) return;

            try
            {
                _chatClient.SendLobbyMessage(_username, _lobbyCode, ChatMessageTextBox.Text);
                AddMessageToUI("Tú:", ChatMessageTextBox.Text);
                ChatMessageTextBox.Clear();
            }
            catch (Exception ex)
            {
                AddMessageToUI("[Sistema]:", "Error al enviar mensaje: " + ex.Message);
            }
        }

        public void ReceiveMessage(string username, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI(username + ":", message);
            });
        }

        private void AddMessageToUI(string name, string message)
        {
            var textBlock = new TextBlock();
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(" " + message);

            ChatMessagesList.Items.Add(textBlock);
            ChatMessagesList.ScrollIntoView(textBlock);
        }

        private async Task CloseChatClientAsync()
        {
            try
            {
                if (_chatClient.State == CommunicationState.Opened)
                {
                    _chatClient.Close();
                }
            }
            catch (Exception)
            {
                _chatClient.Abort();
            }
            await Task.CompletedTask;
        }

        private void LockLobbySettings(string lobbyCode)
        {
            LobbySettingsPanel.IsEnabled = false;
            StartMatchButton.Content = GameClient.Resources.Strings.StartMatchButton;
            StartMatchButton.IsEnabled = true;
            if (LobbyTabControl.Items.Count > 1) { (LobbyTabControl.Items[1] as TabItem).IsEnabled = true; }
            _isLobbyCreated = true;
            TitleBlock.Text = $"CÓDIGO DE PARTIDA: {lobbyCode}";
            CopyCodeButton.Visibility = Visibility.Visible;
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

        private async void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_lobbyCode);

            CopyIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Check;
            CopyCodeButton.ToolTip = "¡Copiado!";

            await Task.Delay(2000);

            CopyIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Copy;
            CopyCodeButton.ToolTip = "Copiar Código";
        }

        private void InitializeTimer()
        {
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromSeconds(3);
            _pollingTimer.Tick += async (s, e) => await PollLobbyState();
            _pollingTimer.Start();
        }

        private async Task PollLobbyState()
        {
            try
            {
                var state = await _lobbyClient.GetLobbyStateAsync(_lobbyCode);
                if (state.IsGameStarted)
                {
                    _pollingTimer.Stop();
                    if (_isHost)
                    {
                        MessageBox.Show("¡El juego ya ha comenzado! (Error de estado)");
                    }
                    else
                    {
                        NavigationService.Navigate(new BoardPage(_lobbyCode, _boardId));
                    }
                }
                else
                {
                    UpdatePlayerListUI(state.Players);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error de sondeo: " + ex.Message);
            }
        }

        private void UpdatePlayerListUI(PlayerLobbyDTO[] players)
        {
            PlayerList.Items.Clear();
            int slotsFilled = 0;
            foreach (var player in players.OrderByDescending(p => p.IsHost))
            {
                PlayerList.Items.Add(CreatePlayerItem(player));
                slotsFilled++;
            }
            int emptySlots = _playerCount - slotsFilled;
            for (int i = 0; i < emptySlots; i++)
            {
                PlayerList.Items.Add(CreateEmptySlotItem());
            }
            PlayersTabHeader.Text = $"JUGADORES ({slotsFilled}/{_playerCount})";
        }

        private ListBoxItem CreatePlayerItem(PlayerLobbyDTO player)
        {
            var textBlock = new TextBlock
            {
                Text = player.Username,
                FontSize = 25,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (player.IsHost) { textBlock.Text += " (Host)"; textBlock.FontWeight = FontWeights.Bold; }
            if (player.Username == _username) { textBlock.Text += " (Tú)"; }
            var icon = new FontAwesome.WPF.FontAwesome
            {
                Icon = FontAwesomeIcon.UserCircle,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 138, 199)),
                Height = 30,
                Width = 30,
                Margin = new Thickness(0, 0, 15, 0)
            };
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            return new ListBoxItem { Content = stackPanel, Padding = new Thickness(10) };
        }

        private ListBoxItem CreateEmptySlotItem()
        {
            var textBlock = new TextBlock
            {
                Text = "Slot Vacío",
                FontSize = 25,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7
            };
            var icon = new FontAwesome.WPF.FontAwesome
            {
                Icon = FontAwesomeIcon.HourglassStart,
                Foreground = new SolidColorBrush(Colors.Gray),
                Height = 30,
                Width = 30,
                Margin = new Thickness(0, 0, 15, 0)
            };
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            return new ListBoxItem { Content = stackPanel, Padding = new Thickness(10) };
        }
    }
}