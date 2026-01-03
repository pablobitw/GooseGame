using GameClient.UserProfileServiceReference;
using System;
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

        public UserProfilePage(string email)
        {
            InitializeComponent();

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Sesión inválida. Por favor inicia sesión nuevamente.");
                Helpers.SessionManager.ForceLogout();
                return;
            }

            userEmail = email;
            Loaded += UserProfilePage_Loaded;

            DeactivateDialog.DialogClosed += DeactivateDialog_DialogClosed;
            DeactivateDialog.AccountDeactivated += DeactivateDialog_AccountDeactivated;
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
                    MessageBox.Show(
                        GameClient.Resources.Strings.ProfileLoadError,
                        GameClient.Resources.Strings.ErrorTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                UpdateProfileUI(profile);
                LoadAvatar(profile.AvatarPath);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.TimeoutLabel,
                    GameClient.Resources.Strings.ErrorTitle);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.EndpointNotFoundLabel,
                    GameClient.Resources.Strings.ErrorTitle);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    $"{GameClient.Resources.Strings.ComunicationLabel}: {ex.Message}",
                    GameClient.Resources.Strings.ErrorTitle);
            }
            finally
            {
                if (client.State == CommunicationState.Opened)
                    client.Close();
                else
                    client.Abort();
            }
        }

        private void UpdateProfileUI(UserProfileDto profile)
        {
            UsernameTextBox.Text = profile.Username;
            EmailTextBox?.SetCurrentValue(TextBox.TextProperty, profile.Email);
            GamesPlayedText?.SetCurrentValue(TextBlock.TextProperty, profile.MatchesPlayed.ToString());
            GamesWonText?.SetCurrentValue(TextBlock.TextProperty, profile.MatchesWon.ToString());
            CoinsText?.SetCurrentValue(TextBlock.TextProperty, profile.Coins.ToString());

            UpdateUsernameChangeLimitUI(profile.UsernameChangeCount);
        }

        private void UpdateUsernameChangeLimitUI(int changeCount)
        {
            UsernameInfoLabel.Visibility = Visibility.Visible;

            if (changeCount >= MaxUsernameChanges)
            {
                UsernameInfoLabel.Text = GameClient.Resources.Strings.LimitReachedMessage;
                UsernameInfoLabel.Foreground = Brushes.Red;
                ChangeUsernameButton.IsEnabled = false;
            }
            else
            {
                int remaining = MaxUsernameChanges - changeCount;
                UsernameInfoLabel.Text = string.Format(
                    GameClient.Resources.Strings.ChangesLeftMessage, remaining);
                UsernameInfoLabel.Foreground = Brushes.Gray;
                ChangeUsernameButton.IsEnabled = true;
            }
        }

        private void LoadAvatar(string avatarName)
        {
            string fileName = string.IsNullOrWhiteSpace(avatarName)
                ? DefaultAvatarFile
                : avatarName;

            try
            {
                string fullPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    AvatarFolder,
                    fileName);

                if (!File.Exists(fullPath)) return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                CurrentAvatarBrush.ImageSource = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando avatar: {ex.Message}");
                CurrentAvatarBrush.ImageSource = null;
            }
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
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
                await mainWindow.ShowMainMenu();
        }

        private void ShowDeactivatePopup_Click(object sender, RoutedEventArgs e)
        {
            DeactivateDialog.CurrentUserEmail = userEmail;
            DeactivateDialog.ResetFields();
            DeactivateDialog.Visibility = Visibility.Visible;
        }

        private void DeactivateDialog_DialogClosed(object sender, EventArgs e)
        {
            DeactivateDialog.Visibility = Visibility.Collapsed;
        }

        private void DeactivateDialog_AccountDeactivated(object sender, EventArgs e)
        {
            DeactivateDialog.Visibility = Visibility.Collapsed;

            MessageBox.Show(
                "Cuenta desactivada con éxito. Serás redirigido al inicio de sesión.",
                "Cuenta Desactivada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var authWindow = new AuthWindow();
            authWindow.Show();
            Window.GetWindow(this)?.Close();
        }
    }
}
