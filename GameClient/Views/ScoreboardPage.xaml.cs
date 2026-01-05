using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameClient.LeaderboardServiceReference;

namespace GameClient.Views
{
    public partial class ScoreboardPage : Page
    {
        private readonly string _username;

        public ScoreboardPage(string username)
        {
            InitializeComponent();
            _username = username;
            this.Loaded += ScoreboardPage_Loaded;
        }

        private async void ScoreboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLeaderboardAsync();
        }

        private async Task LoadLeaderboardAsync()
        {
            try
            {
                LeaderboardList.ItemsSource = null;

                using (var client = new LeaderboardServiceClient())
                {
                    var leaderboardData = await client.GetGlobalLeaderboardAsync(_username);
                    LeaderboardList.ItemsSource = leaderboardData;
                }
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorLeaderboardConnection, GameClient.Resources.Strings.DialogErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorTitle, GameClient.Resources.Strings.DialogErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorTitle, GameClient.Resources.Strings.DialogErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BackButtonClick(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                await mainWindow.ShowMainMenu();
            }
        }
    }
}