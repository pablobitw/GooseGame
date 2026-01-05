using GameClient.GameServiceReference;
using GameClient.GameplayServiceReference;
using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using GameClient.UserProfileServiceReference;
using GameClient.Views;
using System;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
        private readonly string _username;
        private Action _onDialogConfirmAction;

        public GameMainWindow(string loggedInUsername)
        {
            InitializeComponent();
            _username = loggedInUsername;

            FriendshipServiceManager.Initialize(_username);
            LobbyServiceManager.Instance.Initialize(_username);
            GameplayServiceManager.Instance.Initialize(_username);

            FriendshipServiceManager.Instance.GameInvitationReceived += HandleInvitation;
            LobbyServiceManager.Instance.PlayerKicked += OnGlobalPlayerKicked;
            GameplayServiceManager.Instance.PlayerKicked += OnGlobalPlayerKicked;

            this.Closed += GameMainWindow_Closed;

            _ = LoadUserCurrency();

            AudioManager.PlayRandomMusic(AudioManager.MenuTracks);
        }

        private void ShowOverlayDialog(string title, string message, FontAwesome.WPF.FontAwesomeIcon icon, bool isConfirmation = false, Action onConfirm = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Icon = icon;
            DialogCancelBtn.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
            DialogCancelColumn.Width = isConfirmation ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            DialogConfirmBtn.Content = isConfirmation ? GameClient.Resources.Strings.DialogConfirmBtn : GameClient.Resources.Strings.DialogOkBtn;
            DialogCancelBtn.Content = GameClient.Resources.Strings.DialogCancelBtn;
            _onDialogConfirmAction = onConfirm;
            CustomDialogOverlay.Visibility = Visibility.Visible;
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            CustomDialogOverlay.Visibility = Visibility.Collapsed;
            if (sender == DialogConfirmBtn) _onDialogConfirmAction?.Invoke();
            _onDialogConfirmAction = null;
        }

        private void OnGlobalPlayerKicked(string reason)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainFrame.Content is BoardPage boardPage)
                {
                    boardPage.StopTimers();
                }

                ShowOverlayDialog(
                    GameClient.Resources.Strings.KickedTitle,
                    string.Format(GameClient.Resources.Strings.KickedGlobalMsg, reason),
                    FontAwesome.WPF.FontAwesomeIcon.ExclamationTriangle,
                    false,
                    () => _ = ShowMainMenu()
                );
            });
        }

        private async Task LoadUserCurrency()
        {
            try
            {
                CoinCountText.Text = "...";

                using (var client = new UserProfileServiceClient())
                {
                    var userProfile = await client.GetUserProfileAsync(_username);

                    if (userProfile != null)
                    {
                        CoinCountText.Text = userProfile.Coins.ToString();
                    }
                }
            }
            catch (TimeoutException)
            {
                CoinCountText.Text = "---";
                Console.WriteLine(GameClient.Resources.Strings.ErrorCoinFetch);
            }
            catch (EndpointNotFoundException)
            {
                CoinCountText.Text = "---";
                Console.WriteLine(GameClient.Resources.Strings.ErrorDatabaseUnreachable);
            }
            catch (CommunicationException)
            {
                CoinCountText.Text = "---";
            }
            catch (Exception ex)
            {
                CoinCountText.Text = "Err";
                Console.WriteLine(ex.Message);
            }
        }

        private bool IsGuestActionRestricted(string featureName)
        {
            if (UserSession.GetInstance().IsGuest)
            {
                string message = string.Format(GameClient.Resources.Strings.GuestRestrictedMsg, featureName);

                ShowOverlayDialog(
                    GameClient.Resources.Strings.GuestRestrictedTitle,
                    message,
                    FontAwesome.WPF.FontAwesomeIcon.UserSecret,
                    true,
                    () => ReturnToRegister()
                );

                return true;
            }
            return false;
        }

        private void ReturnToRegister()
        {
            AuthWindow authWindow = new AuthWindow();
            authWindow.Show();
            authWindow.NavigateToRegister();
            this.Close();
        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted(GameClient.Resources.Strings.ProfileFeatureName)) return;

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new UserProfilePage(_username));
        }

        private void FriendsButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted(GameClient.Resources.Strings.FriendsFeatureName)) return;

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new FriendshipPage(_username));
        }

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string videoPath = Path.Combine(baseDir, "Assets", "BACKGROUND_1.mp4");

                if (File.Exists(videoPath))
                {
                    var media = (MediaElement)sender;
                    media.Source = new Uri(videoPath, UriKind.Absolute);
                    media.LoadedBehavior = MediaState.Manual;
                    media.Play();
                }
            }
            catch (ArgumentException) { }
            catch (UriFormatException) { }
            catch (IOException) { }
            catch (Exception) { }
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            var media = (MediaElement)sender;
            media.Position = TimeSpan.Zero;
            media.Play();
        }

        private void PlayButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new CreateOrJoinMatchPage(_username));
        }

        private void OptionsButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new OptionsPage());
        }

        private void QuitButtonClick(object sender, RoutedEventArgs e)
        {
            ShowOverlayDialog(
                GameClient.Resources.Strings.DialogConfirmTitle,
                GameClient.Resources.Strings.ConfirmExitLabel,
                FontAwesome.WPF.FontAwesomeIcon.SignOut,
                true,
                () => this.Close()
            );
        }

        private void ShopButtonClick(object sender, RoutedEventArgs e)
        {
            ShowOverlayDialog(
                GameClient.Resources.Strings.ShopPendingTitle,
                GameClient.Resources.Strings.ShopPendingMsg,
                FontAwesome.WPF.FontAwesomeIcon.ShoppingBag
            );
        }

        private void LeaderboardButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted(GameClient.Resources.Strings.LeaderboardFeatureName)) return;

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new ScoreboardPage(_username));
        }

        private async void GameMainWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                if (LobbyServiceManager.Instance != null) LobbyServiceManager.Instance.PlayerKicked -= OnGlobalPlayerKicked;
                if (GameplayServiceManager.Instance != null) GameplayServiceManager.Instance.PlayerKicked -= OnGlobalPlayerKicked;

                if (FriendshipServiceManager.Instance != null)
                {
                    FriendshipServiceManager.Instance.Disconnect();
                }

                LobbyServiceManager.Instance.Dispose();
                GameplayServiceManager.Instance.Dispose();

                using (var client = new GameServiceClient())
                {
                    await client.LogoutAsync(_username);
                }

                UserSession.GetInstance().Logout();
            }
            catch (CommunicationException) { }
            catch (TimeoutException) { }
            catch (Exception) { }
            finally
            {
                if (Application.Current.Windows.Count == 0)
                {
                    Application.Current.Shutdown();
                }
            }
        }

        public async Task ShowMainMenu()
        {
            AudioManager.StopMusic();
            AudioManager.PlayRandomMusic(AudioManager.MenuTracks);

            MainFrame.Content = null;
            while (MainFrame.CanGoBack) MainFrame.RemoveBackEntry();
            MainMenuGrid.Visibility = Visibility.Visible;
            await LoadUserCurrency();
        }

        private async void HandleInvitation(string host, string code)
        {
            if (UserSession.GetInstance().IsGuest) return;

            await this.Dispatcher.InvokeAsync(() =>
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.InvitationTitle,
                    string.Format(GameClient.Resources.Strings.InvitationMessage, host),
                    FontAwesome.WPF.FontAwesomeIcon.Gamepad,
                    true,
                    async () => await AttemptJoinLobbyAsync(code)
                );
            });
        }

        private async Task AttemptJoinLobbyAsync(string code)
        {
            try
            {
                var request = new JoinLobbyRequest
                {
                    LobbyCode = code,
                    Username = _username
                };

                var joinResult = await LobbyServiceManager.Instance.JoinLobbyAsync(request);

                if (joinResult.Success)
                {
                    await Task.Delay(200);
                    MainMenuGrid.Visibility = Visibility.Collapsed;
                    MainFrame.Navigate(new LobbyPage(_username, code, joinResult));
                }
                else
                {
                    ShowOverlayDialog(
                        GameClient.Resources.Strings.DialogErrorTitle,
                        string.Format(GameClient.Resources.Strings.ErrorJoinLobby, joinResult.ErrorMessage),
                        FontAwesome.WPF.FontAwesomeIcon.TimesCircle
                    );
                }
            }
            catch (CommunicationException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorInviteComm, FontAwesome.WPF.FontAwesomeIcon.Wifi);
            }
            catch (TimeoutException)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, GameClient.Resources.Strings.ErrorInviteTimeout, FontAwesome.WPF.FontAwesomeIcon.ClockOutline);
            }
            catch (Exception ex)
            {
                ShowOverlayDialog(GameClient.Resources.Strings.DialogErrorTitle, ex.Message, FontAwesome.WPF.FontAwesomeIcon.TimesCircle);
            }
        }

        private void HowToPlayButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new HowToPlayPage());
        }
    }
}