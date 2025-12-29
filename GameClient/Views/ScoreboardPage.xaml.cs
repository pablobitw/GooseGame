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
                MessageBox.Show("No se pudo conectar con el servidor de ranking.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("El tiempo de espera se ha agotado.", "Error de Tiempo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de comunicación con el servidor.", "Error de Red", MessageBoxButton.OK, MessageBoxImage.Error);
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