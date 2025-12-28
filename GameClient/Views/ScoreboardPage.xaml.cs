using GameClient.UserProfileServiceReference;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.LeaderboardServiceReference;

namespace GameClient.Views
{
    public partial class ScoreboardPage : Page
    {
        private string _username;

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

        private async System.Threading.Tasks.Task LoadLeaderboardAsync()
        {
            try
            {
                LeaderboardList.ItemsSource = null;

                using (var client = new GameClient.LeaderboardServiceReference.LeaderboardServiceClient())
                {
                    var leaderboardData = await client.GetGlobalLeaderboardAsync(_username);
                    LeaderboardList.ItemsSource = leaderboardData;
                }
            }
            catch (System.ServiceModel.EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor de ranking.", "Error de Conexión");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar la tabla: " + ex.Message);
            }
        }

        private void BackButtonClick(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }
        }
    }
}