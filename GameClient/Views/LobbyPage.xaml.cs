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

            LockLobbySettings(lobbyCode);
            StartMatchButton.Visibility = Visibility.Collapsed;

            UpdatePlayerListUI(joinResult.PlayersInLobby);
            InitializeTimer();
            ConnectToChatService();
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
                    MessageBox.Show("Tiempo de espera agotado.", "Error");
                }
                catch (EndpointNotFoundException)
                {
                    MessageBox.Show("No se encontró el servidor.", "Error");
                }
                catch (CommunicationException)
                {
                    MessageBox.Show("Error de comunicación.", "Error");
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
            if (isLobbyCreated == false)
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
                        MessageBox.Show($"Error: {result.ErrorMessage}", "Error");
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
                    MessageBox.Show("Tiempo de espera agotado.", "Error");
                    StartMatchButton.IsEnabled = true;
                }
                catch (EndpointNotFoundException)
                {
                    MessageBox.Show("No se encontró el servidor.", "Error");
                    StartMatchButton.IsEnabled = true;
                }
                catch (CommunicationException)
                {
                    MessageBox.Show("Error de comunicación.", "Error");
                    StartMatchButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Error");
                    StartMatchButton.IsEnabled = true;
                }
            }
            else
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
                        NavigationService.Navigate(new BoardPage(lobbyCode, boardId));
                    }
                    else
                    {
                        MessageBox.Show("Error al iniciar", "Error");
                    }
                }
                catch (TimeoutException)
                {
                    MessageBox.Show("Tiempo de espera agotado.", "Error");
                }
                catch (CommunicationException)
                {
                    MessageBox.Show("Error de comunicación.", "Error");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Error");
                }
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
                AddMessageToUI("[Sistema]:", "Error de comunicación.");
            }
            catch (TimeoutException)
            {
                AddMessageToUI("[Sistema]:", "Tiempo de espera agotado.");
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
            try
            {
                var state = await lobbyClient.GetLobbyStateAsync(lobbyCode);

                if (state.IsGameStarted)
                {
                    pollingTimer.Stop();
                    if (isHost)
                    {
                        MessageBox.Show("Error de estado");
                    }
                    else
                    {
                        NavigationService.Navigate(new BoardPage(lobbyCode, boardId));
                    }
                }
                else
                {
                    UpdatePlayerListUI(state.Players);
                }
            }
            catch (CommunicationException)
            {
                Console.WriteLine("Error de conexión al sondear.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error inesperado: " + ex.Message);
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
    }
}