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
                    UsernameTextBox.Text = profile.Username;

                    if (EmailTextBox != null) EmailTextBox.Text = profile.Email;
                    if (GamesPlayedText != null) GamesPlayedText.Text = profile.MatchesPlayed.ToString();
                    if (GamesWonText != null) GamesWonText.Text = profile.MatchesWon.ToString();
                    if (CoinsText != null) CoinsText.Text = profile.Coins.ToString();

                    string avatarName = profile.AvatarPath;
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
                    catch (UriFormatException)
                    {
                        Console.WriteLine(GameClient.Resources.Strings.AvatarFormatError);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine(GameClient.Resources.Strings.AvatarReadError);
                    }

                    if (profile.UsernameChangeCount >= 3)
                    {
                        UsernameInfoLabel.Text = GameClient.Resources.Strings.LimitReachedMessage;
                        UsernameInfoLabel.Foreground = new SolidColorBrush(Colors.Red);
                        UsernameInfoLabel.Visibility = Visibility.Visible;
                        ChangeUsernameButton.IsEnabled = false;
                    }
                    else
                    {
                        UsernameInfoLabel.Text = string.Format(GameClient.Resources.Strings.ChangesLeftMessage, 3 - profile.UsernameChangeCount);
                        UsernameInfoLabel.Foreground = new SolidColorBrush(Colors.Gray);
                        UsernameInfoLabel.Visibility = Visibility.Visible;
                        ChangeUsernameButton.IsEnabled = true;
                    }
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
                if (client.State == CommunicationState.Opened)
                {
                    client.Close();
                }
                else
                {
                    client.Abort();
                }
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                mainWindow.ShowMainMenu();
            }
        }
    }
}