using FontAwesome.WPF;
using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using GameClient.Models;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class LobbyPage : Page
    {
        private const string ToggleActiveStyle = "LobbyToggleActiveStyle";
        private const string ToggleInactiveStyle = "LobbyToggleInactiveStyle";
        private const int MinPlayersToStart = 2;

        private bool isLobbyCreated;
        private bool isHost;
        private string username;
        private string lobbyCode;
        private int playerCount = 4;
        private int boardId = 1;

        private LobbyChatController chatController;

        public LobbyPage(string username)
        {
            InitializeComponent();
            this.username = username;
            isHost = true;

            SubscribeToLobbyEvents();

            if (LobbyTabControl.Items.Count > 1)
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;

            StartMatchButton.Content = GameClient.Resources.Strings.CreateLobbyButton;
            StartMatchButton.IsEnabled = true;
            StartMatchButton.Opacity = 1.0;

            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        public LobbyPage(string username, string lobbyCode, JoinLobbyResultDto joinResult)
        {
            InitializeComponent();
            this.username = username;
            this.lobbyCode = lobbyCode;

            isHost = false;
            boardId = joinResult.BoardId;
            playerCount = joinResult.MaxPlayers;

            SubscribeToLobbyEvents();
            SyncLobbyVisuals(joinResult.MaxPlayers, joinResult.BoardId, joinResult.IsPublic);
            LockLobbySettings(lobbyCode);

            StartMatchButton.Visibility = Visibility.Collapsed;

            UpdatePlayerListUI(joinResult.PlayersInLobby);
            ConnectToChat();

            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            CommandBindings.Add(new CommandBinding(
                NavigationCommands.BrowseBack,
                (s, a) => a.Handled = true,
                (s, a) => { a.CanExecute = true; a.Handled = true; }));
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromLobbyEvents();
            chatController?.Close();
            CommandBindings.Clear();
        }

        private void SubscribeToLobbyEvents()
        {
            LobbyServiceManager.Instance.PlayerJoined += OnPlayerJoined;
            LobbyServiceManager.Instance.PlayerLeft += OnPlayerLeft;
            LobbyServiceManager.Instance.PlayerKicked += OnPlayerKicked;
            LobbyServiceManager.Instance.GameStarted += OnGameStarted;
            LobbyServiceManager.Instance.LobbyDisbanded += OnLobbyDisbanded;
        }

        private void UnsubscribeFromLobbyEvents()
        {
            LobbyServiceManager.Instance.PlayerJoined -= OnPlayerJoined;
            LobbyServiceManager.Instance.PlayerLeft -= OnPlayerLeft;
            LobbyServiceManager.Instance.PlayerKicked -= OnPlayerKicked;
            LobbyServiceManager.Instance.GameStarted -= OnGameStarted;
            LobbyServiceManager.Instance.LobbyDisbanded -= OnLobbyDisbanded;
        }

        private void OnPlayerJoined(PlayerLobbyDto player)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI("[Sistema]:", $"{player.Username} se ha unido.");
                RefreshLobbyState();
            });
        }

        private void OnPlayerLeft(string username)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI("[Sistema]:", $"{username} ha salido.");
                RefreshLobbyState();
            });
        }

        private void OnPlayerKicked(string reason)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(reason, "Expulsado", MessageBoxButton.OK, MessageBoxImage.Warning);
                ExitLobby();
            });
        }

        private void OnGameStarted()
        {
            Dispatcher.Invoke(() =>
            {
                chatController?.Close();
                NavigationService.Navigate(new BoardPage(lobbyCode, boardId, username));
            });
        }

        private void OnLobbyDisbanded()
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("El anfitrión cerró la sala.", "Sala cerrada");
                ExitLobby();
            });
        }

        private async void RefreshLobbyState()
        {
            try
            {
                var state = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);
                if (state != null)
                    UpdateLobbyUI(state);
            }
            catch { }
        }

        private void UpdateLobbyUI(LobbyStateDto state)
        {
            UpdatePlayerListUI(state.Players);

            if (!isHost)
                SyncLobbyVisuals(state.MaxPlayers, state.BoardId, state.IsPublic);
        }

        private void SyncLobbyVisuals(int maxPlayers, int board, bool isPublic)
        {
            playerCount = maxPlayers;
            boardId = board;
            PlayerCountBlock.Text = maxPlayers.ToString();

            BoardTypeNormalButton.Style = (Style)FindResource(board == 1 ? ToggleActiveStyle : ToggleInactiveStyle);
            BoardTypeSpecialButton.Style = (Style)FindResource(board == 2 ? ToggleActiveStyle : ToggleInactiveStyle);

            VisibilityPublicButton.Style = (Style)FindResource(isPublic ? ToggleActiveStyle : ToggleInactiveStyle);
            VisibilityPrivateButton.Style = (Style)FindResource(isPublic ? ToggleInactiveStyle : ToggleActiveStyle);
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isHost && isLobbyCreated)
                    await LobbyServiceManager.Instance.DisbandLobbyAsync(username);
                else
                    await LobbyServiceManager.Instance.LeaveLobbyAsync(username);
            }
            catch { }

            ExitLobby();
        }

        private void ExitLobby()
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
                _ = mainWindow.ShowMainMenu();
        }

        private async void StartMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isLobbyCreated)
                await CreateLobbyAsync();
            else
                await StartGameAsync();
        }

        private async Task CreateLobbyAsync()
        {
            StartMatchButton.IsEnabled = false;

            var settings = new LobbySettingsDto
            {
                IsPublic = VisibilityPublicButton.Style == (Style)FindResource(ToggleActiveStyle),
                MaxPlayers = playerCount,
                BoardId = boardId
            };

            var result = await LobbyServiceManager.Instance.CreateLobbyAsync(
                new CreateLobbyRequest { HostUsername = username, Settings = settings });

            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                StartMatchButton.IsEnabled = true;
                return;
            }

            lobbyCode = result.LobbyCode;
            LockLobbySettings(lobbyCode);
            UpdatePlayerListUI(new[] { new PlayerLobbyDto { Username = username, IsHost = true } });
            ConnectToChat();
        }

        private async Task StartGameAsync()
        {
            var state = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);

            if (state == null || state.Players.Length < MinPlayersToStart)
            {
                MessageBox.Show("Se necesitan al menos 2 jugadores.");
                return;
            }

            await LobbyServiceManager.Instance.StartGameAsync(lobbyCode);
        }

        private void LockLobbySettings(string code)
        {
            LobbySettingsPanel.IsEnabled = false;
            isLobbyCreated = true;

            StartMatchButton.Content = GameClient.Resources.Strings.StartGameButton;
            StartMatchButton.IsEnabled = false;
            StartMatchButton.Opacity = 0.5;

            if (LobbyTabControl.Items.Count > 1)
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = true;

            TitleBlock.Text = $"CÓDIGO: {code}";
            CopyCodeButton.Visibility = Visibility.Visible;
        }

        private void UpdatePlayerListUI(PlayerLobbyDto[] players)
        {
            PlayerList.Items.Clear();
            int filled = 0;

            foreach (var player in players.OrderByDescending(p => p.IsHost))
            {
                PlayerList.Items.Add(CreatePlayerItem(player));
                filled++;
            }

            for (int i = filled; i < playerCount; i++)
                PlayerList.Items.Add(CreateEmptySlotItem());

            PlayersTabHeader.Text = $"JUGADORES ({filled}/{playerCount})";

            UpdateStartButtonState(filled);
        }

        private void UpdateStartButtonState(int playersInLobby)
        {
            if (!isHost || !isLobbyCreated)
                return;

            bool canStart = playersInLobby >= MinPlayersToStart;

            StartMatchButton.IsEnabled = canStart;
            StartMatchButton.Opacity = canStart ? 1.0 : 0.5;

            if (canStart)
            {
                StartMatchButton.Content = GameClient.Resources.Strings.StartGameButton;
            }
            else
            {
                StartMatchButton.Content = GameClient.Resources.Strings.WaitingForPlayersLabel;
            }
        }


        private void ConnectToChat()
        {
            chatController = new LobbyChatController(username, lobbyCode, Dispatcher);

            chatController.MessageReceived += (sender, message) =>
                AddMessageToUI(sender + ":", message);

            chatController.SystemMessage += (message) =>
                AddMessageToUI("[Sistema]:", message);

            chatController.Connect();
            ChatMessageTextBox.KeyDown += ChatMessageTextBox_KeyDown;
        }

        private void AddMessageToUI(string name, string message)
        {
            var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
            block.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
            block.Inlines.Add(" " + message);
            ChatMessagesList.Items.Add(block);
            ChatMessagesList.ScrollIntoView(block);
        }

        private ListBoxItem CreatePlayerItem(PlayerLobbyDto player)
        {
            var text = player.Username +
                       (player.IsHost ? " (Host)" : "") +
                       (player.Username == username ? " (Tú)" : "");

            var block = new TextBlock { Text = text, FontSize = 22 };
            var icon = new ImageAwesome { Icon = FontAwesomeIcon.UserCircle, Height = 30, Width = 30 };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(icon);
            panel.Children.Add(block);

            return new ListBoxItem { Content = panel, Padding = new Thickness(10) };
        }

        private ListBoxItem CreateEmptySlotItem()
        {
            var block = new TextBlock { Text = "Slot vacío", FontSize = 22, Opacity = 0.6 };
            var icon = new ImageAwesome { Icon = FontAwesomeIcon.HourglassStart, Height = 30, Width = 30 };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(icon);
            panel.Children.Add(block);

            return new ListBoxItem { Content = panel, Padding = new Thickness(10) };
        }

        private void ChatMessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SendChatMessageButton_Click(sender, e);
        }

        private void SendChatMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChatMessageTextBox.Text))
                return;

            chatController?.SendMessage(ChatMessageTextBox.Text);
            AddMessageToUI("Tú:", ChatMessageTextBox.Text);
            ChatMessageTextBox.Clear();
        }

        private async void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(lobbyCode);
            CopyIcon.Icon = FontAwesomeIcon.Check;
            await Task.Delay(2000);
            CopyIcon.Icon = FontAwesomeIcon.Copy;
        }

        private void VisibilityPublicButton_Click(object sender, RoutedEventArgs e)
        {
            VisibilityPublicButton.Style = (Style)FindResource(ToggleActiveStyle);
            VisibilityPrivateButton.Style = (Style)FindResource(ToggleInactiveStyle);
        }

        private void VisibilityPrivateButton_Click(object sender, RoutedEventArgs e)
        {
            VisibilityPrivateButton.Style = (Style)FindResource(ToggleActiveStyle);
            VisibilityPublicButton.Style = (Style)FindResource(ToggleInactiveStyle);
        }

        private void IncreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (playerCount < 4)
            {
                playerCount++;
                PlayerCountBlock.Text = playerCount.ToString();
            }
        }

        private void DecreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (playerCount > 2)
            {
                playerCount--;
                PlayerCountBlock.Text = playerCount.ToString();
            }
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            boardId = 1;
            BoardTypeNormalButton.Style = (Style)FindResource(ToggleActiveStyle);
            BoardTypeSpecialButton.Style = (Style)FindResource(ToggleInactiveStyle);
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
            boardId = 2;
            BoardTypeSpecialButton.Style = (Style)FindResource(ToggleActiveStyle);
            BoardTypeNormalButton.Style = (Style)FindResource(ToggleInactiveStyle);
        }

        private async void OpenInviteMenu_Click(object sender, RoutedEventArgs e)
        {
            InviteFriendsOverlay.Visibility = Visibility.Visible;

            if (FriendshipServiceManager.Instance == null)
                return;

            var friends = await FriendshipServiceManager.Instance.GetFriendListAsync();
            var online = friends.Where(f => f.IsOnline).ToList();

            InviteFriendsList.ItemsSource = online;
            NoFriendsToInviteText.Visibility = online.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void CloseInviteMenu_Click(object sender, RoutedEventArgs e)
        {
            InviteFriendsOverlay.Visibility = Visibility.Collapsed;
        }
        private void OnChatTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                var raw = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
                e.CancelCommand();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var sanitized = raw.Replace("\r", "").Replace("\n", "").Trim();
                    if (sanitized.Length > ChatMessageTextBox.MaxLength)
                        sanitized = sanitized.Substring(0, ChatMessageTextBox.MaxLength);

                    ChatMessageTextBox.Text = sanitized;
                    ChatMessageTextBox.CaretIndex = ChatMessageTextBox.Text.Length;
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                e.CancelCommand();
            }
        }


        private void InviteFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                FriendshipServiceManager.Instance.SendGameInvitation(btn.Tag.ToString(), lobbyCode);
                btn.IsEnabled = false;
                btn.Content = "Enviado";
            }
        }

    }
}
