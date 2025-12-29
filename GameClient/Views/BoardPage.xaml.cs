using GameClient.ChatServiceReference;
using GameClient.GameplayServiceReference;
using GameClient.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace GameClient.Views
{
    [CallbackBehavior(UseSynchronizationContext = false)]
    public partial class BoardPage : Page, IGameplayServiceCallback, IChatServiceCallback
    {
        private string lobbyCode;
        private int boardId;
        private string currentUsername;
        private bool _isGuest = false;

        private GameplayServiceClient gameplayClient;
        private ChatServiceClient chatClient;

        private DispatcherTimer gameLoopTimer;
        private DispatcherTimer _startCountdownTimer;
        private DispatcherTimer _turnCountdownTimer;
        private int _turnSecondsRemaining = 20;
        private string _lastTurnUsername = "";

        private int _countdownValue = 5;
        private bool _isGameStarting = true;
        private bool _isGameOverHandled = false;
        private Dictionary<string, UIElement> _playerTokens = new Dictionary<string, UIElement>();

        private int _luckyBoxClicks = 0;
        private string _currentRewardType = "";
        private int _currentRewardAmount = 0;
        private string _lastLogProcessed = "";

        private readonly string[] _tokenImagePaths =
        {
            "/Assets/Game Pieces/red_piece.png",
            "/Assets/Game Pieces/blue_piece.png",
            "/Assets/Game Pieces/green_piece.png",
            "/Assets/Game Pieces/yellow_piece.png"
        };

        private readonly List<Point> _tileCoordinates = new List<Point>
        {
            new Point(489, 592), new Point(439, 594), new Point(393, 577), new Point(351, 540),
            new Point(330, 494), new Point(326, 434), new Point(326, 377), new Point(327, 319),
            new Point(336, 266), new Point(363, 210), new Point(403, 179), new Point(446, 168),
            new Point(498, 167), new Point(550, 168), new Point(600, 165), new Point(647, 170),
            new Point(695, 170), new Point(741, 171), new Point(790, 179), new Point(831, 214),
            new Point(855, 264), new Point(866, 329), new Point(863, 391), new Point(861, 470),
            new Point(840, 538), new Point(798, 578), new Point(752, 595), new Point(746, 530),
            new Point(695, 537), new Point(651, 536), new Point(603, 534), new Point(552, 533),
            new Point(505, 533), new Point(452, 525), new Point(411, 500), new Point(387, 440),
            new Point(388, 382), new Point(388, 315), new Point(410, 257), new Point(462, 243),
            new Point(507, 245), new Point(561, 244), new Point(604, 248), new Point(653, 244),
            new Point(702, 244), new Point(744, 249), new Point(787, 273), new Point(802, 328),
            new Point(802, 390), new Point(804, 443), new Point(727, 448), new Point(677, 463),
            new Point(627, 464), new Point(582, 461), new Point(532, 461), new Point(483, 458),
            new Point(448, 419), new Point(447, 366), new Point(474, 311), new Point(512, 318),
            new Point(563, 319), new Point(625, 318), new Point(690, 319), new Point(739, 327),
            new Point(611, 393)
        };

        public BoardPage(string lobbyCode, int boardId, string username)
        {
            InitializeComponent();
            this.lobbyCode = lobbyCode;
            this.boardId = boardId;
            this.currentUsername = username;

            if (username.ToLower().StartsWith("guest") || username.Contains("Invitado"))
            {
                _isGuest = true;
            }

            InstanceContext gameplayContext = new InstanceContext(this);
            gameplayClient = new GameplayServiceClient(gameplayContext);

            ConnectToChatService();
            LoadBoardImage();
            StartCountdown();

            _turnCountdownTimer = new DispatcherTimer();
            _turnCountdownTimer.Interval = TimeSpan.FromSeconds(1);
            _turnCountdownTimer.Tick += TurnCountdown_Tick;

            this.Unloaded += (s, e) =>
            {
                gameLoopTimer?.Stop();
                _startCountdownTimer?.Stop();
                _turnCountdownTimer?.Stop();
                try { gameplayClient?.Close(); } catch { gameplayClient?.Abort(); }
                try { chatClient?.Close(); } catch { chatClient?.Abort(); }
            };
        }

        private void ConnectToChatService()
        {
            try
            {
                InstanceContext chatContext = new InstanceContext(this);
                chatClient = new ChatServiceClient(chatContext);
                var request = new JoinChatRequest { Username = currentUsername, LobbyCode = lobbyCode };
                chatClient.JoinLobbyChat(request);
            }
            catch (CommunicationException) { }
            catch (TimeoutException) { }
        }

        public void ReceiveMessage(ChatMessageDto message)
        {
            Dispatcher.Invoke(() =>
            {
                string tabName = "General";
                if (message.IsPrivate)
                {
                    tabName = (message.Sender == currentUsername) ? message.TargetUser : message.Sender;
                }

                ListBox targetList = GetOrCreateTab(tabName);
                string displayMsg = $"{message.Sender}: {message.Message}";
                targetList.Items.Add(displayMsg);
                targetList.ScrollIntoView(targetList.Items[targetList.Items.Count - 1]);
            });
        }

        private ListBox GetOrCreateTab(string tabName)
        {
            foreach (TabItem item in ChatTabControl.Items)
            {
                if (item.Tag?.ToString() == tabName)
                {
                    return (ListBox)item.Content;
                }
            }

            var newListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 0)
            };

            if (GeneralChatList.ItemTemplate != null)
                newListBox.ItemTemplate = GeneralChatList.ItemTemplate;

            var newTab = new TabItem
            {
                Header = tabName,
                Tag = tabName,
                Content = newListBox,
                Style = (Style)FindResource("ChatTabItemStyle")
            };

            ChatTabControl.Items.Add(newTab);
            return newListBox;
        }

        private void SendChatButton_Click(object sender, RoutedEventArgs e)
        {
            string msg = ChatInputBox.Text;
            if (string.IsNullOrWhiteSpace(msg)) return;

            var selectedTab = ChatTabControl.SelectedItem as TabItem;
            string target = selectedTab?.Tag?.ToString() ?? "General";

            var dto = new ChatMessageDto
            {
                Sender = currentUsername,
                LobbyCode = lobbyCode,
                Message = msg
            };

            ReceiveMessage(dto);

            try
            {
                if (target == "General")
                {
                    dto.IsPrivate = false;
                    chatClient.SendLobbyMessage(dto);
                }
                else
                {
                    dto.IsPrivate = true;
                    dto.TargetUser = target;
                    chatClient.SendPrivateMessage(dto);
                }
                ChatInputBox.Clear();
            }
            catch (CommunicationException) { MessageBox.Show("Error de conexión con el chat."); }
            catch (TimeoutException) { MessageBox.Show("El chat no responde."); }
        }

        private void ChatInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendChatButton_Click(sender, e);
        }

        private void PrivateChatMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest)
            {
                MessageBox.Show("Los invitados no pueden enviar mensajes privados.", "Restricción");
                return;
            }

            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var panel = contextMenu?.PlacementTarget as Border;
            string targetUser = panel?.Tag?.ToString();

            if (!string.IsNullOrEmpty(targetUser) && targetUser != currentUsername)
            {
                GetOrCreateTab(targetUser);
                foreach (TabItem item in ChatTabControl.Items)
                {
                    if (item.Tag?.ToString() == targetUser)
                    {
                        ChatTabControl.SelectedItem = item;
                        break;
                    }
                }
                ChatInputBox.Focus();
            }
        }

        private async void AddFriendMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest)
            {
                MessageBox.Show("Las cuentas de invitado no pueden agregar amigos.", "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var panel = contextMenu?.PlacementTarget as Border;
            string targetUser = panel?.Tag?.ToString();

            if (string.IsNullOrEmpty(targetUser) || targetUser == currentUsername) return;

            try
            {
                bool sent = await FriendshipServiceManager.Instance.SendFriendRequestAsync(targetUser);
                if (sent) MessageBox.Show($"Solicitud enviada a {targetUser}.", "Amigos");
                else MessageBox.Show($"No se pudo enviar la solicitud a {targetUser}.", "Error");
            }
            catch (CommunicationException) { MessageBox.Show("Error de conexión al agregar amigo."); }
            catch (TimeoutException) { MessageBox.Show("El servidor no respondió a tiempo."); }
        }

        public void OnVoteKickStarted(string targetUsername, string reason)
        {
            if (targetUsername == currentUsername) return;

            Dispatcher.Invoke(() =>
            {
                VoteKickTargetText.Text = $"¿Expulsar a '{targetUsername}'?\nMotivo: {reason}";
                VoteKickTargetText.Tag = targetUsername;
                VoteKickOverlay.Visibility = Visibility.Visible;
                Panel.SetZIndex(VoteKickOverlay, 999);
            });
        }

        public void OnPlayerKicked(string reason)
        {
            Dispatcher.Invoke(() =>
            {
                gameLoopTimer?.Stop();
                _startCountdownTimer?.Stop();
                _turnCountdownTimer?.Stop();
                _isGameOverHandled = true;

                MessageBox.Show(reason, "Expulsado", MessageBoxButton.OK, MessageBoxImage.Warning);

                var mainWindow = Window.GetWindow(this) as GameMainWindow;
                if (mainWindow != null) mainWindow.ShowMainMenu();
                else
                {
                    GameMainWindow newWindow = new GameMainWindow(currentUsername);
                    newWindow.Show();
                    Window.GetWindow(this)?.Close();
                }
            });
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            PauseMenuOverlay.Visibility = Visibility.Visible;
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            PauseMenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void PauseMenuOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == sender) PauseMenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void IngameScreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow == null || !IsLoaded) return;

            int index = IngameScreenModeCombo.SelectedIndex;
            switch (index)
            {
                case 0:
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Maximized;
                    mainWindow.ResizeMode = ResizeMode.NoResize;
                    break;
                case 1:
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.NoResize;
                    mainWindow.Width = 1280; mainWindow.Height = 720; mainWindow.CenterWindow();
                    break;
                case 2:
                    mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.CanResize;
                    break;
            }
        }

        private async void QuitGameButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Seguro que quieres abandonar? Perderás la partida.", "Abandonar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            PauseMenuOverlay.Visibility = Visibility.Collapsed;
            gameLoopTimer?.Stop();
            _startCountdownTimer?.Stop();
            _turnCountdownTimer?.Stop();
            _isGameOverHandled = true;

            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                await gameplayClient.LeaveGameAsync(request);
            }
            catch (CommunicationException) { }
            catch (TimeoutException) { }
            finally
            {
                var mainWindow = Window.GetWindow(this) as GameMainWindow;
                if (mainWindow != null) mainWindow.ShowMainMenu();
                else if (NavigationService.CanGoBack) NavigationService.GoBack();
            }
        }

        private void StartCountdown()
        {
            _isGameStarting = true;
            StartTimerPanel.Visibility = Visibility.Visible;
            RollDiceButton.IsEnabled = false;
            RollDiceButton.Opacity = 0.5;

            _startCountdownTimer = new DispatcherTimer();
            _startCountdownTimer.Interval = TimeSpan.FromSeconds(1);
            _startCountdownTimer.Tick += Countdown_Tick;
            _startCountdownTimer.Start();

            StartGameLoop();
        }

        private void Countdown_Tick(object sender, EventArgs e)
        {
            _countdownValue--;
            if (_countdownValue > 0) StartTimerText.Text = _countdownValue.ToString();
            else
            {
                _startCountdownTimer.Stop();
                StartTimerPanel.Visibility = Visibility.Collapsed;
                _isGameStarting = false;
                _ = UpdateGameState();
            }
        }

        private void TurnCountdown_Tick(object sender, EventArgs e)
        {
            _turnSecondsRemaining--;
            TurnTimerText.Text = $"Tiempo: {_turnSecondsRemaining}s";
            TurnTimerText.Foreground = _turnSecondsRemaining <= 5 ? Brushes.Red : Brushes.White;
            if (_turnSecondsRemaining <= 0)
            {
                _turnCountdownTimer.Stop();
                TurnTimerText.Text = "¡Tiempo Agotado!";
            }
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == 1) ? "/Assets/Boards/normal_board.png" : "/Assets/Boards/special_board.png";
            try { BoardImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)); } catch { }
        }

        private void StartGameLoop()
        {
            gameLoopTimer = new DispatcherTimer();
            gameLoopTimer.Interval = TimeSpan.FromSeconds(2);
            gameLoopTimer.Tick += async (s, e) => await UpdateGameState();
            gameLoopTimer.Start();
        }

        private async Task UpdateGameState()
        {
            if (_isGameOverHandled) return;
            gameLoopTimer.Stop();

            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                var state = await gameplayClient.GetGameStateAsync(request);

                if (state != null)
                {
                    if (state.IsGameOver)
                    {
                        _isGameOverHandled = true;
                        HandleGameOver(state.WinnerUsername);
                        return;
                    }

                    ProcessGameState(state);
                }
            }
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in Game Loop: " + ex.Message);
            }
            finally
            {
                if (!_isGameOverHandled) gameLoopTimer.Start();
            }
        }

        private void ProcessGameState(GameStateDTO state)
        {
            UpdateTurnUI(state);
            UpdateTurnTimer(state.CurrentTurnUsername);
            UpdateDiceVisuals(state.LastDiceOne, state.LastDiceTwo);
            UpdateGameLog(state.GameLog);
            UpdateBoardVisuals(state.PlayerPositions);
            UpdatePlayerAvatars(state.PlayerPositions);
            ProcessLatestLog(state.GameLog);
        }

        private void UpdateTurnTimer(string currentTurnUsername)
        {
            if (currentTurnUsername != _lastTurnUsername)
            {
                _lastTurnUsername = currentTurnUsername;
                _turnSecondsRemaining = 20;
                TurnTimerPanel.Visibility = Visibility.Visible;
                TurnTimerText.Text = "Tiempo: 20s";
                TurnTimerText.Foreground = Brushes.White;
                _turnCountdownTimer.Start();
            }
        }

        private void UpdateDiceVisuals(int dice1, int dice2)
        {
            if (dice1 > 0)
            {
                DiceOneText.Text = dice1.ToString();
                DiceTwoText.Text = dice2.ToString();
            }
        }

        private void ProcessLatestLog(string[] logs)
        {
            if (logs != null && logs.Any())
            {
                string latestLog = logs.First();
                if (latestLog != _lastLogProcessed)
                {
                    _lastLogProcessed = latestLog;
                    CheckForLuckyBox(latestLog);
                }
            }
        }

        private async void RollDiceButton_Click(object sender, RoutedEventArgs e)
        {
            RollDiceButton.IsEnabled = false;
            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                var result = await gameplayClient.RollDiceAsync(request);
                if (result != null)
                {
                    DiceOneText.Text = result.DiceOne.ToString();
                    DiceTwoText.Text = result.DiceTwo.ToString();
                    await UpdateGameState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al tirar dados: " + ex.Message);
                RollDiceButton.IsEnabled = true;
            }
        }

        private void UpdatePlayerAvatars(PlayerPositionDTO[] players)
        {
            if (players == null) return;

            HideAllPlayerPanels();

            var sortedPlayers = players.OrderBy(p => p.Username).ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                var controls = GetPlayerUIControls(i);

                if (controls.Panel != null)
                {
                    ConfigurePlayerPanel(controls, player);
                }
            }
        }

        private void HideAllPlayerPanels()
        {
            Player1Panel.Visibility = Visibility.Hidden;
            Player2Panel.Visibility = Visibility.Hidden;
            Player3Panel.Visibility = Visibility.Hidden;
            Player4Panel.Visibility = Visibility.Hidden;
        }

        private (ImageBrush Avatar, TextBlock Name, Border Panel) GetPlayerUIControls(int index)
        {
            switch (index)
            {
                case 0: return (Player1Avatar, Player1Name, Player1Panel);
                case 1: return (Player2Avatar, Player2Name, Player2Panel);
                case 2: return (Player3Avatar, Player3Name, Player3Panel);
                case 3: return (Player4Avatar, Player4Name, Player4Panel);
                default: return (null, null, null);
            }
        }

        private void ConfigurePlayerPanel((ImageBrush Avatar, TextBlock Name, Border Panel) controls, PlayerPositionDTO player)
        {
            controls.Panel.Visibility = Visibility.Visible;
            controls.Name.Text = player.Username;
            controls.Panel.Tag = player.Username;

            if (_isGuest && controls.Panel.ContextMenu != null)
            {
                controls.Panel.ContextMenu = null;
            }

            LoadAvatarImage(controls.Avatar, player.AvatarPath);

            controls.Panel.BorderBrush = player.IsMyTurn ? Brushes.Gold : Brushes.Transparent;
            controls.Panel.BorderThickness = new Thickness(player.IsMyTurn ? 3 : 0);
        }

        private void LoadAvatarImage(ImageBrush brush, string avatarPath)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;

            try
            {
                bitmap.UriSource = GetAvatarUri(avatarPath);
            }
            catch (Exception)
            {
                bitmap.UriSource = new Uri("/Assets/default_avatar.png", UriKind.Relative);
            }

            bitmap.EndInit();
            brush.Stretch = Stretch.UniformToFill;
            brush.ImageSource = bitmap;
        }

        private Uri GetAvatarUri(string path)
        {
            if (string.IsNullOrEmpty(path)) return new Uri("/Assets/default_avatar.png", UriKind.Relative);
            if (path.StartsWith("pack://")) return new Uri(path, UriKind.RelativeOrAbsolute);

            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatar", path);
            return File.Exists(fullPath) ? new Uri(fullPath, UriKind.Absolute) : new Uri("/Assets/default_avatar.png", UriKind.Relative);
        }

        private void UpdateBoardVisuals(PlayerPositionDTO[] players)
        {
            if (players == null) return;
            var sortedPlayers = players.OrderBy(p => p.Username).ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                if (!_playerTokens.ContainsKey(player.Username))
                {
                    string imagePath = _tokenImagePaths[i % _tokenImagePaths.Length];
                    var token = CreatePlayerToken(player.Username, imagePath);
                    _playerTokens.Add(player.Username, token);
                    BoardCanvas.Children.Add(token);
                }
                var tokenUI = _playerTokens[player.Username];
                MoveTokenToTile(tokenUI, player.CurrentTile);
                tokenUI.Opacity = player.IsOnline ? 1.0 : 0.4;
            }
        }

        private UIElement CreatePlayerToken(string name, string imagePath)
        {
            var image = new Image
            {
                Width = 40,
                Height = 40,
                Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
                ToolTip = name,
                Stretch = Stretch.Uniform
            };
            Canvas.SetLeft(image, _tileCoordinates[0].X - 20);
            Canvas.SetTop(image, _tileCoordinates[0].Y - 20);
            image.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Direction = 320, ShadowDepth = 4, Opacity = 0.5 };
            return image;
        }

        private void MoveTokenToTile(UIElement token, int tileIndex)
        {
            if (tileIndex < 0) tileIndex = 0;
            if (tileIndex >= _tileCoordinates.Count) tileIndex = _tileCoordinates.Count - 1;
            Point targetPoint = _tileCoordinates[tileIndex];

            var animX = new DoubleAnimation { From = Canvas.GetLeft(token), To = targetPoint.X - 20, Duration = TimeSpan.FromMilliseconds(500) };
            var animY = new DoubleAnimation { From = Canvas.GetTop(token), To = targetPoint.Y - 20, Duration = TimeSpan.FromMilliseconds(500) };
            token.BeginAnimation(Canvas.LeftProperty, animX);
            token.BeginAnimation(Canvas.TopProperty, animY);
        }

        private void UpdateTurnUI(GameStateDTO state)
        {
            if (_isGameStarting)
            {
                RollDiceButton.IsEnabled = false; RollDiceButton.Content = "Esperando inicio..."; RollDiceButton.Opacity = 0.5;
                return;
            }
            if (state.IsMyTurn) { RollDiceButton.IsEnabled = true; RollDiceButton.Content = "¡Tirar Dados!"; RollDiceButton.Opacity = 1; }
            else { RollDiceButton.IsEnabled = false; RollDiceButton.Content = $"Turno de {state.CurrentTurnUsername}"; RollDiceButton.Opacity = 0.6; }
        }

        private void UpdateGameLog(string[] logs)
        {
            if (logs == null) return;
            if (GameLogListBox.Items.Count != logs.Length)
            {
                GameLogListBox.Items.Clear();
                foreach (var logRaw in logs)
                {
                    string textToShow = CleanLogMessage(logRaw);
                    GameLogListBox.Items.Add(textToShow);
                }
                if (GameLogListBox.Items.Count > 0) GameLogListBox.ScrollIntoView(GameLogListBox.Items[GameLogListBox.Items.Count - 1]);
            }
        }

        private string CleanLogMessage(string raw)
        {
            string clean = raw;
            if (clean.Contains("[LUCKYBOX:"))
            {
                int start = clean.IndexOf("[LUCKYBOX:");
                int end = clean.IndexOf("]", start);
                if (start != -1 && end != -1)
                {
                    string tag = clean.Substring(start, end - start + 1);
                    clean = clean.Replace(tag, "").Trim();
                }
            }
            return clean.Replace("[EXTRA]", "").Trim();
        }

        private void HandleGameOver(string winner)
        {
            MessageBox.Show($"¡Juego Terminado!\n\nGanador: {winner}", "Fin de Partida", MessageBoxButton.OK, MessageBoxImage.Information);
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null) mainWindow.ShowMainMenu();
            else if (NavigationService.CanGoBack) NavigationService.GoBack();
        }

        private void CheckForLuckyBox(string logDescription)
        {
            if (string.IsNullOrEmpty(logDescription)) return;

            string boxOwner, rewardType;
            int rewardAmount;

            if (TryParseLuckyBoxTag(logDescription, out boxOwner, out rewardType, out rewardAmount))
            {
                if (boxOwner == currentUsername)
                {
                    ShowLuckyBoxUI(rewardType, rewardAmount);
                }
            }
        }

        private bool TryParseLuckyBoxTag(string log, out string owner, out string type, out int amount)
        {
            owner = type = "";
            amount = 0;
            int start = log.IndexOf("[LUCKYBOX:");
            int end = log.IndexOf("]", start);

            if (start == -1 || end == -1) return false;

            try
            {
                string content = log.Substring(start, end - start + 1).Replace("[LUCKYBOX:", "").Replace("]", "");
                string[] parts = content.Split(':');
                if (parts.Length != 2) return false;

                owner = parts[0];
                string[] rewardParts = parts[1].Split('_');
                if (rewardParts.Length != 2) return false;

                type = rewardParts[0];
                amount = int.Parse(rewardParts[1]);
                return true;
            }
            catch { return false; }
        }

        private void ShowLuckyBoxUI(string type, int amount)
        {
            _currentRewardType = type;
            _currentRewardAmount = amount;
            _luckyBoxClicks = 0;

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "luckybox_closed.png");
                LuckyBoxImage.Source = File.Exists(path)
                    ? new BitmapImage(new Uri(path, UriKind.Absolute))
                    : new BitmapImage(new Uri("/Assets/Images/luckybox_closed.png", UriKind.Relative));
            }
            catch { }

            LuckyBoxImage.Visibility = Visibility.Visible;
            RewardContainer.Visibility = Visibility.Collapsed;
            OpenBoxButton.IsEnabled = true;
            LuckyBoxOverlay.Visibility = Visibility.Visible;
        }

        private async void OpenBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _luckyBoxClicks++;
            var shakeAnim = this.Resources["ShakeAnimation"] as Storyboard ?? LuckyBoxOverlay.Resources["ShakeAnimation"] as Storyboard;

            if (_luckyBoxClicks < 3)
            {
                shakeAnim?.Begin();
            }
            else
            {
                OpenBoxButton.IsEnabled = false;
                LuckyBoxImage.Visibility = Visibility.Collapsed;
                SetRewardVisuals();
                RewardContainer.Visibility = Visibility.Visible;

                var revealAnim = this.Resources["RevealAnimation"] as Storyboard ?? LuckyBoxOverlay.Resources["RevealAnimation"] as Storyboard;
                revealAnim?.Begin();

                await Task.Delay(3000);
                LuckyBoxOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void SetRewardVisuals()
        {
            string imagePath = "";
            string text = "PREMIO";
            SolidColorBrush textColor = Brushes.White;

            switch (_currentRewardType)
            {
                case "COINS": imagePath = "coin_pile.png"; text = $"+{_currentRewardAmount} ORO"; textColor = Brushes.Gold; break;
                case "COMMON": imagePath = "ticket_common.png"; text = "TICKET COMÚN"; textColor = Brushes.White; break;
                case "EPIC": imagePath = "ticket_epic.png"; text = "TICKET ÉPICO"; textColor = Brushes.Purple; break;
                case "LEGENDARY": imagePath = "ticket_legendary.png"; text = "¡LEGENDARIO!"; textColor = Brushes.OrangeRed; break;
            }

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", imagePath);
                    RewardImage.Source = File.Exists(fullPath)
                        ? new BitmapImage(new Uri(fullPath, UriKind.Absolute))
                        : new BitmapImage(new Uri($"/Assets/Images/{imagePath}", UriKind.Relative));
                }
                catch { }
            }
            RewardText.Text = text;
            RewardText.Foreground = textColor;
        }

        private void LuckyBoxOverlay_MouseDown(object sender, MouseButtonEventArgs e) { e.Handled = true; }

        private void VoteKickMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest)
            {
                MessageBox.Show("Los invitados no pueden iniciar votaciones.", "Restricción");
                return;
            }

            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var panel = contextMenu?.PlacementTarget as Border;
            if (panel == null || panel.Tag == null) return;

            string targetUser = panel.Tag.ToString();
            if (targetUser == currentUsername)
            {
                MessageBox.Show("No puedes iniciar una votación contra ti mismo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ReasonSelectorOverlay.Tag = targetUser;
            ReasonSelectorOverlay.Visibility = Visibility.Visible;
        }

        private async void ConfirmKickButton_Click(object sender, RoutedEventArgs e)
        {
            string targetUser = ReasonSelectorOverlay.Tag?.ToString();
            string reason = (KickReasonCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Sin razón";
            ReasonSelectorOverlay.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(targetUser)) return;

            try
            {
                var request = new VoteRequestDTO { Username = currentUsername, TargetUsername = targetUser, Reason = reason };
                await gameplayClient.InitiateVoteKickAsync(request);
                MessageBox.Show($"Has iniciado una votación para expulsar a {targetUser}.", "Votación Iniciada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar votación: " + ex.Message);
            }
        }

        private void CancelKickButton_Click(object sender, RoutedEventArgs e) { ReasonSelectorOverlay.Visibility = Visibility.Collapsed; }
        private async void VoteYes_Click(object sender, RoutedEventArgs e) { await SendVote(true); }
        private async void VoteNo_Click(object sender, RoutedEventArgs e) { await SendVote(false); }

        private async Task SendVote(bool acceptKick)
        {
            VoteKickOverlay.Visibility = Visibility.Collapsed;
            string targetUser = VoteKickTargetText.Tag?.ToString();
            if (string.IsNullOrEmpty(targetUser)) return;

            try
            {
                var response = new VoteResponseDTO { Username = currentUsername, AcceptKick = acceptKick };
                await gameplayClient.CastVoteAsync(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error enviando voto: " + ex.Message);
            }
        }
    }
}