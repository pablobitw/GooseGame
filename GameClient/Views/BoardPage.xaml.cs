using GameClient.ChatServiceReference;
using GameClient.GameplayServiceReference;
using GameClient.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public partial class BoardPage : Page, IChatServiceCallback
    {
        private const string LuckyBoxTagPrefix = "[LUCKYBOX:";
        private string lobbyCode;
        private int boardId;
        private string currentUsername;
        private bool _isGuest = false;

        private ChatServiceClient chatClient;

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

            SubscribeToEvents();

            ConnectToChatService();
            LoadBoardImage();
            StartCountdown();

            _turnCountdownTimer = new DispatcherTimer();
            _turnCountdownTimer.Interval = TimeSpan.FromSeconds(1);
            _turnCountdownTimer.Tick += TurnCountdown_Tick;

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
            StopTimers();
            UnsubscribeFromEvents();
            CloseChatClient();
            this.CommandBindings.Clear();
        }

        private void SubscribeToEvents()
        {
            GameplayServiceManager.Instance.TurnChanged += OnTurnChanged;
            GameplayServiceManager.Instance.GameFinished += OnGameFinished;
            GameplayServiceManager.Instance.PlayerKicked += OnPlayerKicked;
            GameplayServiceManager.Instance.VoteKickStarted += OnVoteKickStarted;
        }

        private void UnsubscribeFromEvents()
        {
            GameplayServiceManager.Instance.TurnChanged -= OnTurnChanged;
            GameplayServiceManager.Instance.GameFinished -= OnGameFinished;
            GameplayServiceManager.Instance.PlayerKicked -= OnPlayerKicked;
            GameplayServiceManager.Instance.VoteKickStarted -= OnVoteKickStarted;
        }

        private void OnTurnChanged(GameStateDto newState)
        {
            Dispatcher.Invoke(() => ProcessGameState(newState));
        }

        private void OnGameFinished(string winner)
        {
            Dispatcher.Invoke(() => HandleGameOver(winner));
        }

        private void OnPlayerKicked(string reason)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                StopTimers();
                _isGameOverHandled = true;
                MessageBox.Show(reason, "Expulsado", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (Window.GetWindow(this) is GameMainWindow mainWindow)
                {
                    await mainWindow.ShowMainMenu();
                }
            });
        }

        private void OnVoteKickStarted(string targetUsername, string reason)
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

        public void StopTimers()
        {
            _startCountdownTimer?.Stop();
            _turnCountdownTimer?.Stop();
        }

        public void UpdateUI(GameStateDto state)
        {
            ProcessGameState(state);
        }

        public async void HandleGameOver(string winner)
        {
            if (_isGameOverHandled) return;
            _isGameOverHandled = true;
            StopTimers();

            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow == null && !NavigationService.CanGoBack) return;

            MessageBox.Show($"¡Juego Terminado!\n\nGanador: {winner}", "Fin de Partida", MessageBoxButton.OK, MessageBoxImage.Information);

            if (mainWindow != null)
            {
                await mainWindow.ShowMainMenu();
            }
            else if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
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

            _ = InitialStateLoad();
        }

        private async Task InitialStateLoad()
        {
            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                var state = await GameplayServiceManager.Instance.GetGameStateAsync(request);
                if (state != null) ProcessGameState(state);
            }
            catch { }
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
                _ = InitialStateLoad();
            }
        }

        private void ProcessGameState(GameStateDto state)
        {
            if (state == null) return;

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

        private async void RollDiceButton_Click(object sender, RoutedEventArgs e)
        {
            RollDiceButton.IsEnabled = false;
            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                var result = await GameplayServiceManager.Instance.RollDiceAsync(request);

                if (result != null)
                {
                    DiceOneText.Text = result.DiceOne.ToString();
                    DiceTwoText.Text = result.DiceTwo.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al tirar dados: " + ex.Message);
                RollDiceButton.IsEnabled = true;
            }
        }

        private void UpdateTurnUI(GameStateDto state)
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
                    GameLogListBox.Items.Add(CleanLogMessage(logRaw));
                }
                if (GameLogListBox.Items.Count > 0) GameLogListBox.ScrollIntoView(GameLogListBox.Items[GameLogListBox.Items.Count - 1]);
            }
        }

        private static string CleanLogMessage(string raw)
        {
            string clean = raw;
            if (string.IsNullOrEmpty(clean)) return string.Empty;

            if (clean.Contains(LuckyBoxTagPrefix))
            {
                int start = clean.IndexOf(LuckyBoxTagPrefix);
                if (start != -1)
                {
                    int end = clean.IndexOf("]", start);
                    if (end != -1)
                    {
                        string tag = clean.Substring(start, end - start + 1);
                        clean = clean.Replace(tag, "").Trim();
                    }
                }
            }
            return clean.Replace("[EXTRA]", "").Trim();
        }

        private void CheckForLuckyBox(string logDescription)
        {
            if (string.IsNullOrEmpty(logDescription)) return;
            if (TryParseLuckyBoxTag(logDescription, out string boxOwner, out string rewardType, out int rewardAmount) && boxOwner == currentUsername)
            {
                ShowLuckyBoxUI(rewardType, rewardAmount);
            }
        }

        private static bool TryParseLuckyBoxTag(string log, out string owner, out string type, out int amount)
        {
            owner = type = ""; amount = 0;
            if (string.IsNullOrEmpty(log)) return false;

            int start = log.IndexOf(LuckyBoxTagPrefix);
            if (start == -1) return false;

            int end = log.IndexOf("]", start);
            if (end == -1) return false;

            try
            {
                string content = log.Substring(start, end - start + 1).Replace(LuckyBoxTagPrefix, "").Replace("]", "");
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

        private void ProcessLatestLog(string[] logs)
        {
            if (logs != null && logs.Any())
            {
                string latestLog = logs[0];
                if (latestLog != _lastLogProcessed)
                {
                    _lastLogProcessed = latestLog;
                    CheckForLuckyBox(latestLog);
                }
            }
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == 1) ? "/Assets/Boards/normal_board.png" : "/Assets/Boards/special_board.png";
            try { BoardImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)); }
            catch { }
        }

        private void UpdateBoardVisuals(PlayerPositionDto[] players)
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

        private void UpdateDiceVisuals(int dice1, int dice2)
        {
            if (dice1 > 0)
            {
                DiceOneText.Text = dice1.ToString();
                DiceTwoText.Text = dice2.ToString();
            }
        }

        private void UpdatePlayerAvatars(PlayerPositionDto[] players)
        {
            if (players == null) return;
            HideAllPlayerPanels();
            var sortedPlayers = players.OrderBy(p => p.Username).ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                var controls = GetPlayerUIControls(i);
                if (controls.Panel != null) ConfigurePlayerPanel(controls, player);
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

        private void ConfigurePlayerPanel((ImageBrush Avatar, TextBlock Name, Border Panel) controls, PlayerPositionDto player)
        {
            controls.Panel.Visibility = Visibility.Visible;
            controls.Name.Text = player.Username;
            controls.Panel.Tag = player.Username;

            if (_isGuest && controls.Panel.ContextMenu != null) controls.Panel.ContextMenu = null;

            LoadAvatarImage(controls.Avatar, player.AvatarPath);
            controls.Panel.BorderBrush = player.IsMyTurn ? Brushes.Gold : Brushes.Transparent;
            controls.Panel.BorderThickness = new Thickness(player.IsMyTurn ? 3 : 0);
        }

        private static void LoadAvatarImage(ImageBrush brush, string avatarPath)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            try { bitmap.UriSource = GetAvatarUri(avatarPath); }
            catch { bitmap.UriSource = new Uri("/Assets/default_avatar.png", UriKind.Relative); }
            bitmap.EndInit();
            brush.Stretch = Stretch.UniformToFill;
            brush.ImageSource = bitmap;
        }

        private static Uri GetAvatarUri(string path)
        {
            if (string.IsNullOrEmpty(path)) return new Uri("/Assets/default_avatar.png", UriKind.Relative);
            if (path.StartsWith("pack://")) return new Uri(path, UriKind.RelativeOrAbsolute);
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatar", path);
            return File.Exists(fullPath) ? new Uri(fullPath, UriKind.Absolute) : new Uri("/Assets/default_avatar.png", UriKind.Relative);
        }

        private void CloseChatClient()
        {
            if (chatClient != null)
            {
                try { if (chatClient.State == CommunicationState.Opened) chatClient.Close(); else chatClient.Abort(); }
                catch { chatClient.Abort(); }
            }
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
            catch { }
        }

        public void ReceiveMessage(ChatMessageDto message)
        {
            Dispatcher.Invoke(() =>
            {
                string tabName = "General";
                if (message.IsPrivate) tabName = (message.Sender == currentUsername) ? message.TargetUser : message.Sender;
                ListBox targetList = GetOrCreateTab(tabName);
                targetList.Items.Add($"{message.Sender}: {message.Message}");
                targetList.ScrollIntoView(targetList.Items[targetList.Items.Count - 1]);
            });
        }

        private ListBox GetOrCreateTab(string tabName)
        {
            foreach (TabItem item in ChatTabControl.Items)
            {
                if (item.Tag?.ToString() == tabName) return (ListBox)item.Content;
            }
            var newListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 0)
            };
            if (GeneralChatList.ItemTemplate != null) newListBox.ItemTemplate = GeneralChatList.ItemTemplate;
            var newTab = new TabItem { Header = tabName, Tag = tabName, Content = newListBox, Style = (Style)FindResource("ChatTabItemStyle") };
            ChatTabControl.Items.Add(newTab);
            return newListBox;
        }

        private void SendChatButton_Click(object sender, RoutedEventArgs e)
        {
            string msg = ChatInputBox.Text;
            if (string.IsNullOrWhiteSpace(msg)) return;
            var selectedTab = ChatTabControl.SelectedItem as TabItem;
            string target = selectedTab?.Tag?.ToString() ?? "General";
            var dto = new ChatMessageDto { Sender = currentUsername, LobbyCode = lobbyCode, Message = msg };
            ReceiveMessage(dto);
            try
            {
                if (target == "General") { dto.IsPrivate = false; chatClient.SendLobbyMessage(dto); }
                else { dto.IsPrivate = true; dto.TargetUser = target; chatClient.SendPrivateMessage(dto); }
                ChatInputBox.Clear();
            }
            catch { MessageBox.Show("Error enviando mensaje."); }
        }

        private void ChatInputBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SendChatButton_Click(sender, e); }

        private void PrivateChatMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest) return;
            var panel = (sender as MenuItem)?.Parent is ContextMenu cm ? cm.PlacementTarget as Border : null;
            string targetUser = panel?.Tag?.ToString();
            if (!string.IsNullOrEmpty(targetUser) && targetUser != currentUsername)
            {
                GetOrCreateTab(targetUser);
                foreach (TabItem item in ChatTabControl.Items)
                {
                    if (item.Tag?.ToString() == targetUser) { ChatTabControl.SelectedItem = item; break; }
                }
            }
        }

        private async void AddFriendMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest) return;
            var panel = (sender as MenuItem)?.Parent is ContextMenu cm ? cm.PlacementTarget as Border : null;
            string targetUser = panel?.Tag?.ToString();
            if (string.IsNullOrEmpty(targetUser) || targetUser == currentUsername) return;
            try
            {
                bool sent = await FriendshipServiceManager.Instance.SendFriendRequestAsync(targetUser);
                MessageBox.Show(sent ? $"Solicitud enviada a {targetUser}." : "No se pudo enviar.");
            }
            catch { MessageBox.Show("Error de conexión."); }
        }

        private void ShowLuckyBoxUI(string type, int amount)
        {
            _currentRewardType = type; _currentRewardAmount = amount; _luckyBoxClicks = 0;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "luckybox_closed.png");
            try { LuckyBoxImage.Source = new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute)); } catch { }
            LuckyBoxImage.Visibility = Visibility.Visible;
            RewardContainer.Visibility = Visibility.Collapsed;
            OpenBoxButton.IsEnabled = true;
            LuckyBoxOverlay.Visibility = Visibility.Visible;
        }

        private async void OpenBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _luckyBoxClicks++;
            var shakeAnim = this.Resources["ShakeAnimation"] as Storyboard ?? LuckyBoxOverlay.Resources["ShakeAnimation"] as Storyboard;
            if (_luckyBoxClicks < 3) shakeAnim?.Begin();
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
            string imagePath = ""; string text = ""; SolidColorBrush color = Brushes.White;
            switch (_currentRewardType)
            {
                case "COINS": imagePath = "coin_pile.png"; text = $"+{_currentRewardAmount} ORO"; color = Brushes.Gold; break;
                case "COMMON": imagePath = "ticket_common.png"; text = "TICKET COMÚN"; break;
                case "EPIC": imagePath = "ticket_epic.png"; text = "TICKET ÉPICO"; color = Brushes.Purple; break;
                case "LEGENDARY": imagePath = "ticket_legendary.png"; text = "¡LEGENDARIO!"; color = Brushes.OrangeRed; break;
            }
            RewardText.Text = text; RewardText.Foreground = color;
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", imagePath);
            try { RewardImage.Source = new BitmapImage(new Uri(fullPath, UriKind.RelativeOrAbsolute)); } catch { }
        }

        private void LuckyBoxOverlay_MouseDown(object sender, MouseButtonEventArgs e) { e.Handled = true; }

        private void VoteKickMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest) return;
            var panel = (sender as MenuItem)?.Parent is ContextMenu cm ? cm.PlacementTarget as Border : null;
            string targetUser = panel?.Tag?.ToString();
            if (string.IsNullOrEmpty(targetUser) || targetUser == currentUsername) return;
            ReasonSelectorOverlay.Tag = targetUser;
            ReasonSelectorOverlay.Visibility = Visibility.Visible;
        }

        private async void ConfirmKickButton_Click(object sender, RoutedEventArgs e)
        {
            string target = ReasonSelectorOverlay.Tag?.ToString();
            string reason = (KickReasonCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Sin razón";
            ReasonSelectorOverlay.Visibility = Visibility.Collapsed;
            try
            {
                var req = new VoteRequestDto { Username = currentUsername, TargetUsername = target, Reason = reason };
                await GameplayServiceManager.Instance.InitiateVoteKickAsync(req);
                MessageBox.Show($"Votación iniciada contra {target}.");
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void CancelKickButton_Click(object sender, RoutedEventArgs e) => ReasonSelectorOverlay.Visibility = Visibility.Collapsed;
        private async void VoteYes_Click(object sender, RoutedEventArgs e) => await SendVote(true);
        private async void VoteNo_Click(object sender, RoutedEventArgs e) => await SendVote(false);

        private async Task SendVote(bool accept)
        {
            VoteKickOverlay.Visibility = Visibility.Collapsed;
            try
            {
                var resp = new VoteResponseDto { Username = currentUsername, AcceptKick = accept };
                await GameplayServiceManager.Instance.CastVoteAsync(resp);
            }
            catch { }
        }

        private async void QuitGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Salir y perder?", "Abandonar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            UnsubscribeFromEvents();
            StopTimers();

            try
            {
                await GameplayServiceManager.Instance.LeaveGameAsync(new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername });
            }
            finally
            {
                if (Window.GetWindow(this) is GameMainWindow mw) await mw.ShowMainMenu();
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e) => PauseMenuOverlay.Visibility = Visibility.Visible;
        private void ResumeButton_Click(object sender, RoutedEventArgs e) => PauseMenuOverlay.Visibility = Visibility.Collapsed;
        private void PauseMenuOverlay_MouseDown(object sender, MouseButtonEventArgs e) { if (e.OriginalSource == sender) PauseMenuOverlay.Visibility = Visibility.Collapsed; }

        private void IngameScreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mw)
            {
                if (IngameScreenModeCombo.SelectedIndex == 0) { mw.WindowStyle = WindowStyle.None; mw.WindowState = WindowState.Maximized; }
                else if (IngameScreenModeCombo.SelectedIndex == 1) { mw.WindowStyle = WindowStyle.None; mw.WindowState = WindowState.Normal; mw.Width = 1280; mw.Height = 720; mw.CenterWindow(); }
                else { mw.WindowStyle = WindowStyle.SingleBorderWindow; mw.WindowState = WindowState.Normal; }
            }
        }
    }
}