using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using GameClient.UserProfileServiceReference;
using GameClient.Models;
using GameClient.Views;

namespace GameClient.Views
{
    public partial class AvatarSelectPage : Page
    {
        private readonly string _userEmail;
        private string _selectedAvatarFileName;

        public AvatarSelectPage(string email)
        {
            InitializeComponent();
            _userEmail = email;
            this.Loaded += AvatarSelectPage_Loaded;
        }

        public AvatarSelectPage() : this("dev@test.com") { }

        private void AvatarSelectPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAvatars();
        }

        private void LoadAvatars()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string avatarsDir = Path.Combine(baseDir, "Assets", "Avatar");

                if (!Directory.Exists(avatarsDir))
                {
                    string devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\Assets\Avatar"));
                    if (Directory.Exists(devPath)) avatarsDir = devPath;
                    else
                    {
                        ShowError(GameClient.Resources.Strings.AvatarLoadError);
                        return;
                    }
                }

                var files = Directory.GetFiles(avatarsDir)
                                     .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg"));

                var avatarList = new List<AvatarItem>();

                foreach (var filePath in files)
                {
                    avatarList.Add(new AvatarItem
                    {
                        FileName = Path.GetFileName(filePath),
                        FullPath = filePath
                    });
                }

                AvatarsListBox.ItemsSource = avatarList;
            }
            catch (IOException ex)
            {
                ShowError($"{GameClient.Resources.Strings.AvatarLoadError}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowError($"{GameClient.Resources.Strings.AvatarLoadError}: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowError(string.Format(GameClient.Resources.Strings.UnexpectedError, ex.Message));
            }
        }

        private void AvatarsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvatarsListBox.SelectedItem is AvatarItem selectedItem)
            {
                _selectedAvatarFileName = selectedItem.FileName;
                SaveAvatarButton.IsEnabled = true;
            }
        }

        private async void SaveAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedAvatarFileName)) return;

            SaveAvatarButton.IsEnabled = false;
            AvatarsListBox.IsEnabled = false;
            BackButton.IsEnabled = false;

            var client = new UserProfileServiceClient();

            try
            {
                bool success = await client.ChangeAvatarAsync(_userEmail, _selectedAvatarFileName);

                if (success)
                {
                    ShowSuccessOverlay();
                    client.Close();
                }
                else
                {
                    ShowError(GameClient.Resources.Strings.AvatarUpdateError);
                    client.Close();
                    ResetUiState();
                }
            }
            catch (TimeoutException)
            {
                client.Abort();
                ShowError(GameClient.Resources.Strings.TimeoutLabel);
                ResetUiState();
            }
            catch (CommunicationException ex)
            {
                client.Abort();
                ShowError($"{GameClient.Resources.Strings.ComunicationLabel}: {ex.Message}");
                ResetUiState();
            }
            catch (Exception ex)
            {
                client.Abort();
                ShowError(string.Format(GameClient.Resources.Strings.UnexpectedError, ex.Message));
                ResetUiState();
            }
        }

        private void ShowError(string message)
        {
            CustomMessageBox msgBox = new CustomMessageBox(message);
            msgBox.ShowDialog();
        }

        private void ShowSuccessOverlay()
        {
            var selectedItem = AvatarsListBox.SelectedItem as AvatarItem;
            if (selectedItem != null)
            {
                try
                {
                    SuccessAvatarImage.ImageSource = new BitmapImage(new Uri(selectedItem.FullPath));
                }
                catch (UriFormatException) { }
            }

            SuccessOverlay.Visibility = Visibility.Visible;
        }

        private void OnSuccessContinue_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }

        private void ResetUiState()
        {
            SaveAvatarButton.IsEnabled = true;
            AvatarsListBox.IsEnabled = true;
            BackButton.IsEnabled = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }

        private void GoBack()
        {
            if (NavigationService.CanGoBack) NavigationService.GoBack();
            else NavigationService.Navigate(new UserProfilePage(_userEmail));
        }
    }
}