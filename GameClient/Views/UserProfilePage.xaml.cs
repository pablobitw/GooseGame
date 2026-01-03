using GameClient.UserProfileServiceReference;
using System;
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
        private readonly string userEmail;

        public UserProfilePage(string email)
        {
            InitializeComponent();
            userEmail = email;
            this.Loaded += UserProfilePage_Loaded;

            DeactivateDialog.DialogClosed += (s, e) => DeactivateDialog.Visibility = Visibility.Collapsed;
            DeactivateDialog.AccountDeactivated += DeactivateDialog_AccountDeactivated;
        }

        public UserProfilePage() : this("dev@test.com")
        {
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

                if (profile != null)
                {
                    UpdateProfileUI(profile);
                    LoadAvatar(profile.AvatarPath);
                }
                else
                {
                    MessageBox.Show(GameClient.Resources.Strings.ProfileLoadError, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorTitle);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"{GameClient.Resources.Strings.ComunicationLabel}: {ex.Message}", GameClient.Resources.Strings.ErrorTitle);
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
                else client.Abort();
            }
        }

        private void UpdateProfileUI(UserProfileDto profile)
        {
            UsernameTextBox.Text = profile.Username;
            if (EmailTextBox != null) EmailTextBox.Text = profile.Email;
            if (GamesPlayedText != null) GamesPlayedText.Text = profile.MatchesPlayed.ToString();
            if (GamesWonText != null) GamesWonText.Text = profile.MatchesWon.ToString();
            if (CoinsText != null) CoinsText.Text = profile.Coins.ToString();
            UpdateUsernameChangeLimitUI(profile.UsernameChangeCount);
        }

        private void UpdateUsernameChangeLimitUI(int changeCount)
        {
            if (changeCount >= 3)
            {
                UsernameInfoLabel.Text = GameClient.Resources.Strings.LimitReachedMessage;
                UsernameInfoLabel.Foreground = new SolidColorBrush(Colors.Red);
                UsernameInfoLabel.Visibility = Visibility.Visible;
                ChangeUsernameButton.IsEnabled = false;
            }
            else
            {
                UsernameInfoLabel.Text = string.Format(GameClient.Resources.Strings.ChangesLeftMessage, 3 - changeCount);
                UsernameInfoLabel.Foreground = new SolidColorBrush(Colors.Gray);
                UsernameInfoLabel.Visibility = Visibility.Visible;
                ChangeUsernameButton.IsEnabled = true;
            }
        }

        private void LoadAvatar(string avatarName)
        {
            if (string.IsNullOrEmpty(avatarName)) avatarName = "default_avatar.png";

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.Combine(baseDir, "Assets", "Avatar", avatarName);

                if (File.Exists(fullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    CurrentAvatarBrush.ImageSource = bitmap;
                }
            }
            catch (Exception) { }
        }

        private async void ChangeUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            var changeWindow = new ChangeUsernameWindow(userEmail);
            changeWindow.ShowDialog();
            await LoadUserProfile();
        }

        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AvatarSelectPage(userEmail));
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ResetPasswordPage(userEmail));
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null) await mainWindow.ShowMainMenu();
        }

        private void ShowDeactivatePopup_Click(object sender, RoutedEventArgs e)
        {
            DeactivateDialog.CurrentUserEmail = userEmail;
            DeactivateDialog.ResetFields();
            DeactivateDialog.Visibility = Visibility.Visible;
        }

        private void DeactivateDialog_AccountDeactivated(object sender, EventArgs e)
        {
            DeactivateDialog.Visibility = Visibility.Collapsed;
            MessageBox.Show("Cuenta desactivada con éxito. Serás redirigido al inicio de sesión.", "Cuenta Desactivada", MessageBoxButton.OK, MessageBoxImage.Information);

            var authWindow = new AuthWindow();
            authWindow.Show();
            Window.GetWindow(this)?.Close();
        }
    }
}