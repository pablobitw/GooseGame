using GameClient.ChatServiceReference;
using GameClient.FriendshipServiceReference;
using GameClient.GameplayServiceReference;
using GameClient.Helpers;
using GameClient.Views.Dialogs;
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
        private const double TokenSize = 40.0;
        private const double TokenOffset = 20.0;
        private const int AnimationDurationMs = 500;

        private const double OpacityActive = 1.0;
        private const double OpacityInactive = 0.4;
        private const double OpacityDisabled = 0.5;
        private const double OpacitySemi = 0.6;

        private readonly string lobbyCode;
        private readonly int boardId;
        private readonly string currentUsername;
        private readonly bool _isGuest = false;

        private ChatServiceClient chatClient;
        private ChatUiManager _chatManager;

        private DispatcherTimer _startCountdownTimer;
        private DispatcherTimer _turnCountdownTimer;

        private int _turnSecondsRemaining;
        private string _lastTurnUsername = string.Empty;
        private int _countdownValue;
        private bool _isGameStarting = true;
        private bool _isGameOverHandled = false;

        private Dictionary<string, UIElement> _playerTokens = new Dictionary<string, UIElement>();
        private string _lastLogProcessed = string.Empty;

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

            _turnSecondsRemaining = GameConfiguration.TurnDurationSeconds;
            _countdownValue = GameConfiguration.StartCountdownSeconds;

            _chatManager = new ChatUiManager(ChatTabControl, GeneralChatList, (Style)FindResource("ChatTabItemStyle"));

            PauseMenu.ResumeRequested += (s, e) => PauseMenu.Visibility = Visibility.Collapsed;
            PauseMenu.QuitRequested += (s, e) => QuitGameProcess();

            KickReasonPrompt.KickConfirmed += KickReasonPrompt_KickConfirmed;
            KickReasonPrompt.KickCancelled += (s, e) => KickReasonPrompt.Visibility = Visibility.Collapsed;

            VoteKickPrompt.VoteSubmitted += VoteKickPrompt_VoteSubmitted;

            SubscribeToEvents();
            ConnectToChatService();
            LoadBoardImage();
            StartCountdown();

            _turnCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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
            FriendshipServiceManager.Instance.FriendRequestPopUpReceived += OnFriendRequestPopUpReceived;
        }

        private void UnsubscribeFromEvents()
        {
            GameplayServiceManager.Instance.TurnChanged -= OnTurnChanged;
            GameplayServiceManager.Instance.GameFinished -= OnGameFinished;
            GameplayServiceManager.Instance.PlayerKicked -= OnPlayerKicked;
            GameplayServiceManager.Instance.VoteKickStarted -= OnVoteKickStarted;
            if (FriendshipServiceManager.Instance != null)
            {
                FriendshipServiceManager.Instance.FriendRequestPopUpReceived -= OnFriendRequestPopUpReceived;
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
            catch (EndpointNotFoundException)
            {
                SessionManager.ForceLogout("No se pudo conectar al servidor de chat. Verifica la configuración.");
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Error de conexión al intentar unirse al chat.");
            }
        }

        public void ReceiveMessage(ChatMessageDto message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _chatManager.AddMessage(message.Sender, message.Message, message.IsPrivate, message.TargetUser, currentUsername);
            });
        }

        private void SendChatButton_Click(object sender, RoutedEventArgs e)
        {
            string msg = ChatInputBox.Text;
            if (string.IsNullOrWhiteSpace(msg)) return;

            string target = _chatManager.GetCurrentTarget();
            var dto = new ChatMessageDto { Sender = currentUsername, LobbyCode = lobbyCode, Message = msg };

            ReceiveMessage(dto);
            ChatInputBox.Clear();

            Task.Run(() =>
            {
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
                }
                catch (CommunicationException)
                {
                    Dispatcher.InvokeAsync(() => MessageBox.Show("No se pudo enviar el mensaje. Revisa tu conexión."));
                }
                catch (Exception)
                {
                    Dispatcher.InvokeAsync(() => MessageBox.Show("Error interno al enviar mensaje."));
                }
            });
        }

        private void ChatInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendChatButton_Click(sender, e);
        }

        private void PrivateChatMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest) return;
            var panel = (sender as MenuItem)?.Parent is ContextMenu cm ? cm.PlacementTarget as Border : null;
            string targetUser = panel?.Tag?.ToString();

            if (!string.IsNullOrEmpty(targetUser) && targetUser != currentUsername)
            {
                _chatManager.EnsureTabExists(targetUser);
                _chatManager.SelectTab(targetUser);
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            PauseMenu.Visibility = Visibility.Visible;
        }

        private async void QuitGameProcess()
        {
            PauseMenu.Visibility = Visibility.Collapsed;

            if (MessageBox.Show("¿Estás seguro de salir? Perderás la partida.", "Abandonar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            UnsubscribeFromEvents();
            StopTimers();

            try
            {
                await GameplayServiceManager.Instance.LeaveGameAsync(new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername });

                if (Window.GetWindow(this) is GameMainWindow mw)
                {
                    await mw.ShowMainMenu();
                }
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Error de conexión al intentar abandonar la partida.");
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió al intentar salir.");
            }
            catch (Exception ex)
            {
                SessionManager.ForceLogout($"Error inesperado al salir: {ex.Message}");
            }
        }

        private void VoteKickMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isGuest) return;
            var panel = (sender as MenuItem)?.Parent is ContextMenu cm ? cm.PlacementTarget as Border : null;
            string targetUser = panel?.Tag?.ToString();

            if (!string.IsNullOrEmpty(targetUser) && targetUser != currentUsername)
            {
                KickReasonPrompt.Show(targetUser);
            }
        }

        private async void KickReasonPrompt_KickConfirmed(object sender, KickEventArgs e)
        {
            KickReasonPrompt.Visibility = Visibility.Collapsed;
            try
            {
                var req = new VoteRequestDto { Username = currentUsername, TargetUsername = e.TargetUsername, Reason = e.Reason };
                await GameplayServiceManager.Instance.InitiateVoteKickAsync(req);
                MessageBox.Show($"Votación iniciada contra {e.TargetUsername}.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (FaultException ex)
            {
                MessageBox.Show($"No se pudo iniciar votación: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Conexión perdida al intentar iniciar la votación.");
            }
        }

        private async void VoteKickPrompt_VoteSubmitted(object sender, bool accept)
        {
            try
            {
                var resp = new VoteResponseDto { Username = currentUsername, AcceptKick = accept };
                await GameplayServiceManager.Instance.CastVoteAsync(resp);
            }
            catch (FaultException ex)
            {
                MessageBox.Show($"No se pudo registrar el voto: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Se perdió la conexión al enviar tu voto.");
            }
        }

        private void OnTurnChanged(GameStateDto newState)
        {
            Dispatcher.InvokeAsync(() => ProcessGameState(newState));
        }

        private void OnGameFinished(string winner)
        {
            Dispatcher.InvokeAsync(() => HandleGameOver(winner));
        }

        private void OnPlayerKicked(string reason)
        {
            Dispatcher.Invoke(() =>
            {
                IsEnabled = false;
                RollDiceButton.IsEnabled = false;
                StopTimers();
                _isGameOverHandled = true;

                UnsubscribeFromEvents();
                CloseChatClient();

                string title = GameClient.Resources.Strings.KickedTitle ?? "Expulsado";
                MessageBox.Show(reason, title, MessageBoxButton.OK, MessageBoxImage.Warning);

                
                if (Window.GetWindow(this) is GameMainWindow mainWindow)
                {
                  
                    var loginScreen = new AuthWindow();
                    loginScreen.Show();
                     mainWindow.Close();
                }
            });
        }

        private void OnVoteKickStarted(string targetUsername, string reason)
        {
            if (targetUsername == currentUsername) return;

            Dispatcher.InvokeAsync(() =>
            {
                VoteKickPrompt.ShowVote(targetUsername, reason);
            });
        }

        private async Task InitialStateLoad()
        {
            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                var state = await GameplayServiceManager.Instance.GetGameStateAsync(request);
                if (state != null) ProcessGameState(state);
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("No se pudo cargar el estado inicial de la partida por problemas de conexión.");
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor tardó demasiado en enviar el estado inicial.");
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

        private async void RollDiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isGameOverHandled || !IsEnabled) return;

            RollDiceButton.IsEnabled = false;
            try
            {
                var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                var result = await GameplayServiceManager.Instance.RollDiceAsync(request);

                if (result != null)
                {
                    UpdateDiceVisuals(result.DiceOne, result.DiceTwo);
                }
            }
            catch (FaultException ex)
            {
                MessageBox.Show($"Error del juego: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (!_isGameOverHandled) RollDiceButton.IsEnabled = true;
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Conexión perdida con el servidor de juego.");
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió al intento de tirar dados.");
            }
        }

        private void UpdateTurnUI(GameStateDto state)
        {
            if (_isGameStarting)
            {
                RollDiceButton.IsEnabled = false;
                RollDiceButton.Content = "Esperando inicio...";
                RollDiceButton.Opacity = OpacityDisabled;
                return;
            }

            if (state.IsMyTurn)
            {
                RollDiceButton.IsEnabled = true;
                RollDiceButton.Content = "¡Tirar Dados!";
                RollDiceButton.Opacity = OpacityActive;
            }
            else
            {
                RollDiceButton.IsEnabled = false;
                RollDiceButton.Content = $"Turno de {state.CurrentTurnUsername}";
                RollDiceButton.Opacity = OpacitySemi;
            }
        }

        private void UpdateTurnTimer(string currentTurnUsername)
        {
            if (currentTurnUsername != _lastTurnUsername)
            {
                _lastTurnUsername = currentTurnUsername;
                _turnSecondsRemaining = GameConfiguration.TurnDurationSeconds;

                TurnTimerPanel.Visibility = Visibility.Visible;
                TurnTimerText.Text = $"Tiempo: {GameConfiguration.TurnDurationSeconds}s";
                TurnTimerText.Foreground = Brushes.White;
                _turnCountdownTimer.Start();
            }
        }

        private async void TurnCountdown_Tick(object sender, EventArgs e)
        {
            _turnSecondsRemaining--;

            if (_turnSecondsRemaining < 0) _turnSecondsRemaining = 0;

            TurnTimerText.Text = $"Tiempo: {_turnSecondsRemaining}s";

            TurnTimerText.Foreground = _turnSecondsRemaining <= GameConfiguration.TurnWarningThreshold
                ? Brushes.Red
                : Brushes.White;

            if (_turnSecondsRemaining <= 0)
            {
                _turnCountdownTimer.Stop();
                TurnTimerText.Text = "¡Tiempo Agotado!";

                await Task.Delay(1500);

                try
                {
                    var request = new GameplayRequest { LobbyCode = lobbyCode, Username = currentUsername };
                    await GameplayServiceManager.Instance.GetGameStateAsync(request);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error triggering server timeout: " + ex.Message);
                }
            }
        }

        private void StartCountdown()
        {
            _isGameStarting = true;
            StartTimerPanel.Visibility = Visibility.Visible;
            RollDiceButton.IsEnabled = false;
            RollDiceButton.Opacity = OpacityDisabled;

            _startCountdownTimer = new DispatcherTimer();
            _startCountdownTimer.Interval = TimeSpan.FromSeconds(1);
            _startCountdownTimer.Tick += Countdown_Tick;
            _startCountdownTimer.Start();

            _ = InitialStateLoad();
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
                _ = InitialStateLoad();
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
                    GameLogListBox.Items.Add(GameLogHelper.CleanMessage(logRaw));
                }
                if (GameLogListBox.Items.Count > 0)
                {
                    GameLogListBox.ScrollIntoView(GameLogListBox.Items[GameLogListBox.Items.Count - 1]);
                }
            }
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

        private void CheckForLuckyBox(string logDescription)
        {
            if (string.IsNullOrEmpty(logDescription)) return;

            if (GameLogHelper.TryParseLuckyBox(logDescription, out string boxOwner, out string rewardType, out int rewardAmount))
            {
                if (boxOwner.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    LuckyBox.ShowReward(rewardType, rewardAmount);
                }
            }
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == GameConfiguration.NormalBoardId)
                ? "/Assets/Boards/normal_board.png"
                : "/Assets/Boards/special_board.png";

            try
            {
                BoardImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"No se pudo cargar la imagen del tablero: {ex.Message}");
            }
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
                    string imagePath = BoardDataHelper.TokenImagePaths[i % BoardDataHelper.TokenImagePaths.Length];
                    var token = CreatePlayerToken(player.Username, imagePath);
                    _playerTokens.Add(player.Username, token);
                    BoardCanvas.Children.Add(token);
                }

                var tokenUI = _playerTokens[player.Username];
                MoveTokenToTile(tokenUI, player.CurrentTile);
                tokenUI.Opacity = player.IsOnline ? OpacityActive : OpacityInactive;
            }
        }

        private UIElement CreatePlayerToken(string name, string imagePath)
        {
            var image = new Image
            {
                Width = TokenSize,
                Height = TokenSize,
                Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
                ToolTip = name,
                Stretch = Stretch.Uniform
            };

            var startPos = BoardDataHelper.GetTileLocation(0);
            Canvas.SetLeft(image, startPos.X - TokenOffset);
            Canvas.SetTop(image, startPos.Y - TokenOffset);

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
            Point targetPoint = BoardDataHelper.GetTileLocation(tileIndex);

            var duration = TimeSpan.FromMilliseconds(AnimationDurationMs);

            var animX = new DoubleAnimation { From = Canvas.GetLeft(token), To = targetPoint.X - TokenOffset, Duration = duration };
            var animY = new DoubleAnimation { From = Canvas.GetTop(token), To = targetPoint.Y - TokenOffset, Duration = duration };

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
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = GetAvatarUri(avatarPath);
                bitmap.EndInit();

                brush.Stretch = Stretch.UniformToFill;
                brush.ImageSource = bitmap;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UI Error] No se pudo cargar el avatar '{avatarPath}'. Error: {ex.Message}");

                try
                {
                    brush.ImageSource = new BitmapImage(new Uri("/Assets/default_avatar.png", UriKind.Relative));
                }
                catch (Exception fallbackEx)
                {
                    Console.Error.WriteLine($"[UI Critical] Falló la carga del avatar por defecto: {fallbackEx.Message}");
                }
            }
        }

        private static Uri GetAvatarUri(string path)
        {
            if (string.IsNullOrEmpty(path)) return new Uri("/Assets/default_avatar.png", UriKind.Relative);
            if (path.StartsWith("pack://")) return new Uri(path, UriKind.RelativeOrAbsolute);

            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatar", path);
            return File.Exists(fullPath) ? new Uri(fullPath, UriKind.Absolute) : new Uri("/Assets/default_avatar.png", UriKind.Relative);
        }

        public void StopTimers()
        {
            _startCountdownTimer?.Stop();
            _turnCountdownTimer?.Stop();
        }

        private void CloseChatClient()
        {
            if (chatClient == null) return;

            try
            {
                if (chatClient.State == CommunicationState.Opened) chatClient.Close();
                else chatClient.Abort();
            }
            catch (Exception)
            {
                chatClient.Abort();
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
                var result = await FriendshipServiceManager.Instance.SendFriendRequestAsync(targetUser);

                switch (result)
                {
                    case FriendRequestResult.Success:
                        MessageBox.Show($"Solicitud enviada a {targetUser}.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case FriendRequestResult.AlreadyFriends:
                        MessageBox.Show($"Actualmente ya eres amigo de {targetUser}.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case FriendRequestResult.Pending:
                        MessageBox.Show($"Ya le has mandado solicitud a este jugador.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                    case FriendRequestResult.GuestRestriction:
                        MessageBox.Show("Los invitados no pueden tener amigos.", "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                    case FriendRequestResult.TargetNotFound:
                        MessageBox.Show("No se encontró al jugador.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        MessageBox.Show("Ocurrió un error al intentar enviar la solicitud.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Error de conexión al enviar solicitud de amistad.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void OnFriendRequestPopUpReceived(string senderName)
        {
            Dispatcher.Invoke(() =>
            {
                FriendRequestPopup.ShowRequest(senderName);
            });
        }

        private async void HandleGameOver(string winner)
        {
            if (_isGameOverHandled) return;
            _isGameOverHandled = true;

            StopTimers();

            MessageBox.Show($"¡Juego Terminado!\n\nGanador: {winner}", "Fin de Partida", MessageBoxButton.OK, MessageBoxImage.Information);

            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                await mainWindow.ShowMainMenu();
            }
            else
            {
                if (NavigationService != null && NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
        }
    }
}