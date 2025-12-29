using FontAwesome.WPF;
using GameClient.ChatServiceReference;
using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using GameClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace GameClient.Views
{
    public partial class LobbyPage : Page, IChatServiceCallback
    {
        private const string ToggleActiveStyle = "LobbyToggleActiveStyle";
        private const string ToggleInactiveStyle = "LobbyToggleInactiveStyle";

        private bool isLobbyCreated = false;
        private bool isHost = false;
        private string username;
        private string lobbyCode;
        private ChatServiceClient chatClient;
        private int playerCount = 4;
        private int boardId = 1;

        public LobbyPage(string username)
        {
            InitializeComponent();
            this.username = username;
            isHost = true;

            SubscribeToLobbyEvents();

            if (LobbyTabControl.Items.Count > 1)
            {
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;
            }

            StartMatchButton.IsEnabled = true;
            StartMatchButton.Opacity = 1.0;
            StartMatchButton.Content = GameClient.Resources.Strings.CreateLobbyButton ?? "CREAR SALA";

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;
        }

        public LobbyPage(string username, string lobbyCode, JoinLobbyResultDto joinResult)
        {
            InitializeComponent();
            this.username = username;
            this.lobbyCode = lobbyCode;
            boardId = joinResult.BoardId;
            playerCount = joinResult.MaxPlayers;
            isHost = false;

            SubscribeToLobbyEvents();

            SyncLobbyVisuals(joinResult.MaxPlayers, joinResult.BoardId, joinResult.IsPublic);
            LockLobbySettings(lobbyCode);
            StartMatchButton.Visibility = Visibility.Collapsed;

            UpdatePlayerListUI(joinResult.PlayersInLobby);

            ConnectToChatService();

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var backCommandBinding = new CommandBinding(
                NavigationCommands.BrowseBack,
                OnBrowseBackExecuted,
                OnBrowseBackCanExecute);

            this.CommandBindings.Add(backCommandBinding);
        }

        private void OnBrowseBackCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void OnBrowseBackExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromLobbyEvents();
            CloseChatClient();
            this.CommandBindings.Clear();
        }

        private void SubscribeToLobbyEvents()
        {
            LobbyServiceManager.Instance.PlayerKicked += OnPlayerKicked;
            LobbyServiceManager.Instance.PlayerJoined += OnPlayerJoined;
            LobbyServiceManager.Instance.PlayerLeft += OnPlayerLeft;
            LobbyServiceManager.Instance.GameStarted += OnGameStarted;
            LobbyServiceManager.Instance.LobbyDisbanded += OnLobbyDisbanded;
        }

        private void UnsubscribeFromLobbyEvents()
        {
            LobbyServiceManager.Instance.PlayerKicked -= OnPlayerKicked;
            LobbyServiceManager.Instance.PlayerJoined -= OnPlayerJoined;
            LobbyServiceManager.Instance.PlayerLeft -= OnPlayerLeft;
            LobbyServiceManager.Instance.GameStarted -= OnGameStarted;
            LobbyServiceManager.Instance.LobbyDisbanded -= OnLobbyDisbanded;
        }

        private void OnPlayerJoined(PlayerLobbyDto newPlayer)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI("[Sistema]:", $"{newPlayer.Username} se ha unido.");
                RefreshLobbyState();
            });
        }

        private void OnPlayerLeft(string leftUsername)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI("[Sistema]:", $"{leftUsername} ha salido.");
                RefreshLobbyState();
            });
        }

        private void OnGameStarted()
        {
            Dispatcher.Invoke(() =>
            {
                UnsubscribeFromLobbyEvents();
                CloseChatClient();
                NavigationService.Navigate(new BoardPage(lobbyCode, boardId, username));
            });
        }

        private void OnLobbyDisbanded()
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("El anfitrión ha disuelto la sala.", "Sala Cerrada", MessageBoxButton.OK, MessageBoxImage.Information);
                ExitLobbyToMenu();
            });
        }

        private async void RefreshLobbyState()
        {
            try
            {
                var state = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);
                if (state != null)
                {
                    UpdateLobbyUI(state);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refrescando lobby: {ex.Message}");
            }
        }

        private void OnPlayerKicked(string reason)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(reason, "Expulsado del Lobby", MessageBoxButton.OK, MessageBoxImage.Warning);
                ExitLobbyToMenu();
            });
        }

        private async void ExitLobbyToMenu()
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                await mainWindow.ShowMainMenu();
            }
        }

        private void UpdateLobbyUI(LobbyStateDto state)
        {
            UpdatePlayerListUI(state.Players);

            if (!isHost)
            {
                SyncLobbyVisuals(state.MaxPlayers, state.BoardId, state.IsPublic);
            }
        }

        private void SyncLobbyVisuals(int maxPlayers, int currentBoardId, bool isPublic)
        {
            PlayerCountBlock.Text = maxPlayers.ToString();
            boardId = currentBoardId;

            if (currentBoardId == 2)
            {
                BoardTypeSpecialButton.Style = (Style)FindResource(ToggleActiveStyle);
                BoardTypeNormalButton.Style = (Style)FindResource(ToggleInactiveStyle);
            }
            else
            {
                BoardTypeNormalButton.Style = (Style)FindResource(ToggleActiveStyle);
                BoardTypeSpecialButton.Style = (Style)FindResource(ToggleInactiveStyle);
            }

            if (isPublic)
            {
                VisibilityPublicButton.Style = (Style)FindResource(ToggleActiveStyle);
                VisibilityPrivateButton.Style = (Style)FindResource(ToggleInactiveStyle);
            }
            else
            {
                VisibilityPrivateButton.Style = (Style)FindResource(ToggleActiveStyle);
                VisibilityPublicButton.Style = (Style)FindResource(ToggleInactiveStyle);
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLobbyCreated && isHost)
            {
                var result = MessageBox.Show("Se disolverá la sala. ¿Seguro?", "Salir", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                try
                {
                    await LobbyServiceManager.Instance.DisbandLobbyAsync(username);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            else
            {
                try
                {
                    await LobbyServiceManager.Instance.LeaveLobbyAsync(username);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            ExitLobbyToMenu();
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeSpecialButton.Style = (Style)FindResource(ToggleActiveStyle);
            BoardTypeNormalButton.Style = (Style)FindResource(ToggleInactiveStyle);
            boardId = 2;
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeNormalButton.Style = (Style)FindResource(ToggleActiveStyle);
            BoardTypeSpecialButton.Style = (Style)FindResource(ToggleInactiveStyle);
            boardId = 1;
        }

        private void DecreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (playerCount > 2) { playerCount--; PlayerCountBlock.Text = playerCount.ToString(); }
        }

        private void IncreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (playerCount < 4) { playerCount++; PlayerCountBlock.Text = playerCount.ToString(); }
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

        private void SendChatMessageButton_Click(object sender, RoutedEventArgs e) => SendMessage();

        private async void StartMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isLobbyCreated) await HandleCreateLobbyAsync();
            else await HandleStartGameAsync();
        }

        private async Task HandleCreateLobbyAsync()
        {
            StartMatchButton.IsEnabled = false;
            var settings = new LobbySettingsDto
            {
                IsPublic = (VisibilityPublicButton.Style == (Style)FindResource(ToggleActiveStyle)),
                MaxPlayers = playerCount,
                BoardId = boardId
            };

            try
            {
                var request = new CreateLobbyRequest { Settings = settings, HostUsername = username };
                var result = await LobbyServiceManager.Instance.CreateLobbyAsync(request);

                if (result.Success)
                {
                    lobbyCode = result.LobbyCode;
                    LockLobbySettings(result.LobbyCode);
                    var initialPlayers = new PlayerLobbyDto[] { new PlayerLobbyDto { Username = username, IsHost = true } };
                    UpdatePlayerListUI(initialPlayers);
                    ConnectToChatService();
                }
                else
                {
                    MessageBox.Show($"Error: {result.ErrorMessage}");
                    StartMatchButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                StartMatchButton.IsEnabled = true;
            }
        }

        private async Task HandleStartGameAsync()
        {
            StartMatchButton.IsEnabled = false;

            try
            {
                var serverState = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);

                if (serverState == null || serverState.Players.Length < 2)
                {
                    MessageBox.Show("Error: Se necesitan al menos 2 jugadores (Verificación de Servidor).");

                    if (serverState != null) UpdatePlayerListUI(serverState.Players);
                    StartMatchButton.IsEnabled = true;
                    return;
                }

                bool started = await LobbyServiceManager.Instance.StartGameAsync(lobbyCode);

                if (!started)
                {
                    MessageBox.Show("Error al iniciar.");
                    StartMatchButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                StartMatchButton.IsEnabled = true;
            }
        }

        private void ConnectToChatService()
        {
            try
            {
                InstanceContext context = new InstanceContext(this);
                chatClient = new ChatServiceClient(context);
                var request = new JoinChatRequest { Username = username, LobbyCode = lobbyCode };
                chatClient.JoinLobbyChat(request);
                ChatMessageTextBox.KeyDown += ChatMessageTextBox_KeyDown;
            }
            catch (Exception ex)
            {
                AddMessageToUI("[Sistema]:", "Error conectando chat: " + ex.Message);
            }
        }

        private void ChatMessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SendMessage(); }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(ChatMessageTextBox.Text) || chatClient == null) return;
            try
            {
                var msgDto = new ChatMessageDto { Sender = username, LobbyCode = lobbyCode, Message = ChatMessageTextBox.Text };
                chatClient.SendLobbyMessage(msgDto);
                AddMessageToUI("Tú:", ChatMessageTextBox.Text);
                ChatMessageTextBox.Clear();
            }
            catch (Exception ex)
            {
                AddMessageToUI("[Sistema]:", "Error enviando mensaje: " + ex.Message);
            }
        }

        public async void ReceiveMessage(ChatMessageDto message)
        {
            await Dispatcher.InvokeAsync(() => AddMessageToUI(message.Sender + ":", message.Message));
        }

        private void AddMessageToUI(string name, string message)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            textBlock.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(" " + message);
            ChatMessagesList.Items.Add(textBlock);
            ChatMessagesList.ScrollIntoView(textBlock);
        }

        private void CloseChatClient()
        {
            if (chatClient != null)
            {
                try
                {
                    if (chatClient.State == CommunicationState.Opened)
                    {
                        chatClient.Close();
                    }
                    else
                    {
                        chatClient.Abort();
                    }
                }
                catch (CommunicationException)
                {
                    chatClient.Abort();
                }
                catch (TimeoutException)
                {
                    chatClient.Abort();
                }
                catch (Exception)
                {
                    chatClient.Abort();
                }
            }
        }

        private void LockLobbySettings(string lobbyCode)
        {
            LobbySettingsPanel.IsEnabled = false;

            StartMatchButton.Content = GameClient.Resources.Strings.StartGameButton;
            StartMatchButton.IsEnabled = false;
            StartMatchButton.Opacity = 0.5;

            if (LobbyTabControl.Items.Count > 1) (LobbyTabControl.Items[1] as TabItem).IsEnabled = true;
            isLobbyCreated = true;
            TitleBlock.Text = $"CÓDIGO: {lobbyCode}";
            CopyCodeButton.Visibility = Visibility.Visible;
        }

        private async void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(lobbyCode);
            CopyIcon.Icon = FontAwesomeIcon.Check;
            await Task.Delay(2000);
            CopyIcon.Icon = FontAwesomeIcon.Copy;
        }

        private void UpdatePlayerListUI(PlayerLobbyDto[] players)
        {
            PlayerList.Items.Clear();
            int slotsFilled = 0;
            if (players != null)
            {
                foreach (var player in players.OrderByDescending(p => p.IsHost))
                {
                    PlayerList.Items.Add(CreatePlayerItem(player));
                    slotsFilled++;
                }
            }
            int emptySlots = playerCount - slotsFilled;
            for (int i = 0; i < emptySlots; i++) PlayerList.Items.Add(CreateEmptySlotItem());
            PlayersTabHeader.Text = $"JUGADORES ({slotsFilled}/{playerCount})";

            if (isHost && isLobbyCreated)
            {
                bool canStart = slotsFilled >= 2;
                StartMatchButton.IsEnabled = canStart;
                StartMatchButton.Opacity = canStart ? 1.0 : 0.5;
                StartMatchButton.Content = canStart
                    ? GameClient.Resources.Strings.StartGameButton
                    : "Esperando jugadores...";
            }
        }

        private ListBoxItem CreatePlayerItem(PlayerLobbyDto player)
        {
            var textBlock = new TextBlock { Text = player.Username, FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
            if (player.IsHost) { textBlock.Text += " (Host)"; textBlock.FontWeight = FontWeights.Bold; }
            if (player.Username == username) textBlock.Text += " (Tú)";
            var icon = new FontAwesome.WPF.ImageAwesome { Icon = FontAwesomeIcon.UserCircle, Foreground = new SolidColorBrush(Color.FromRgb(52, 138, 199)), Height = 30, Width = 30, Margin = new Thickness(0, 0, 15, 0) };
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            stackPanel.Children.Add(icon); stackPanel.Children.Add(textBlock);
            return new ListBoxItem { Content = stackPanel, Padding = new Thickness(10) };
        }

        private ListBoxItem CreateEmptySlotItem()
        {
            var textBlock = new TextBlock { Text = "Slot Vacío", FontSize = 22, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
            var icon = new FontAwesome.WPF.ImageAwesome { Icon = FontAwesomeIcon.HourglassStart, Foreground = new SolidColorBrush(Colors.Gray), Height = 30, Width = 30, Margin = new Thickness(0, 0, 15, 0) };
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            stackPanel.Children.Add(icon); stackPanel.Children.Add(textBlock);
            return new ListBoxItem { Content = stackPanel, Padding = new Thickness(10) };
        }

        private async void OpenInviteMenu_Click(object sender, RoutedEventArgs e)
        {
            InviteFriendsOverlay.Visibility = Visibility.Visible;
            if (FriendshipServiceManager.Instance != null)
            {
                var friends = await FriendshipServiceManager.Instance.GetFriendListAsync();
                var online = friends.Where(f => f.IsOnline).ToList();
                InviteFriendsList.ItemsSource = online;
                NoFriendsToInviteText.Visibility = online.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CloseInviteMenu_Click(object sender, RoutedEventArgs e) => InviteFriendsOverlay.Visibility = Visibility.Collapsed;

        private void InviteFriend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            FriendshipServiceManager.Instance.SendGameInvitation(btn.Tag.ToString(), this.lobbyCode);
            btn.IsEnabled = false; btn.Content = "Enviado";
        }
    }
}