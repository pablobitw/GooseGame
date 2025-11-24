using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using GameClient.UserProfileServiceReference;
using GameClient;

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

        private void UserProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserProfile();
        }

        private async void LoadUserProfile()
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
                        string fullPath = System.IO.Path.Combine(baseDir, "Assets", "Avatar", avatarName);

                        if (System.IO.File.Exists(fullPath))
                        {
                           
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();

                            CurrentAvatarBrush.ImageSource = bitmap;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error cargando avatar: " + ex.Message);
                    }

                    if (profile.UsernameChangeCount >= 3)
                    {
                        UsernameInfoLabel.Text = "Has alcanzado el límite de cambios de nombre (3/3).";
                        UsernameInfoLabel.Foreground = new SolidColorBrush(Colors.Red);
                        UsernameInfoLabel.Visibility = Visibility.Visible;
                        ChangeUsernameButton.IsEnabled = false;
                    }
                    else
                    {
                        UsernameInfoLabel.Text = $"Te quedan {3 - profile.UsernameChangeCount} cambios de nombre.";
                        UsernameInfoLabel.Foreground = new SolidColorBrush(Colors.Gray);
                        UsernameInfoLabel.Visibility = Visibility.Visible;
                        ChangeUsernameButton.IsEnabled = true;
                    }
                }
                else
                {
                    MessageBox.Show("No se pudo cargar el perfil del usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de conexión al cargar perfil: {ex.Message}", "Error de Red");
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
            }
        }

        private void ChangeUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            var changeWindow = new ChangeUsernameWindow(userEmail);
            changeWindow.ShowDialog();

            LoadUserProfile();
        }

        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AvatarSelectPage(userEmail));
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            //var passWindow = new ChangePasswordWindow(userEmail);
            //passWindow.ShowDialog();
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