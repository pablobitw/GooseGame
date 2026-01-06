using GameClient.UserProfileServiceReference;
using GameClient.Views.Components;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class UserProfilePage : Page
    {
        private const int MaxUsernameChanges = 3;
        private const string DefaultAvatarFile = "default_avatar.png";
        private static readonly string AvatarFolder = Path.Combine("Assets", "Avatar");

        private readonly string userEmail;
        private ObservableCollection<string> _socialLinks;
        private Action _onConfirmAction;

        public UserProfilePage(string email)
        {
            InitializeComponent();

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowErrorMessage("Sesión inválida. Por favor inicia sesión nuevamente.");
                Helpers.UserSession.GetInstance().HandleCatastrophicError("Sesión inválida.");
                return;
            }

            userEmail = email;
            _socialLinks = new ObservableCollection<string>();
            SocialLinksPanel.ItemsSource = _socialLinks;

            Loaded += UserProfilePage_Loaded;
            DeactivateDialog.DialogClosed += DeactivateDialog_DialogClosed;
            DeactivateDialog.AccountDeactivated += DeactivateDialog_AccountDeactivated;
            AddLinkPopup.DialogClosed += AddLinkPopup_DialogClosed;
            AddLinkPopup.LinkAdded += AddLinkPopup_LinkAdded;
        }

        private void ShowCustomDialog(string title, string message, FontAwesome.WPF.FontAwesomeIcon icon, bool isConfirmation = false, Action onConfirm = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Icon = icon;
            CancelBtn.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
            CancelColumn.Width = isConfirmation ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            ConfirmBtn.Content = isConfirmation ? GameClient.Resources.Strings.DialogConfirmBtn : GameClient.Resources.Strings.DialogOkBtn;
            CancelBtn.Content = GameClient.Resources.Strings.DialogCancelBtn;
            _onConfirmAction = onConfirm;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            if (sender == ConfirmBtn)
            {
                _onConfirmAction?.Invoke();
            }
            _onConfirmAction = null;
        }

        private async void UserProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserProfile();
        }

        private async Task LoadUserProfile()
        {
            var client = new UserProfileServiceClient();
            try
            {
                var profile = await client.GetUserProfileAsync(userEmail);
                if (profile == null)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_Database);
                    return;
                }
                UpdateProfileUI(profile);
                LoadAvatar(profile.AvatarPath);
            }
            catch (EndpointNotFoundException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_ServerDown);
            }
            catch (TimeoutException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_Timeout);
            }
            catch (FaultException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_Database);
            }
            catch (CommunicationException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.Login_Error_Communication);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_General + "\n" + ex.Message);
            }
            finally
            {
                CloseClient(client);
            }
        }

        private static void CloseClient(UserProfileServiceClient client)
        {
            try
            {
                if (client.State == CommunicationState.Opened) client.Close();
                else client.Abort();
            }
            catch (Exception) { client.Abort(); }
        }

        private void UpdateProfileUI(UserProfileDto profile)
        {
            UsernameTextBox.Text = profile.Username;
            EmailTextBox?.SetCurrentValue(TextBox.TextProperty, profile.Email);
            GamesPlayedText?.SetCurrentValue(TextBlock.TextProperty, profile.MatchesPlayed.ToString());
            GamesWonText?.SetCurrentValue(TextBlock.TextProperty, profile.MatchesWon.ToString());
            CoinsText?.SetCurrentValue(TextBlock.TextProperty, profile.Coins.ToString());

            UpdateUsernameChangeLimitUI(profile.UsernameChangeCount);

            _socialLinks.Clear();
            if (profile.SocialLinks != null)
            {
                foreach (var link in profile.SocialLinks)
                {
                    _socialLinks.Add(link.Url);
                }
            }
        }

        private void UpdateUsernameChangeLimitUI(int changeCount)
        {
            UsernameInfoLabel.Visibility = Visibility.Visible;
            if (changeCount >= MaxUsernameChanges)
            {
                UsernameInfoLabel.Text = GameClient.Resources.Strings.Profile_User_Limit;
                UsernameInfoLabel.Foreground = Brushes.Red;
                ChangeUsernameButton.IsEnabled = false;
            }
            else
            {
                int remaining = MaxUsernameChanges - changeCount;
                UsernameInfoLabel.Text = string.Format(GameClient.Resources.Strings.ChangesLeftMessage, remaining);
                UsernameInfoLabel.Foreground = Brushes.Gray;
                ChangeUsernameButton.IsEnabled = true;
            }
        }

        private void LoadAvatar(string avatarName)
        {
            string fileName = string.IsNullOrWhiteSpace(avatarName) ? DefaultAvatarFile : avatarName;
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AvatarFolder, fileName);
                if (!File.Exists(fullPath)) return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                CurrentAvatarBrush.ImageSource = bitmap;
            }
            catch (Exception) { CurrentAvatarBrush.ImageSource = null; }
        }

        private async void ChangeUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            var changeWindow = new ChangeUsernameWindow(userEmail);
            changeWindow.ShowDialog();
            await LoadUserProfile();
        }

        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AvatarSelectPage(userEmail));
        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new ResetPasswordPage(userEmail));

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
                await mainWindow.ShowMainMenu();
        }

        private void AddSocialLinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_socialLinks.Count >= 3)
            {
                ShowCustomDialog(GameClient.Resources.Strings.SocialLimitTitle, GameClient.Resources.Strings.Profile_Social_Limit, FontAwesome.WPF.FontAwesomeIcon.InfoCircle);
                return;
            }
            AddLinkPopup.Reset();
            AddLinkPopup.Visibility = Visibility.Visible;
        }

        private void AddLinkPopup_DialogClosed(object sender, EventArgs e) => AddLinkPopup.Visibility = Visibility.Collapsed;

        private async void AddLinkPopup_LinkAdded(object sender, string url)
        {
            AddLinkPopup.Visibility = Visibility.Collapsed;
            var client = new UserProfileServiceClient();
            try
            {
                string error = await client.AddSocialLinkAsync(userEmail, url);
                if (error == null)
                {
                    ShowSuccessMessage(GameClient.Resources.Strings.Profile_Social_Added);
                    await LoadUserProfile();
                }
                else
                {
                    ShowCustomDialog(GameClient.Resources.Strings.DialogWarningTitle, error, FontAwesome.WPF.FontAwesomeIcon.ExclamationTriangle);
                }
            }
            catch (EndpointNotFoundException) { ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_ServerDown); }
            catch (TimeoutException) { ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_Timeout); }
            catch (Exception ex)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle + ": " + ex.Message);
            }
            finally
            {
                CloseClient(client);
            }
        }

        private void DeleteSocialLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string urlToRemove)
            {
                ShowCustomDialog(GameClient.Resources.Strings.DialogConfirmTitle, GameClient.Resources.Strings.Profile_Social_Delete, FontAwesome.WPF.FontAwesomeIcon.QuestionCircle, true, async () =>
                {
                    var client = new UserProfileServiceClient();
                    try
                    {
                        bool success = await client.RemoveSocialLinkAsync(userEmail, urlToRemove);
                        if (success) await LoadUserProfile();
                        else ShowErrorMessage(GameClient.Resources.Strings.LinkDeleteError);
                    }
                    catch (EndpointNotFoundException) { ShowErrorMessage(GameClient.Resources.Strings.Profile_Error_ServerDown); }
                    catch (Exception ex) { ShowErrorMessage("Error: " + ex.Message); }
                    finally
                    {
                        CloseClient(client);
                    }
                });
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { ShowErrorMessage("No se pudo abrir el enlace."); }
        }

        private void ShowDeactivatePopup_Click(object sender, RoutedEventArgs e)
        {
            DeactivateDialog.CurrentUserEmail = userEmail;
            DeactivateDialog.ResetFields();
            DeactivateDialog.Visibility = Visibility.Visible;
        }

        private void DeactivateDialog_DialogClosed(object sender, EventArgs e) => DeactivateDialog.Visibility = Visibility.Collapsed;

        private void DeactivateDialog_AccountDeactivated(object sender, EventArgs e)
        {
            DeactivateDialog.Visibility = Visibility.Collapsed;
            ShowCustomDialog(GameClient.Resources.Strings.DeactivatedSuccessTitle, GameClient.Resources.Strings.Profile_Deactivate_Success, FontAwesome.WPF.FontAwesomeIcon.SignOut, false, () =>
            {
                var authWindow = new AuthWindow();
                authWindow.Show();
                Window.GetWindow(this)?.Close();
            });
        }

        private void ShowErrorMessage(string message) => ShowCustomDialog(GameClient.Resources.Strings.DialogErrorTitle, message, FontAwesome.WPF.FontAwesomeIcon.TimesCircle);
        private void ShowSuccessMessage(string message) => ShowCustomDialog(GameClient.Resources.Strings.DialogSuccessTitle, message, FontAwesome.WPF.FontAwesomeIcon.CheckCircle);
    }
}