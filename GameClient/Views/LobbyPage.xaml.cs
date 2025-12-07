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
using FontAwesome.WPF;
using GameClient.ChatServiceReference;
using GameClient.LobbyServiceReference;
using GameClient.Helpers;
using GameClient.Models;

namespace GameClient.Views
{
    public partial class LobbyPage : Page, IChatServiceCallback
    {
        private bool isLobbyCreated = false;
        private bool isHost = false;
        private string username;
        private string lobbyCode;
        private LobbyServiceClient lobbyClient;
        private ChatServiceClient chatClient;
        private int playerCount = 4;
        private int boardId = 1;
        private DispatcherTimer pollingTimer;

        public LobbyPage(string username)
        {
            InitializeComponent();
            this.username = username;
            isHost = true;
            lobbyClient = new LobbyServiceClient();

            if (LobbyTabControl.Items.Count > 1)
            {
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;
            }
        }

        public LobbyPage(string username, string lobbyCode, JoinLobbyResultDTO joinResult)
        {
            InitializeComponent();
            this.username = username;
            this.lobbyCode = lobbyCode;
            boardId = joinResult.BoardId;
            playerCount = joinResult.MaxPlayers;
            isHost = false;
            lobbyClient = new LobbyServiceClient();

            SyncLobbyVisuals(joinResult.MaxPlayers, joinResult.BoardId, joinResult.IsPublic);

            LockLobbySettings(lobbyCode);
            StartMatchButton.Visibility = Visibility.Collapsed;

            UpdatePlayerListUI(joinResult.PlayersInLobby);
            InitializeTimer();
            ConnectToChatService();
        }

        private void SyncLobbyVisuals(int maxPlayers, int currentBoardId, bool isPublic)
        {
            PlayerCountBlock.Text = maxPlayers.ToString();

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

            if (isLobbyCreated && isHost)
            {
                try
                {
                    await lobbyClient.DisbandLobbyAsync(username);
                }
                catch (TimeoutException)
                {
                    MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorLabel);
                }
                catch (EndpointNotFoundException)
                {
                    MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorLabel);
                }
                catch (CommunicationException)
                {
                    MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorLabel);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error fatal: " + ex.Message);
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
            if (playerCount > 2)
            {
                playerCount--;
                PlayerCountBlock.Text = playerCount.ToString();
            }
        }

