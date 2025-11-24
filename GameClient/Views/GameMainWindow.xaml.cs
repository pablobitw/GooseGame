using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.Views;

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
        private string _username;

        public GameMainWindow(string loggedInUsername)
        {
            InitializeComponent();
            _username = loggedInUsername;
        }

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string videoPath = System.IO.Path.Combine(baseDir, "Assets", "fondoloop.mp4");

                var media = (MediaElement)sender;
                media.Source = new Uri(videoPath, UriKind.Absolute);
                media.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando video: " + ex.Message);
            }
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            ((MediaElement)sender).Position = TimeSpan.FromSeconds(0);
            ((MediaElement)sender).Play();
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
            string confirmationMessage = GameClient.Resources.Strings.ConfirmExitLabel;
            string yesButtonText = GameClient.Resources.Strings.YesLabel;
            string noButtonText = GameClient.Resources.Strings.NoLabel;

            var confirmationDialog = new CustomMessageBox(confirmationMessage, yesButtonText, noButtonText);

            bool? result = confirmationDialog.ShowDialog();
            if (result == true)
            {
                Application.Current.Shutdown();
            }
        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new UserProfilePage(_username));
        }

        public void ShowMainMenu()
        {
            MainFrame.Content = null;
            MainMenuGrid.Visibility = Visibility.Visible;
        }
    }
}