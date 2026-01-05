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
        private Action _onDialogConfirmAction;

        public LobbyPage(string username)
        {
            InitializeComponent();
            AudioManager.PlayRandomMusic(AudioManager.LobbyTracks);

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
            AudioManager.PlayRandomMusic(AudioManager.LobbyTracks);

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

        private void ShowOverlayDialog(string title, string message, FontAwesomeIcon icon, bool isConfirmation = false, Action onConfirm = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Icon = icon;
            DialogCancelBtn.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
            DialogCancelColumn.Width = isConfirmation ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            DialogConfirmBtn.Content = isConfirmation ? GameClient.Resources.Strings.DialogConfirmBtn : GameClient.Resources.Strings.DialogOkBtn;
            DialogCancelBtn.Content = GameClient.Resources.Strings.DialogCancelBtn;
            _onDialogConfirmAction = onConfirm;
            CustomDialogOverlay.Visibility = Visibility.Visible;
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            CustomDialogOverlay.Visibility = Visibility.Collapsed;
            _onDialogConfirmAction?.Invoke();
            _onDialogConfirmAction = null;
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
            Dispatcher.Invoke(async () =>
            {
                AddMessageToUI(GameClient.Resources.Strings.SystemPrefix, string.Format(GameClient.Resources.Strings.PlayerJoinedMsg, player.Username));
                await RefreshLobbyState();
            });
        }

        private void OnPlayerLeft(string username)
        {
            Dispatcher.Invoke(async () =>
            {
                AddMessageToUI(GameClient.Resources.Strings.SystemPrefix, string.Format(GameClient.Resources.Strings.PlayerLeftMsg, username));
                await RefreshLobbyState();
            });
        }

        private void OnPlayerKicked(string reason)
        {
            Dispatcher.Invoke(() =>
            {
                ShowOverlayDialog(GameClient.Resources.Strings.KickedTitle, GameClient.Resources.Strings.KickedByHost, FontAwesomeIcon.ExclamationTriangle, false, () => ExitLobby());
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
                ShowOverlayDialog(GameClient.Resources.Strings.LobbyClosedTitle, GameClient.Resources.Strings.LobbyDisbandedByHost, FontAwesomeIcon.InfoCircle, false, () => ExitLobby());
            });
        }

        private async Task RefreshLobbyState()
        {
            try
            {
                var state = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);
                if (state != null)
                    UpdateLobbyUI(state);
            }
            catch (TimeoutException)
            {
                HandleConnectionError(GameClient.Resources.Strings.ErrorLobbyUpdateTimeout);
            }
            catch (CommunicationException)
            {
                HandleConnectionError(GameClient.Resources.Strings.ErrorLobbyUpdateComm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando lobby: {ex.Message}");
            }
        }

        private void HandleConnectionError(string message)
        {
            ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, message, FontAwesomeIcon.TimesCircle, false, () => ExitLobby());
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
            if (isHost && isLobbyCreated)
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.DialogConfirmTitle,
                    GameClient.Resources.Strings.LobbyHostExitConfirm,
                    FontAwesomeIcon.ExclamationTriangle,
                    true,
                    async () => await ProcessExitLobby()
                );
            }
            else
            {
                await ProcessExitLobby();
            }
        }

        private async Task ProcessExitLobby()
        {
            try
            {
                if (isHost && isLobbyCreated)
                    await LobbyServiceManager.Instance.DisbandLobbyAsync(username);
                else
                    await LobbyServiceManager.Instance.LeaveLobbyAsync(username);
            }
            catch (TimeoutException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.ErrorTitle, GameClient.Resources.Strings.ErrorNotifyServer, FontAwesomeIcon.ClockOutline);
            }
            catch (CommunicationException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorLeaveLobby, FontAwesomeIcon.Wifi);
            }
            finally
            {
                ExitLobby();
            }
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

            try
            {
                var result = await LobbyServiceManager.Instance.CreateLobbyAsync(
                    new CreateLobbyRequest { HostUsername = username, Settings = settings });

                if (!result.Success)
                {
                    ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, result.ErrorMessage, FontAwesomeIcon.TimesCircle);
                    StartMatchButton.IsEnabled = true;
                    return;
                }

                lobbyCode = result.LobbyCode;
                LockLobbySettings(lobbyCode);
                UpdatePlayerListUI(new[] { new PlayerLobbyDto { Username = username, IsHost = true } });
                ConnectToChat();
            }
            catch (TimeoutException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.ErrorTitle, GameClient.Resources.Strings.ErrorNotifyServer, FontAwesomeIcon.ClockOutline);
                StartMatchButton.IsEnabled = true;
            }
            catch (CommunicationException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorLobbyUpdateComm, FontAwesomeIcon.Wifi);
                StartMatchButton.IsEnabled = true;
            }
        }

        private async Task StartGameAsync()
        {
            try
            {
                var state = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);

                if (state == null || state.Players.Length < MinPlayersToStart)
                {
                    ShowOverlayDialog(GameClient.Resources.Strings.ImpossibleStartTitle, GameClient.Resources.Strings.MinPlayersRequired, FontAwesomeIcon.InfoCircle);
                    return;
                }

                await LobbyServiceManager.Instance.StartGameAsync(lobbyCode);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is CommunicationException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorStartGame, FontAwesomeIcon.TimesCircle);
            }
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

            TitleBlock.Text = string.Format(GameClient.Resources.Strings.LobbyCodeTitle, code);
            CopyCodeButton.Visibility = Visibility.Visible;
        }

        private void UpdatePlayerListUI(PlayerLobbyDto[] players)
        {
            PlayerList.Items.Clear();
            int slotsFilled = 0;

            foreach (var player in players.OrderByDescending(p => p.IsHost))
            {
                PlayerList.Items.Add(CreatePlayerItem(player));
                slotsFilled++;
            }

            int emptySlots = playerCount - slotsFilled;
            for (int i = 0; i < emptySlots; i++)
            {
                PlayerList.Items.Add(CreateEmptySlotItem());
            }

            PlayersTabHeader.Text = string.Format(GameClient.Resources.Strings.PlayersCountTitle, slotsFilled, playerCount);

            UpdateStartButtonState(slotsFilled);
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
                AddMessageToUI(GameClient.Resources.Strings.SystemPrefix, message);

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
            var textBlock = new TextBlock
            {
                Text = player.Username,
                FontSize = 22,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (player.IsHost)
            {
                textBlock.Text += " " + GameClient.Resources.Strings.LobbyHostTag;
                textBlock.FontWeight = FontWeights.Bold;
            }

            if (player.Username == username)
            {
                textBlock.Text += " " + GameClient.Resources.Strings.LobbyYouTag;
            }

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
            var block = new TextBlock { Text = GameClient.Resources.Strings.EmptySlotText, FontSize = 22, Opacity = 0.6 };
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
            AddMessageToUI(GameClient.Resources.Strings.ChatYou, ChatMessageTextBox.Text);
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

            try
            {
                var friends = await FriendshipServiceManager.Instance.GetFriendListAsync();
                var online = friends.Where(f => f.IsOnline).ToList();

                InviteFriendsList.ItemsSource = online;
                NoFriendsToInviteText.Visibility = online.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorLoadFriends, FontAwesomeIcon.ExclamationTriangle);
            }
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
                try
                {
                    FriendshipServiceManager.Instance.SendGameInvitation(btn.Tag.ToString(), lobbyCode);
                    btn.IsEnabled = false;
                    btn.Content = GameClient.Resources.Strings.InviteSentStatus;
                }
                catch (CommunicationException)
                {
                    ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorInviteFriend, FontAwesomeIcon.TimesCircle);
                }
            }
        }
    }
}