        private void IncreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (playerCount < 4)
            {
                playerCount++;
                PlayerCountBlock.Text = playerCount.ToString();
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
            if (!isLobbyCreated)
            {
                await HandleCreateLobbyAsync();
            }
            else
            {
                await HandleStartGameAsync();
            }
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
                var result = await lobbyClient.CreateLobbyAsync(settings, username);

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
                    MessageBox.Show($"Error: {result.ErrorMessage}", GameClient.Resources.Strings.ErrorLabel);
                    if (result.ErrorMessage.Contains("already in a game"))
                    {
                        try
                        {
                            await lobbyClient.DisbandLobbyAsync(username);
                        }
                        catch (CommunicationException) { }
                        catch (Exception) { }
                    }
                    StartMatchButton.IsEnabled = true;
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorLabel);
                StartMatchButton.IsEnabled = true;
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorLabel);
                StartMatchButton.IsEnabled = true;
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorLabel);
                StartMatchButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, GameClient.Resources.Strings.ErrorLabel);
                StartMatchButton.IsEnabled = true;
            }
        }

        private async Task HandleStartGameAsync()
        {
            int currentPlayers = PlayerList.Items.OfType<ListBoxItem>()
                    .Count(item => !((TextBlock)((StackPanel)item.Content).Children[1]).Text.Contains("Slot Vacío"));

            if (currentPlayers < 2)
            {
                MessageBox.Show("Se necesitan al menos 2 jugadores para iniciar la partida.", "Aviso");
                return;
            }

            try
            {
                bool started = await lobbyClient.StartGameAsync(lobbyCode);
                if (started)
                {
                    pollingTimer.Stop();
                    NavigationService.Navigate(new BoardPage(lobbyCode, boardId, username));
                }
                else
                {
                    MessageBox.Show("Error al iniciar", GameClient.Resources.Strings.ErrorLabel);
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorLabel);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorLabel);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, GameClient.Resources.Strings.ErrorLabel);
            }
        }

        private void ConnectToChatService()
        {
            try
            {
                InstanceContext context = new InstanceContext(this);
                chatClient = new ChatServiceClient(context);
                chatClient.JoinLobbyChat(username, lobbyCode);

                ChatMessageTextBox.KeyDown += ChatMessageTextBox_KeyDown;
            }
            catch (Exception ex)
            {
                AddMessageToUI("[Sistema]:", "Error chat: " + ex.Message);
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
            if (string.IsNullOrWhiteSpace(ChatMessageTextBox.Text) || chatClient == null)
            {
                return;
            }

            try
            {
                chatClient.SendLobbyMessage(username, lobbyCode, ChatMessageTextBox.Text);
                AddMessageToUI("Tú:", ChatMessageTextBox.Text);
                ChatMessageTextBox.Clear();
            }
            catch (CommunicationException)
            {
                AddMessageToUI("[Sistema]:", GameClient.Resources.Strings.ComunicationLabel);
            }
            catch (TimeoutException)
            {
                AddMessageToUI("[Sistema]:", GameClient.Resources.Strings.TimeoutLabel);
            }
            catch (Exception ex)
            {
                AddMessageToUI("[Sistema]:", "Error: " + ex.Message);
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
                if (chatClient != null && chatClient.State == CommunicationState.Opened)
                {
                    chatClient.Close();
                }
            }
            catch (Exception)
            {
                chatClient.Abort();
            }
            await Task.CompletedTask;
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

            isLobbyCreated = true;
            TitleBlock.Text = $"CÓDIGO: {lobbyCode}";

            CopyCodeButton.Visibility = Visibility.Visible;
        }

        private async Task CloseClientAsync()
        {
            try
            {
                if (lobbyClient.State == CommunicationState.Opened)
                {
                    lobbyClient.Close();
                }
            }
            catch (Exception)
            {
                lobbyClient.Abort();
            }
            await Task.CompletedTask;
        }

        private async void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(lobbyCode);

            CopyIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Check;
            CopyCodeButton.ToolTip = "¡Copiado!";

            await Task.Delay(2000);

            CopyIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Copy;
            CopyCodeButton.ToolTip = "Copiar";
        }

        private void InitializeTimer()
        {
            pollingTimer = new DispatcherTimer();
            pollingTimer.Interval = TimeSpan.FromSeconds(3);
            pollingTimer.Tick += async (s, e) => await PollLobbyState();
            pollingTimer.Start();
        }

        private async Task PollLobbyState()
        {
            pollingTimer.Stop();
            bool shouldContinuePolling = true;

            try
            {
                if (lobbyClient == null ||
                    lobbyClient.State == CommunicationState.Faulted ||
                    lobbyClient.State == CommunicationState.Closed)
                {
                    lobbyClient = new LobbyServiceClient();
                }

                var state = await lobbyClient.GetLobbyStateAsync(lobbyCode);

                if (state == null)
                {
                    Console.WriteLine("El estado del lobby es nulo.");
                    return;
                }

                if (state.IsGameStarted)
                {
                    shouldContinuePolling = false;

                    if (isHost)
                    {
                        Console.WriteLine("El juego inició (detectado por polling en Host).");
                    }
                    else
                    {
                        // Cliente: Navegar a la partida
                        NavigationService.Navigate(new BoardPage(lobbyCode, boardId, username));
                    }
                }
                else
                {
                    if (state.Players != null) 
                    {
                        UpdatePlayerListUI(state.Players);
                    }
                    SyncLobbyVisuals(state.MaxPlayers, state.BoardId, state.IsPublic);
                }
            }
            catch (EndpointNotFoundException)
            {
                Console.WriteLine("Servidor no encontrado. Reintentando...");
            }
            catch (CommunicationException)
            {
                Console.WriteLine("Problema de conexión. Reintentando...");
                if (lobbyClient != null) lobbyClient.Abort();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error inesperado en Polling: " + ex.Message);
            }
            finally
            {
                if (shouldContinuePolling)
                {
                    pollingTimer.Start();
                }
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

            int emptySlots = playerCount - slotsFilled;
            for (int i = 0; i < emptySlots; i++)
            {
                PlayerList.Items.Add(CreateEmptySlotItem());
            }

            PlayersTabHeader.Text = $"JUGADORES ({slotsFilled}/{playerCount})";
        }

        private ListBoxItem CreatePlayerItem(PlayerLobbyDTO player)
        {
            var textBlock = new TextBlock
            {
                Text = player.Username,
                FontSize = 22,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (player.IsHost)
            {
                textBlock.Text += " (Host)";
                textBlock.FontWeight = FontWeights.Bold;
            }

            if (player.Username == username)
            {
                textBlock.Text += " (Tú)";
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
            var textBlock = new TextBlock
            {
                Text = "Slot Vacío",
                FontSize = 22,
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

        private void OpenInviteMenu_Click(object sender, RoutedEventArgs e)
        {
            InviteFriendsOverlay.Visibility = Visibility.Visible;
            LoadOnlineFriends();
        }

        private void CloseInviteMenu_Click(object sender, RoutedEventArgs e)
        {
            InviteFriendsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void LoadOnlineFriends()
        {
            if (FriendshipServiceManager.Instance == null) return;

            var allFriends = await FriendshipServiceManager.Instance.GetFriendListAsync();

            var onlineFriends = allFriends.Where(f => f.IsOnline).ToList();

            InviteFriendsList.ItemsSource = onlineFriends;

            NoFriendsToInviteText.Visibility = onlineFriends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InviteFriend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string friendUsername = btn.Tag.ToString();

            FriendshipServiceManager.Instance.SendGameInvitation(friendUsername, this.lobbyCode);

            MessageBox.Show($"Invitación enviada a {friendUsername}.", "Invitado", MessageBoxButton.OK, MessageBoxImage.Information);

            btn.IsEnabled = false;
            btn.Content = "Enviado";
        }
    }
}