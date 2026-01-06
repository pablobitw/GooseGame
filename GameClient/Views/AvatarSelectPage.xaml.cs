using GameClient.Models;
using GameClient.UserProfileServiceReference;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

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

        private static void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GameClient.Resources.Strings.ResourceManager.GetString(messageKey);
            string title = GameClient.Resources.Strings.ResourceManager.GetString(titleKey);
            MessageBox.Show(message ?? messageKey, title ?? titleKey, MessageBoxButton.OK, icon);
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
                    if (Directory.Exists(devPath))
                    {
                        avatarsDir = devPath;
                    }
                    else
                    {
                        ShowTranslatedMessageBox("Avatar_Error_LoadFiles", "Avatar_Title_Error", MessageBoxImage.Error);
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
                string msg = GameClient.Resources.Strings.Avatar_Error_LoadFiles + "\n" + ex.Message;
                MessageBox.Show(msg, GameClient.Resources.Strings.Avatar_Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                string msg = GameClient.Resources.Strings.Avatar_Error_LoadFiles + "\n" + ex.Message;
                MessageBox.Show(msg, GameClient.Resources.Strings.Avatar_Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string msg = GameClient.Resources.Strings.Avatar_Error_General + "\n" + ex.Message;
                MessageBox.Show(msg, GameClient.Resources.Strings.Avatar_Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Avatar_Error_NoInternet", "Avatar_Title_Error", MessageBoxImage.Error);
                return;
            }

            SetUiEnabled(false);
            var client = new UserProfileServiceClient();

            try
            {
                bool success = await client.ChangeAvatarAsync(_userEmail, _selectedAvatarFileName);

                if (success)
                {
                    ShowSuccessOverlay();
                }
                else
                {
                    ShowTranslatedMessageBox("Avatar_Error_UpdateFailed", "Avatar_Title_Error", MessageBoxImage.Warning);
                    SetUiEnabled(true);
                }
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Avatar_Error_Timeout", "Avatar_Title_Error", MessageBoxImage.Warning);
                SetUiEnabled(true);
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Avatar_Error_ServerDown", "Avatar_Title_Error", MessageBoxImage.Error);
                SetUiEnabled(true);
            }
            catch (FaultException)
            {
                ShowTranslatedMessageBox("Avatar_Error_Database", "Avatar_Title_Error", MessageBoxImage.Error);
                SetUiEnabled(true);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Avatar_Error_Communication", "Avatar_Title_Error", MessageBoxImage.Error);
                SetUiEnabled(true);
            }
            catch (Exception ex)
            {
                string general = GameClient.Resources.Strings.Avatar_Error_General;
                string title = GameClient.Resources.Strings.Avatar_Title_Error;
                MessageBox.Show($"{general}\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
                SetUiEnabled(true);
            }
            finally
            {
                CloseClientSafely(client);
            }
        }

        private static void CloseClientSafely(UserProfileServiceClient client)
        {
            try
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
            catch (Exception)
            {
                client.Abort();
            }
        }

        private void SetUiEnabled(bool isEnabled)
        {
            SaveAvatarButton.IsEnabled = isEnabled;
            AvatarsListBox.IsEnabled = isEnabled;
            BackButton.IsEnabled = isEnabled;
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
                catch (UriFormatException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AvatarSelectPage] Error de formato URI al cargar preview: {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AvatarSelectPage] Error inesperado cargando imagen de éxito: {ex.Message}");
                }
            }

            SuccessOverlay.Visibility = Visibility.Visible;
        }

        private void OnSuccessContinue_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }

        private void GoBack()
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                NavigationService.Navigate(new UserProfilePage(_userEmail));
            }
        }
    }
}