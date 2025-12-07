using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.ServiceModel;
using GameClient.GameplayServiceReference;

namespace GameClient.Views
{
    public partial class BoardPage : Page
    {
        private const string AvatarBasePath = "/Assets/Avatar/";
        private const string DefaultAvatarName = "default_avatar.png";
        private const string BoardNormalPath = "/Assets/Boards/normal_board.png";
        private const string BoardSpecialPath = "/Assets/Boards/special_board.png";

        private readonly string[] _tokenImagePaths =
        {
            "/Assets/Game Pieces/red_piece.png",
            "/Assets/Game Pieces/blue_piece.png",
            "/Assets/Game Pieces/green_piece.png",
            "/Assets/Game Pieces/yellow_piece.png"
        };

        private string lobbyCode;
        private int boardId;
        private string currentUsername;
        private GameplayServiceClient gameplayClient;
        private DispatcherTimer gameLoopTimer;
        private bool _isGameOverHandled = false;
        private Dictionary<string, UIElement> _playerTokens = new Dictionary<string, UIElement>();

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

            LoadBoardImage();
            StartGameLoop();

            this.Unloaded += (s, e) =>
            {
                gameLoopTimer?.Stop();
                CloseClient();
            };
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == 1) ? BoardNormalPath : BoardSpecialPath;
            BoardImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative));
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

                    if (state.LastDiceOne > 0)
                    {
                        DiceOneText.Text = state.LastDiceOne.ToString();
                        DiceTwoText.Text = state.LastDiceTwo.ToString();
                    }

                    UpdateGameLog(state.GameLog);
                    UpdateBoardVisuals(state.PlayerPositions);
                    UpdatePlayerAvatars(state.PlayerPositions);
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout actualizando estado del juego.");
            }
            catch (CommunicationException)
            {
                Console.WriteLine("Error de comunicación actualizando estado.");
                gameplayClient.Abort();
                gameplayClient = new GameplayServiceClient();
            }
            finally
            {
                if (!_isGameOverHandled) gameLoopTimer.Start();
            }
        }

        private void UpdatePlayerAvatars(PlayerPositionDTO[] players)
        {
            if (players == null) return;

            SetPanelsVisibility(Visibility.Hidden);

            var sortedPlayers = players.OrderBy(p => p.Username).ToList();

            var playerControls = new List<(Border Panel, ImageBrush Avatar, TextBlock Name)>
            {
                (Player1Panel, Player1Avatar, Player1Name),
                (Player2Panel, Player2Avatar, Player2Name),
                (Player3Panel, Player3Avatar, Player3Name),
                (Player4Panel, Player4Avatar, Player4Name)
            };

            for (int i = 0; i < sortedPlayers.Count && i < playerControls.Count; i++)
            {
                var player = sortedPlayers[i];
                var controls = playerControls[i];

                SetupPlayerPanel(controls, player);
            }
        }

        private static void SetupPlayerPanel((Border Panel, ImageBrush Avatar, TextBlock Name) controls, PlayerPositionDTO player)
        {
            controls.Panel.Visibility = Visibility.Visible;
            controls.Name.Text = player.Username;

            string avatarName = string.IsNullOrEmpty(player.AvatarPath) ? DefaultAvatarName : player.AvatarPath;
            controls.Avatar.ImageSource = LoadAvatarImage(avatarName);

            controls.Panel.BorderBrush = player.IsMyTurn ? Brushes.Gold : Brushes.Transparent;
            controls.Panel.BorderThickness = new Thickness(player.IsMyTurn ? 3 : 0);
        }

        private static ImageSource LoadAvatarImage(string avatarName)
        {
            try
            {
                string path = $"{AvatarBasePath}{avatarName}";
                return new BitmapImage(new Uri(path, UriKind.Relative));
            }
            catch
            {
                try
                {
                    string defaultPath = $"{AvatarBasePath}{DefaultAvatarName}";
                    return new BitmapImage(new Uri(defaultPath, UriKind.Relative));
                }
                catch
                {
                    return null;
                }
            }
        }

        private void SetPanelsVisibility(Visibility visibility)
        {
            Player1Panel.Visibility = visibility;
            Player2Panel.Visibility = visibility;
            Player3Panel.Visibility = visibility;
            Player4Panel.Visibility = visibility;
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

            Point startPoint = _tileCoordinates[0];
            Canvas.SetLeft(image, startPoint.X - 20);
            Canvas.SetTop(image, startPoint.Y - 20);

            image.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 320,
                ShadowDepth = 4,
                Opacity = 0.5
            };

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

            var animX = new DoubleAnimation
            {
                From = currentLeft,
                To = left,
                Duration = TimeSpan.FromMilliseconds(500)
            };

            var animY = new DoubleAnimation
            {
                From = currentTop,
                To = top,
                Duration = TimeSpan.FromMilliseconds(500)
            };

            token.BeginAnimation(Canvas.LeftProperty, animX);
            token.BeginAnimation(Canvas.TopProperty, animY);
        }

        private void UpdateTurnUI(GameStateDTO state)
        {
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
                foreach (var log in logs)
                {
                    GameLogListBox.Items.Add(log);
                }
                if (GameLogListBox.Items.Count > 0)
                {
                    GameLogListBox.ScrollIntoView(GameLogListBox.Items[GameLogListBox.Items.Count - 1]);
                }
            }
        }

        private void HandleGameOver(string winner)
        {
            MessageBox.Show($"¡Juego Terminado!\n\nGanador: {winner}", "Fin de Partida", MessageBoxButton.OK, MessageBoxImage.Information);

            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null)
            {
                mainWindow.ShowMainMenu();
            }
            else
            {
                if (NavigationService.CanGoBack) NavigationService.GoBack();
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

                DiceOneText.Text = result.DiceOne.ToString();
                DiceTwoText.Text = result.DiceTwo.ToString();
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Tiempo de espera agotado al tirar dados.");
                RollDiceButton.IsEnabled = true;
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de comunicación al tirar dados.");
                RollDiceButton.IsEnabled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            gameLoopTimer?.Stop();
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null)
            {
                mainWindow.ShowMainMenu();
            }
            else if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void CloseClient()
        {
            if (gameplayClient == null) return;

            try
            {
                if (gameplayClient.State == CommunicationState.Opened)
                {
                    gameplayClient.Close();
                }
                else
                {
                    gameplayClient.Abort();
                }
            }
            catch
            {
                gameplayClient.Abort();
            }
        }
    }
}