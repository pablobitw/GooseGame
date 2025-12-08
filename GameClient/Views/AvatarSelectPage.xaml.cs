using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.UserProfileServiceReference;
using GameClient.Models;

namespace GameClient.Views
{
    public partial class AvatarSelectPage : Page
    {
        private readonly string userEmail;
        private string selectedAvatarFileName;

        public AvatarSelectPage(string email)
        {
            InitializeComponent();
            userEmail = email;
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
                    // Fallback para desarrollo
                    string devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\Assets\Avatar"));
                    if (Directory.Exists(devPath)) avatarsDir = devPath;
                    else return;
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
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(GameClient.Resources.Strings.AvatarLoadError, ex.Message),
                                GameClient.Resources.Strings.ErrorTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void AvatarsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvatarsListBox.SelectedItem is AvatarItem selectedItem)
            {
                selectedAvatarFileName = selectedItem.FileName;
                SaveAvatarButton.IsEnabled = true;
            }
        }

        private async void SaveAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedAvatarFileName)) return;

            SaveAvatarButton.IsEnabled = false;
            var client = new UserProfileServiceClient();

            try
            {
                bool success = await client.ChangeAvatarAsync(userEmail, selectedAvatarFileName);
                if (success)
                {
                    MessageBox.Show(GameClient.Resources.Strings.AvatarUpdatedMessage,
                                    GameClient.Resources.Strings.SuccessTitle,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    GoBack();
                }
                else
                {
                    MessageBox.Show(GameClient.Resources.Strings.AvatarUpdateError,
                                    GameClient.Resources.Strings.ErrorTitle,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    SaveAvatarButton.IsEnabled = true;
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorTitle);
                SaveAvatarButton.IsEnabled = true;
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"{GameClient.Resources.Strings.ComunicationLabel}: {ex.Message}", GameClient.Resources.Strings.ErrorTitle);
                SaveAvatarButton.IsEnabled = true;
            }
            catch (Exception)
            {
                MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorTitle);
                SaveAvatarButton.IsEnabled = true;
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
                else client.Abort();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }

        private void GoBack()
        {
            if (NavigationService.CanGoBack) NavigationService.GoBack();
            else NavigationService.Navigate(new UserProfilePage(userEmail));
        }
    }
}