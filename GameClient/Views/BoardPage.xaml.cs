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
using GameClient.GameplayServiceReference;

namespace GameClient.Views
{
    public partial class BoardPage : Page
    {
        private string lobbyCode;
        private int boardId;
        private string currentUsername;
        private GameplayServiceClient gameplayClient;
        private DispatcherTimer gameLoopTimer;
        private bool _isGameOverHandled = false;
        private Dictionary<string, UIElement> _playerTokens = new Dictionary<string, UIElement>();

        private readonly string[] _tokenImagePaths =
        {
            "pack://application:,,,/Assets/Game Pieces/red_piece.png",
            "pack://application:,,,/Assets/Game Pieces/blue_piece.png",
            "pack://application:,,,/Assets/Game Pieces/green_piece.png",
            "pack://application:,,,/Assets/Game Pieces/yellow_piece.png"
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

            LoadBoardImage();
            StartGameLoop();

            this.Unloaded += (s, e) => gameLoopTimer?.Stop();
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == 1)
                ? "pack://application:,,,/Assets/Boards/normal_board.png"
                : "pack://application:,,,/Assets/Boards/special_board.png";

            BoardImage.Source = new BitmapImage(new Uri(imagePath));
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
                // [FIX CRÍTICO 2] Usar objeto Request para cumplir con el contrato WCF
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

        // [FIX CRÍTICO 3] Lógica de Salida implementada
        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Seguro que quieres salir? Perderás la partida.", "Salir", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            gameLoopTimer?.Stop();
            _isGameOverHandled = true; // Evitar que el timer se reactive

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
                // Navegación local
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
            catch (Exception ex)
            {
                MessageBox.Show("Error al tirar dados: " + ex.Message);
                RollDiceButton.IsEnabled = true;
            }
        }

        // --- Resto de la lógica visual original ---

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
                string avatarPath = string.IsNullOrEmpty(player.AvatarPath)
                    ? "pack://application:,,,/Assets/default_avatar.png"
                    : player.AvatarPath;

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
                    try { targetAvatarBrush.ImageSource = new BitmapImage(new Uri(avatarPath, UriKind.RelativeOrAbsolute)); }
                    catch { targetAvatarBrush.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Assets/default_avatar.png")); }
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
                Source = new BitmapImage(new Uri(imagePath)),
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
                foreach (var log in logs) GameLogListBox.Items.Add(log);
                if (GameLogListBox.Items.Count > 0) GameLogListBox.ScrollIntoView(GameLogListBox.Items[GameLogListBox.Items.Count - 1]);
            }
        }

        private void HandleGameOver(string winner)
        {
            MessageBox.Show($"¡Juego Terminado!\n\nGanador: {winner}", "Fin de Partida", MessageBoxButton.OK, MessageBoxImage.Information);
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null) mainWindow.ShowMainMenu();
            else if (NavigationService.CanGoBack) NavigationService.GoBack();
        }
    }
}