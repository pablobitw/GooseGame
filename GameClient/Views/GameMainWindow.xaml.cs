using FontAwesome.WPF;
using GameClient.GameplayServiceReference;
using GameClient.AuthServiceReference;
using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using GameClient.UserProfileServiceReference;
using GameClient.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Net.NetworkInformation;

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
        private const string AssetsDirectory = "Assets";
        private const string BackgroundVideoFileName = "BACKGROUND_1.mp4";
        private const string AutoBanTag = "[AUTO-BAN]";
        private const string LoadingPlaceholder = "...";
        private const string ErrorPlaceholder = "---";
        private const string LogErrorPrefix = "[GameMainWindow] Error inicializando servicios: ";
        private const int JoinLobbyDelayMs = 200;
        private const double CancelColumnWidthStar = 1.0;

        private readonly string _username;
        private Action _onDialogConfirmAction;
        private bool _isExplicitLogout = false;

        public GameMainWindow(string loggedInUsername)
        {
            InitializeComponent();
            _username = loggedInUsername;

            try
            {
                FriendshipServiceManager.Initialize(_username);
                LobbyServiceManager.Instance.Initialize(_username);
                GameplayServiceManager.Instance.Initialize(_username);

                FriendshipServiceManager.Instance.GameInvitationReceived += HandleInvitation;

                LobbyServiceManager.Instance.PlayerKicked += OnGlobalPlayerKicked;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{LogErrorPrefix}{ex.Message}");
            }

            this.Closing += GameMainWindow_Closing;

            _ = LoadUserCurrency();

            AudioManager.PlayRandomMusic(AudioManager.MenuTracks);
        }

        private void ShowOverlayDialog(string title, string message, FontAwesomeIcon icon, bool isConfirmation = false, Action onConfirm = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Icon = icon;
            DialogCancelBtn.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
            DialogCancelColumn.Width = isConfirmation ? new GridLength(CancelColumnWidthStar, GridUnitType.Star) : new GridLength(0);
            DialogConfirmBtn.Content = isConfirmation ? GameClient.Resources.Strings.DialogConfirmBtn : GameClient.Resources.Strings.DialogOkBtn;
            DialogCancelBtn.Content = GameClient.Resources.Strings.DialogCancelBtn;
            _onDialogConfirmAction = onConfirm;
            CustomDialogOverlay.Visibility = Visibility.Visible;
        }

        private void HandleLobbyErrorWithOverlay(LobbyErrorType errorType, string fallbackMessage)
        {
            string message = fallbackMessage;
            string title = GameClient.Resources.Strings.DialogErrorTitle;
            FontAwesomeIcon icon = FontAwesomeIcon.TimesCircle;

            switch (errorType)
            {
                case LobbyErrorType.DatabaseError:
                    message = GameClient.Resources.Strings.InvitationAccept_DatabaseError;
                    icon = FontAwesomeIcon.Database;
                    break;
                case LobbyErrorType.ServerTimeout:
                    message = GameClient.Resources.Strings.SafeZone_ServerTimeout;
                    icon = FontAwesomeIcon.ClockOutline;
                    break;
                case LobbyErrorType.GameFull:
                    message = GameClient.Resources.Strings.LobbyError_Full;
                    icon = FontAwesomeIcon.Users;
                    break;
                case LobbyErrorType.GameStarted:
                    message = GameClient.Resources.Strings.LobbyError_Started;
                    icon = FontAwesomeIcon.PlayCircle;
                    break;
                case LobbyErrorType.GameNotFound:
                    message = GameClient.Resources.Strings.LobbyError_NotFound;
                    icon = FontAwesomeIcon.Search;
                    break;
                case LobbyErrorType.PlayerAlreadyInGame:
                    message = GameClient.Resources.Strings.LobbyError_AlreadyInGame;
                    icon = FontAwesomeIcon.ExclamationTriangle;
                    break;
                case LobbyErrorType.GuestNotAllowed:
                    message = GameClient.Resources.Strings.FriendGuestRestriction;
                    icon = FontAwesomeIcon.UserSecret;
                    break;
            }

            ShowOverlayDialog(title, message, icon);
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            CustomDialogOverlay.Visibility = Visibility.Collapsed;

            if (sender == DialogConfirmBtn)
            {
                _onDialogConfirmAction?.Invoke();
            }

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

                bool isBan = reason != null && reason.Contains(AutoBanTag);

                ShowOverlayDialog(
                    GameClient.Resources.Strings.KickedTitle,
                    string.Format(GameClient.Resources.Strings.KickedGlobalMsg, reason),
                    FontAwesomeIcon.ExclamationTriangle,
                    false,
                    () =>
                    {
                        if (isBan)
                        {
                            ForceLogoutAndClose();
                        }
                        else
                        {
                            _ = ShowMainMenu();
                        }
                    }
                );
            });
        }

        private void ForceLogoutAndClose()
        {
            try
            {
                _isExplicitLogout = true;
                UserSession.GetInstance().Logout();
                AuthWindow authWindow = new AuthWindow();
                Application.Current.MainWindow = authWindow;
                authWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during forced logout: " + ex.Message);
                this.Close();
            }
        }

        private async Task LoadUserCurrency()
        {
            try
            {
                CoinCountText.Text = LoadingPlaceholder;

                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    CoinCountText.Text = ErrorPlaceholder;
                    return;
                }

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
                CoinCountText.Text = ErrorPlaceholder;
            }
            catch (EndpointNotFoundException)
            {
                CoinCountText.Text = ErrorPlaceholder;
            }
            catch (CommunicationException)
            {
                CoinCountText.Text = ErrorPlaceholder;
            }
            catch (Exception)
            {
                CoinCountText.Text = ErrorPlaceholder;
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
                    FontAwesomeIcon.UserSecret,
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
            Application.Current.MainWindow = authWindow;
            authWindow.Show();
            authWindow.NavigateToRegister();
            _isExplicitLogout = true;
            this.Close();
        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted(GameClient.Resources.Strings.ProfileFeatureName))
            {
                return;
            }

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new UserProfilePage(_username));
        }

        private void FriendsButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted(GameClient.Resources.Strings.FriendsFeatureName))
            {
                return;
            }

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new FriendshipPage(_username));
        }

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string videoPath = Path.Combine(baseDir, AssetsDirectory, BackgroundVideoFileName);

                if (File.Exists(videoPath))
                {
                    var media = (MediaElement)sender;
                    media.Source = new Uri(videoPath, UriKind.Absolute);
                    media.LoadedBehavior = MediaState.Manual;
                    media.Play();
                }
                else
                {
                    Debug.WriteLine($"Video file not found: {videoPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error loading video: {ex.Message}");
            }
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
                FontAwesomeIcon.SignOut,
                true,
                async () =>
                {
                    await PerformLogoutAsync();
                    _isExplicitLogout = true;
                    Application.Current.Shutdown();
                }
            );
        }

        private void ShopButtonClick(object sender, RoutedEventArgs e)
        {
            ShowOverlayDialog(
                GameClient.Resources.Strings.ShopPendingTitle,
                GameClient.Resources.Strings.ShopPendingMsg,
                FontAwesomeIcon.ShoppingBag
            );
        }

        private void LeaderboardButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted(GameClient.Resources.Strings.LeaderboardFeatureName))
            {
                return;
            }

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new ScoreboardPage(_username));
        }

        private async Task PerformLogoutAsync()
        {
            DisposeServices();

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                try
                {
                    using (var client = new AuthServiceClient())
                    {
                        await client.LogoutAsync(_username);
                    }
                }
                catch (Exception) { }
            }

            UserSession.GetInstance().Logout();
        }

        private void DisposeServices()
        {
            try
            {
                if (LobbyServiceManager.Instance != null)
                {
                    LobbyServiceManager.Instance.PlayerKicked -= OnGlobalPlayerKicked;
                    LobbyServiceManager.Instance.Dispose();
                }

                if (GameplayServiceManager.Instance != null)
                {
                    GameplayServiceManager.Instance.Dispose();
                }

                if (FriendshipServiceManager.Instance != null)
                {
                    FriendshipServiceManager.Instance.Disconnect();
                }
            }
            catch { }
        }

        private async void GameMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Application.Current.MainWindow != this)
            {
                DisposeServices();
                return;
            }

            if (!_isExplicitLogout)
            {
                e.Cancel = true;
                _isExplicitLogout = true;

                try
                {
                    await PerformLogoutAsync();
                }
                catch { }

                Application.Current.Shutdown();
            }
        }

        public async Task ShowMainMenu()
        {
            AudioManager.StopMusic();
            AudioManager.PlayRandomMusic(AudioManager.MenuTracks);

            MainFrame.Content = null;

            while (MainFrame.CanGoBack)
            {
                MainFrame.RemoveBackEntry();
            }

            MainMenuGrid.Visibility = Visibility.Visible;
            await LoadUserCurrency();
        }

        private async void HandleInvitation(string host, string code)
        {
            if (UserSession.GetInstance().IsGuest)
            {
                return;
            }

            await this.Dispatcher.InvokeAsync(() =>
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.InvitationTitle,
                    string.Format(GameClient.Resources.Strings.InvitationMessage, host),
                    FontAwesomeIcon.Gamepad,
                    true,
                    async () => await AttemptJoinLobbyAsync(code)
                );
            });
        }

        private async Task AttemptJoinLobbyAsync(string code)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.DialogErrorTitle,
                    GameClient.Resources.Strings.InvitationAccept_NoInternet,
                    FontAwesomeIcon.Wifi);
                return;
            }

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
                    await Task.Delay(JoinLobbyDelayMs);
                    MainMenuGrid.Visibility = Visibility.Collapsed;
                    MainFrame.Navigate(new LobbyPage(_username, code, joinResult));
                }
                else
                {
                    HandleLobbyErrorWithOverlay(joinResult.ErrorType, joinResult.ErrorMessage);
                }
            }
            catch (EndpointNotFoundException)
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.DialogErrorTitle,
                    GameClient.Resources.Strings.InvitationAccept_ServerDown,
                    FontAwesomeIcon.Server);
            }
            catch (TimeoutException)
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.DialogErrorTitle,
                    GameClient.Resources.Strings.InvitationAccept_ServerTimeout,
                    FontAwesomeIcon.ClockOutline);
            }
            catch (CommunicationException)
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.DialogErrorTitle,
                    GameClient.Resources.Strings.InvitationAccept_CommunicationError,
                    FontAwesomeIcon.Wifi);
            }
            catch (Exception ex)
            {
                ShowOverlayDialog(
                    GameClient.Resources.Strings.DialogErrorTitle,
                    string.Format(GameClient.Resources.Strings.InvitationAccept_UnexpectedError, ex.Message),
                    FontAwesomeIcon.ExclamationTriangle);
            }
        }

        private void HowToPlayButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new HowToPlayPage());
        }
    }
}