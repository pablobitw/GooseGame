using FontAwesome.WPF;
using GameClient.ChatServiceReference;
using GameClient.Helpers; // Para FriendshipServiceManager
using GameClient.LobbyServiceReference;
using GameClient.ViewModels;
using System;
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
    public partial class LobbyPage : Page
    {
        private LobbyViewModel _viewModel;

        private int _localMaxPlayers = 4;
        private int _localBoardId = 1;
        private bool _localIsPublic = true;

        public LobbyPage(string username)
        {
            InitializeComponent();
            _viewModel = new LobbyViewModel(username);

            _localMaxPlayers = 4;
            _localBoardId = 1;
            _localIsPublic = true;

            InitializePage();

            if (LobbyTabControl.Items.Count > 1)
                (LobbyTabControl.Items[1] as TabItem).IsEnabled = false;
        }

        public LobbyPage(string username, string lobbyCode, JoinLobbyResultDTO joinResult)
        {
            InitializeComponent();
            _viewModel = new LobbyViewModel(username, lobbyCode, joinResult);

            _localMaxPlayers = joinResult.MaxPlayers;
            _localBoardId = joinResult.BoardId;
            _localIsPublic = joinResult.IsPublic;

            InitializePage();

            LockLobbySettings();
            StartMatchButton.Visibility = Visibility.Collapsed;

            if (joinResult.PlayersInLobby != null)
                UpdatePlayerListManual(joinResult.PlayersInLobby.ToArray());
        }

        private void InitializePage()
        {
            SubscribeToEvents();
            SyncVisualStyles(); 
        }

        private void SubscribeToEvents()
        {
            _viewModel.MessageReceived += (user, msg) => Dispatcher.Invoke(() => AddMessageToUI(user + ":", msg));

            _viewModel.StateUpdated += (state) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (state.Players != null) UpdatePlayerListManual(state.Players.ToArray());

                    if (!_viewModel.IsHost)
                    {
                        _localMaxPlayers = state.MaxPlayers;
                        _localBoardId = state.BoardId;
                        _localIsPublic = state.IsPublic;
                        SyncVisualStyles();
                    }
                });
            };

            _viewModel.GameStarted += () =>
                Dispatcher.Invoke(() =>
                {
                    if (!_viewModel.IsHost) NavigationService.Navigate(new BoardPage(_viewModel.LobbyCode, _viewModel.BoardId, _viewModel.Username));
                });
        }

        private void IncreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_localMaxPlayers < 4)
            {
                _localMaxPlayers++;
                PlayerCountBlock.Text = _localMaxPlayers.ToString();

                _viewModel.MaxPlayers = _localMaxPlayers;

            }
        }

        private void DecreasePlayersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_localMaxPlayers > 2)
            {
                _localMaxPlayers--;
                PlayerCountBlock.Text = _localMaxPlayers.ToString();
                _viewModel.MaxPlayers = _localMaxPlayers;
            }
        }

        private void BoardTypeSpecialButton_Click(object sender, RoutedEventArgs e)
        {
            _localBoardId = 2;
            _viewModel.BoardId = 2;
            SyncVisualStyles();
        }

        private void BoardTypeNormalButton_Click(object sender, RoutedEventArgs e)
        {
            _localBoardId = 1;
            _viewModel.BoardId = 1;
            SyncVisualStyles();
        }

        private void VisibilityPublicButton_Click(object sender, RoutedEventArgs e)
        {
            _localIsPublic = true;
            _viewModel.IsPublic = true;
            SyncVisualStyles();
        }

        private void VisibilityPrivateButton_Click(object sender, RoutedEventArgs e)
        {
            _localIsPublic = false;
            _viewModel.IsPublic = false;
            SyncVisualStyles();
        }

        private void SyncVisualStyles()
        {
            PlayerCountBlock.Text = _localMaxPlayers.ToString();

            UpdateToggleStyle(BoardTypeSpecialButton, BoardTypeNormalButton, _localBoardId == 2);

            UpdateToggleStyle(VisibilityPublicButton, VisibilityPrivateButton, _localIsPublic);
        }

        private void UpdateToggleStyle(Button btnActive, Button btnInactive, bool condition)
        {
            btnActive.Style = (Style)FindResource(condition ? "LobbyToggleActiveStyle" : "LobbyToggleInactiveStyle");
            btnInactive.Style = (Style)FindResource(!condition ? "LobbyToggleActiveStyle" : "LobbyToggleInactiveStyle");
        }


        private void UpdatePlayerListManual(PlayerLobbyDTO[] players)
        {
            PlayerList.Items.Clear();
            int slotsFilled = 0;

            foreach (var p in players.OrderByDescending(x => x.IsHost))
            {
                PlayerList.Items.Add(CreatePlayerItem(p));
                slotsFilled++;
            }

            int emptySlots = _localMaxPlayers - slotsFilled;
            for (int i = 0; i < emptySlots; i++)
            {
                PlayerList.Items.Add(CreateEmptySlotItem());
            }

            if (PlayersTabHeader != null)
                PlayersTabHeader.Text = $"JUGADORES ({slotsFilled}/{_localMaxPlayers})";
        }

        private ListBoxItem CreatePlayerItem(PlayerLobbyDTO player)
        {
            var textBlock = new TextBlock { Text = player.Username, FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
            if (player.IsHost) { textBlock.Text += " (Host)"; textBlock.FontWeight = FontWeights.Bold; }
            if (player.Username == _viewModel.Username) { textBlock.Text += " (Tú)"; }

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


        private async void StartMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsLobbyCreated)
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
            try
            {
                var result = await _viewModel.CreateLobbyAsync();

                if (result.Success)
                {
                    LockLobbySettings();
                    TitleBlock.Text = $"CÓDIGO: {result.LobbyCode}";
                    StartMatchButton.Content = "Iniciar Partida";

                    var initialPlayers = new PlayerLobbyDTO[] { new PlayerLobbyDTO { Username = _viewModel.Username, IsHost = true } };
                    UpdatePlayerListManual(initialPlayers);
                }
                else
                {
                    ShowError($"Error: {result.ErrorMessage}");
                    if (result.ErrorMessage.Contains("already in a game"))
                    {
                        try { await _viewModel.DisbandLobbyAsync(); } catch { }
                    }
                    StartMatchButton.IsEnabled = true;
                }
            }
            catch (TimeoutException) { ShowError(GameClient.Resources.Strings.TimeoutLabel); }
            catch (EndpointNotFoundException) { ShowError(GameClient.Resources.Strings.EndpointNotFoundLabel); }
            catch (CommunicationException) { ShowError(GameClient.Resources.Strings.ComunicationLabel); }
        }

        private async Task HandleStartGameAsync()
        {
            int currentPlayers = PlayerList.Items.OfType<ListBoxItem>()
                    .Count(item => !((TextBlock)((StackPanel)item.Content).Children[1]).Text.Contains("Slot Vacío"));

            if (currentPlayers < 2)
            {
                MessageBox.Show("Se necesitan al menos 2 jugadores.", "Aviso");
                return;
            }

            try
            {
                bool started = await _viewModel.StartGameAsync();
                if (started)
                {
                    NavigationService.Navigate(new BoardPage(_viewModel.LobbyCode, _viewModel.BoardId, _viewModel.Username));
                }
                else
                {
                    ShowError("Error al iniciar");
                }
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void LockLobbySettings()
        {
            LobbySettingsPanel.IsEnabled = false;
            StartMatchButton.Content = "Iniciar Partida";
            StartMatchButton.IsEnabled = true;
            if (LobbyTabControl.Items.Count > 1) (LobbyTabControl.Items[1] as TabItem).IsEnabled = true;

            TitleBlock.Text = $"CÓDIGO: {_viewModel.LobbyCode}";
            CopyCodeButton.Visibility = Visibility.Visible;
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
            string friend = btn.Tag.ToString();
            _viewModel.SendInvitation(friend);
            MessageBox.Show($"Invitación enviada a {friend}.");
            btn.IsEnabled = false; btn.Content = "Enviado";
        }

        private void SendChatMessageButton_Click(object sender, RoutedEventArgs e) => SendMessage();
        private void ChatMessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SendMessage(); }

        private void SendMessage()
        {
            _viewModel.SendChatMessage(ChatMessageTextBox.Text);
            ChatMessageTextBox.Clear();
        }

        private void AddMessageToUI(string name, string message)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            textBlock.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(" " + message);
            ChatMessagesList.Items.Add(textBlock);
            ChatMessagesList.ScrollIntoView(textBlock);
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LeaveOrDisbandAsync();
            if (Window.GetWindow(this) is GameMainWindow mw) mw.ShowMainMenu();
        }

        private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_viewModel.LobbyCode);
            MessageBox.Show("Código copiado");
        }

        private void ShowError(string msg)
        {
            MessageBox.Show(msg, "Error");
            StartMatchButton.IsEnabled = true;
        }
    }
}