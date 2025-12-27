using GameClient.GameplayServiceReference;
using GameClient.LobbyServiceReference;
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
    public partial class BoardPage : Page, ILobbyServiceCallback
    {
        private string lobbyCode;
        private int boardId;
        private string currentUsername;
        private GameplayServiceClient gameplayClient;
        private LobbyServiceClient _lobbyProxy;
        private DispatcherTimer gameLoopTimer;
        private DispatcherTimer _startCountdownTimer;
        private DispatcherTimer _turnCountdownTimer;
        private int _turnSecondsRemaining = 60;
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

            gameplayClient = new GameplayServiceClient();
            InitializeLobbyListener();

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
            };
        }

        private void InitializeLobbyListener()
        {
            InstanceContext context = new InstanceContext(this);
            _lobbyProxy = new LobbyServiceClient(context);
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
                if (mainWindow != null)
                {
                    mainWindow.ShowMainMenu();
                }
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
            if (e.OriginalSource == sender)
            {
                PauseMenuOverlay.Visibility = Visibility.Collapsed;
            }
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
                var request = new GameplayRequest
                {
                    LobbyCode = lobbyCode,
                    Username = currentUsername
                };
                await gameplayClient.LeaveGameAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al abandonar: " + ex.Message);
            }
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

            if (_countdownValue > 0)
            {
                StartTimerText.Text = _countdownValue.ToString();
            }
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

            if (_turnSecondsRemaining <= 15)
            {
                TurnTimerText.Foreground = Brushes.Red;
            }
            else
            {
                TurnTimerText.Foreground = Brushes.White;
            }

            if (_turnSecondsRemaining <= 0)
            {
                _turnCountdownTimer.Stop();
                TurnTimerText.Text = "¡Tiempo Agotado!";
            }
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == 1)
                ? "/Assets/Boards/normal_board.png"
                : "/Assets/Boards/special_board.png";

            try { BoardImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)); }
            catch { }
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
                var request = new GameplayRequest
                {
                    LobbyCode = lobbyCode,
                    Username = currentUsername
                };

                var state = await gameplayClient.GetGameStateAsync(request);

                if (state != null)
                {
                    if (state.IsGameOver)
                    {
                        _isGameOverHandled = true;
                        HandleGameOver(state.WinnerUsername);
                        return;
                    }

                    UpdateTurnUI(state);

                    if (state.CurrentTurnUsername != _lastTurnUsername)
                    {
                        _lastTurnUsername = state.CurrentTurnUsername;
                        _turnSecondsRemaining = 60;
                        TurnTimerPanel.Visibility = Visibility.Visible;
                        TurnTimerText.Text = "Tiempo: 60s";
                        TurnTimerText.Foreground = Brushes.White;
                        _turnCountdownTimer.Start();
                    }

                    if (state.LastDiceOne > 0)
                    {
                        DiceOneText.Text = state.LastDiceOne.ToString();
                        DiceTwoText.Text = state.LastDiceTwo.ToString();
                    }

                    UpdateGameLog(state.GameLog);
                    UpdateBoardVisuals(state.PlayerPositions);
                    UpdatePlayerAvatars(state.PlayerPositions);

                    if (state.GameLog != null && state.GameLog.Any())
                    {
                        string latestLog = state.GameLog.First();
                        if (latestLog != _lastLogProcessed)
                        {
                            _lastLogProcessed = latestLog;
                            CheckForLuckyBox(latestLog);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (!_isGameOverHandled)
                {
                    gameLoopTimer.Start();
                }
            }
        }

        private async void RollDiceButton_Click(object sender, RoutedEventArgs e)
        {
            RollDiceButton.IsEnabled = false;

            try
            {
                var request = new GameplayRequest
                {
                    LobbyCode = lobbyCode,
                    Username = currentUsername
                };

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

            Player1Panel.Visibility = Visibility.Hidden;
            Player2Panel.Visibility = Visibility.Hidden;
            Player3Panel.Visibility = Visibility.Hidden;
            Player4Panel.Visibility = Visibility.Hidden;

            var sortedPlayers = players.OrderBy(p => p.Username).ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                string avatarPath = player.AvatarPath;

                ImageBrush targetAvatarBrush = null;
                TextBlock targetNameBlock = null;
                Border targetPanel = null;

                switch (i)
                {
                    case 0: targetAvatarBrush = Player1Avatar; targetNameBlock = Player1Name; targetPanel = Player1Panel; break;
                    case 1: targetAvatarBrush = Player2Avatar; targetNameBlock = Player2Name; targetPanel = Player2Panel; break;
                    case 2: targetAvatarBrush = Player3Avatar; targetNameBlock = Player3Name; targetPanel = Player3Panel; break;
                    case 3: targetAvatarBrush = Player4Avatar; targetNameBlock = Player4Name; targetPanel = Player4Panel; break;
                }

                if (targetPanel != null)
                {
                    targetPanel.Visibility = Visibility.Visible;
                    targetNameBlock.Text = player.Username;

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;

                    try
                    {
                        if (string.IsNullOrEmpty(avatarPath))
                        {
                            bitmap.UriSource = new Uri("/Assets/default_avatar.png", UriKind.Relative);
                        }
                        else if (avatarPath.StartsWith("pack://"))
                        {
                            bitmap.UriSource = new Uri(avatarPath, UriKind.RelativeOrAbsolute);
                        }
                        else
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            string fullPath = Path.Combine(baseDir, "Assets", "Avatar", avatarPath);

                            if (!File.Exists(fullPath))
                            {
                                fullPath = Path.Combine(baseDir, "Assets", avatarPath);
                            }

                            if (File.Exists(fullPath))
                            {
                                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                            }
                            else
                            {
                                bitmap.UriSource = new Uri("/Assets/default_avatar.png", UriKind.Relative);
                            }
                        }
                    }
                    catch
                    {
                        bitmap.UriSource = new Uri("/Assets/default_avatar.png", UriKind.Relative);
                    }

                    bitmap.EndInit();

                    targetAvatarBrush.Stretch = Stretch.UniformToFill;
                    targetAvatarBrush.ImageSource = bitmap;

                    targetPanel.BorderBrush = player.IsMyTurn ? Brushes.Gold : Brushes.Transparent;
                    targetPanel.BorderThickness = new Thickness(player.IsMyTurn ? 3 : 0);
                }
            }
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
            double left = targetPoint.X - 20;
            double top = targetPoint.Y - 20;
            double currentLeft = Canvas.GetLeft(token);
            double currentTop = Canvas.GetTop(token);
            if (double.IsNaN(currentLeft)) currentLeft = _tileCoordinates[0].X - 20;
            if (double.IsNaN(currentTop)) currentTop = _tileCoordinates[0].Y - 20;

            var animX = new DoubleAnimation { From = currentLeft, To = left, Duration = TimeSpan.FromMilliseconds(500) };
            var animY = new DoubleAnimation { From = currentTop, To = top, Duration = TimeSpan.FromMilliseconds(500) };
            token.BeginAnimation(Canvas.LeftProperty, animX);
            token.BeginAnimation(Canvas.TopProperty, animY);
        }

        private void UpdateTurnUI(GameStateDTO state)
        {
            if (_isGameStarting)
            {
                RollDiceButton.IsEnabled = false;
                RollDiceButton.Content = "Esperando inicio...";
                RollDiceButton.Opacity = 0.5;
                return;
            }

            if (state.IsMyTurn)
            {
                RollDiceButton.IsEnabled = true;
                RollDiceButton.Content = "¡Tirar Dados!";
                RollDiceButton.Opacity = 1;
            }
            else
            {
                RollDiceButton.IsEnabled = false;
                RollDiceButton.Content = $"Turno de {state.CurrentTurnUsername}";
                RollDiceButton.Opacity = 0.6;
            }
        }

        private void UpdateGameLog(string[] logs)
        {
            if (logs == null) return;

            if (GameLogListBox.Items.Count != logs.Length)
            {
                GameLogListBox.Items.Clear();

                foreach (var logRaw in logs)
                {
                    string textToShow = logRaw;

                    if (textToShow.Contains("[LUCKYBOX:"))
                    {
                        int start = textToShow.IndexOf("[LUCKYBOX:");
                        int end = textToShow.IndexOf("]", start);

                        if (start != -1 && end != -1)
                        {
                            string tagToRemove = textToShow.Substring(start, end - start + 1);
                            textToShow = textToShow.Replace(tagToRemove, "").Trim();
                        }
                    }

                    textToShow = textToShow.Replace("[EXTRA]", "").Trim();
                    GameLogListBox.Items.Add(textToShow);
                }

                if (GameLogListBox.Items.Count > 0)
                    GameLogListBox.ScrollIntoView(GameLogListBox.Items[GameLogListBox.Items.Count - 1]);
            }
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

            if (logDescription.Contains("[LUCKYBOX:"))
            {
                try
                {
                    int startIndex = logDescription.IndexOf("[LUCKYBOX:");
                    int endIndex = logDescription.IndexOf("]", startIndex);

                    if (startIndex != -1 && endIndex != -1)
                    {
                        string tag = logDescription.Substring(startIndex, endIndex - startIndex + 1);
                        string data = tag.Replace("[LUCKYBOX:", "").Replace("]", "");
                        string[] mainParts = data.Split(':');

                        if (mainParts.Length == 2)
                        {
                            string boxOwner = mainParts[0];
                            string rewardData = mainParts[1];

                            if (boxOwner != currentUsername)
                            {
                                return;
                            }

                            string[] rewardParts = rewardData.Split('_');
                            if (rewardParts.Length == 2)
                            {
                                _currentRewardType = rewardParts[0];
                                _currentRewardAmount = int.Parse(rewardParts[1]);

                                _luckyBoxClicks = 0;
                                try
                                {
                                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                                    string path = Path.Combine(baseDir, "Assets", "Images", "luckybox_closed.png");
                                    if (File.Exists(path))
                                        LuckyBoxImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                                    else
                                        LuckyBoxImage.Source = new BitmapImage(new Uri("/Assets/Images/luckybox_closed.png", UriKind.Relative));
                                }
                                catch { }

                                LuckyBoxImage.Visibility = Visibility.Visible;
                                RewardContainer.Visibility = Visibility.Collapsed;
                                OpenBoxButton.IsEnabled = true;
                                LuckyBoxOverlay.Visibility = Visibility.Visible;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error LuckyBox: " + ex.Message);
                }
            }
        }

        private async void OpenBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _luckyBoxClicks++;
            Storyboard shakeAnim = (Storyboard)LuckyBoxOverlay.Resources["ShakeAnimation"];

            if (_luckyBoxClicks < 3)
            {
                shakeAnim.Begin();
            }
            else
            {
                OpenBoxButton.IsEnabled = false;
                LuckyBoxImage.Visibility = Visibility.Collapsed;

                SetRewardVisuals();

                RewardContainer.Visibility = Visibility.Visible;
                Storyboard revealAnim = (Storyboard)LuckyBoxOverlay.Resources["RevealAnimation"];
                revealAnim.Begin();

                await Task.Delay(3000);
                LuckyBoxOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void SetRewardVisuals()
        {
            string imagePath = "";
            string text = "";
            SolidColorBrush textColor = Brushes.White;

            switch (_currentRewardType)
            {
                case "COINS":
                    imagePath = "coin_pile.png";
                    text = $"+{_currentRewardAmount} ORO";
                    textColor = Brushes.Gold;
                    break;
                case "COMMON":
                    imagePath = "ticket_common.png";
                    text = "TICKET COMÚN";
                    textColor = Brushes.White;
                    break;
                case "EPIC":
                    imagePath = "ticket_epic.png";
                    text = "TICKET ÉPICO";
                    textColor = Brushes.Purple;
                    break;
                case "LEGENDARY":
                    imagePath = "ticket_legendary.png";
                    text = "¡LEGENDARIO!";
                    textColor = Brushes.OrangeRed;
                    break;
                default:
                    text = "PREMIO";
                    break;
            }

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string fullPath = Path.Combine(baseDir, "Assets", "Images", imagePath);
                    if (File.Exists(fullPath))
                        RewardImage.Source = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                    else
                        RewardImage.Source = new BitmapImage(new Uri($"/Assets/Images/{imagePath}", UriKind.Relative));
                }
                catch { }
            }
            RewardText.Text = text;
            RewardText.Foreground = textColor;
        }

        private void LuckyBoxOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}