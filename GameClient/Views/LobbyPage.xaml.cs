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
        private bool isLobbyCreated = false;
        private bool isHost = false;
        private string username;
        private string lobbyCode;
        private ChatServiceClient chatClient;
        private int playerCount = 4;
        private int boardId = 1;
        private DispatcherTimer pollingTimer;

        public LobbyPage(string username)
        {
            InitializeComponent();
            this.username = username;
            isHost = true;

            LobbyServiceManager.Instance.PlayerKicked += OnPlayerKicked;

            if (LobbyTabControl.Items.Count > 1)
            {
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;
            }

            StartMatchButton.IsEnabled = true;
            StartMatchButton.Opacity = 1.0;
            StartMatchButton.Content = GameClient.Resources.Strings.CreateLobbyButton ?? "CREAR SALA";
        }

        public LobbyPage(string username, string lobbyCode, JoinLobbyResultDTO joinResult)
        {
            InitializeComponent();
            this.username = username;
            this.lobbyCode = lobbyCode;
            boardId = joinResult.BoardId;
            playerCount = joinResult.MaxPlayers;
            isHost = false;

            LobbyServiceManager.Instance.PlayerKicked += OnPlayerKicked;

            SyncLobbyVisuals(joinResult.MaxPlayers, joinResult.BoardId, joinResult.IsPublic);
            LockLobbySettings(lobbyCode);
            StartMatchButton.Visibility = Visibility.Collapsed;

            UpdatePlayerListUI(joinResult.PlayersInLobby);

            this.Loaded += async (s, e) =>
            {
                await Task.Delay(500);
                InitializeTimer();
            };

            ConnectToChatService();
        }

        private void OnPlayerKicked(string reason)
        {
            Dispatcher.Invoke(() =>
            {
                pollingTimer?.Stop();
                CloseChatClient();

                LobbyServiceManager.Instance.PlayerKicked -= OnPlayerKicked;

                MessageBox.Show(reason, "Expulsado del Lobby", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (Window.GetWindow(this) is GameMainWindow mainWindow)
                {
                    mainWindow.ShowMainMenu();
                }
            });
        }

        private void InitializeTimer()
        {
            pollingTimer = new DispatcherTimer();
            pollingTimer.Interval = TimeSpan.FromSeconds(1);
            pollingTimer.Tick += async (s, e) => await PollLobbyState();
            pollingTimer.Start();
        }

        private async Task PollLobbyState()
        {
            pollingTimer.Stop();

            try
            {
                var state = await LobbyServiceManager.Instance.GetLobbyStateAsync(lobbyCode);

                if (state != null)
                {
                    if (state.IsGameStarted)
                    {
                        pollingTimer.Stop();
                        pollingTimer = null;

                        if (!isHost)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    NavigationService.Navigate(new BoardPage(lobbyCode, boardId, username));
                                }
                                catch { }
                            });
                        }
                        return;
                    }
                    else
                    {
                        UpdatePlayerListUI(state.Players);

                        if (!isHost)
                        {
                            SyncLobbyVisuals(state.MaxPlayers, state.BoardId, state.IsPublic);
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
            }
            catch (CommunicationException)
            {
            }
            catch { }
            finally
            {
                if (pollingTimer != null)
                {
                    pollingTimer.Start();
                }
            }
        }

        private void SyncLobbyVisuals(int maxPlayers, int currentBoardId, bool isPublic)
        {
            PlayerCountBlock.Text = maxPlayers.ToString();
            boardId = currentBoardId;

            if (currentBoardId == 2)
            {
                BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
                BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            }
            else
            {
                BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
                BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            }

            if (isPublic)
            {
                VisibilityPublicButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
                VisibilityPrivateButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            }
            else
            {
                VisibilityPrivateButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
                VisibilityPublicButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            pollingTimer?.Stop();

            LobbyServiceManager.Instance.PlayerKicked -= OnPlayerKicked;

            if (isLobbyCreated && isHost)
            {
                var result = MessageBox.Show("Se disolverá la sala. ¿Seguro?", "Salir", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    pollingTimer?.Start();
                    LobbyServiceManager.Instance.PlayerKicked += OnPlayerKicked;
                    return;
                }

                try { await LobbyServiceManager.Instance.DisbandLobbyAsync(username); } catch { }
            }
            else
            {
                try { await LobbyServiceManager.Instance.LeaveLobbyAsync(username); } catch { }
            }

            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }

            CloseChatClient();
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
            boardId = 2;
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            BoardTypeNormalButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            BoardTypeSpecialButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
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
            VisibilityPublicButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            VisibilityPrivateButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
        }

        private void VisibilityPrivateButton_Click(object sender, RoutedEventArgs e)
        {
            VisibilityPrivateButton.Style = (Style)FindResource("LobbyToggleActiveStyle");
            VisibilityPublicButton.Style = (Style)FindResource("LobbyToggleInactiveStyle");
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
            var settings = new LobbySettingsDTO
            {
                IsPublic = (VisibilityPublicButton.Style == (Style)FindResource("LobbyToggleActiveStyle")),
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
                    var initialPlayers = new PlayerLobbyDTO[] { new PlayerLobbyDTO { Username = username, IsHost = true } };
                    UpdatePlayerListUI(initialPlayers);
                    InitializeTimer();
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
                    return;
                }

                bool started = await LobbyServiceManager.Instance.StartGameAsync(lobbyCode);
                if (started)
                {
                    pollingTimer.Stop();
                    pollingTimer = null;
                    NavigationService.Navigate(new BoardPage(lobbyCode, boardId, username));
                }
                else
                {
                    MessageBox.Show("Error al iniciar.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
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
            catch { AddMessageToUI("[Sistema]:", "Error conectando chat."); }
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
            catch { AddMessageToUI("[Sistema]:", "Error enviando mensaje."); }
        }

        public void ReceiveMessage(string username, string message)
        {
            Dispatcher.Invoke(() => AddMessageToUI(username + ":", message));
        }

        private void AddMessageToUI(string name, string message)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            textBlock.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(" " + message);
            ChatMessagesList.Items.Add(textBlock);
            ChatMessagesList.ScrollIntoView(textBlock);
        }

        private void CloseChatClient() { try { chatClient?.Close(); } catch { chatClient?.Abort(); } }

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

        private void UpdatePlayerListUI(PlayerLobbyDTO[] players)
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

        private ListBoxItem CreatePlayerItem(PlayerLobbyDTO player)
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