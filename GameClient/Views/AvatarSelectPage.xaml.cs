using System;
using System.Collections.Generic; 
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
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
                string avatarsDir = System.IO.Path.Combine(baseDir, "Assets", "Avatar");

                if (!Directory.Exists(avatarsDir))
                {
                    string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\Assets\Avatar"));
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
                        FileName = System.IO.Path.GetFileName(filePath),
                        FullPath = filePath 
                    });
                }

                AvatarsListBox.ItemsSource = avatarList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando avatares: " + ex.Message);
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
                    MessageBox.Show("¡Avatar actualizado!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    GoBack();
                }
                else
                {
                    MessageBox.Show("Error al actualizar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    SaveAvatarButton.IsEnabled = true;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error de conexión.");
                SaveAvatarButton.IsEnabled = true;
            }
            finally
            {
                if (client.State == CommunicationState.Opened) client.Close();
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