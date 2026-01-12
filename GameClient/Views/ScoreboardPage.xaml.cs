using GameClient.LeaderboardServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GameClient.Views
{
    public partial class ScoreboardPage : Page
    {
        private readonly string _username;

        public ScoreboardPage(string username)
        {
            InitializeComponent();
            _username = username;
            Loaded += ScoreboardPage_Loaded;
        }

        private async void ScoreboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLeaderboardAsync();
        }

        private async Task LoadLeaderboardAsync()
        {
            LeaderboardList.ItemsSource = null;

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorNetworkInterruption,
                                GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var client = new LeaderboardServiceClient();

            try
            {
                var leaderboardData = await client.GetGlobalLeaderboardAsync(_username);

                if (leaderboardData == null || leaderboardData.Length == 0)
                {
                    MessageBox.Show(GameClient.Resources.Strings.LabelEmptyLeaderboard,
                                    GameClient.Resources.Strings.InformationTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LeaderboardList.ItemsSource = leaderboardData;
                }
            }
            catch (FaultException<ServiceFault> fault)
            {
                string errorMessage = GameClient.Resources.Strings.ErrorLeaderboardGeneral;

                if (fault.Detail.Code == "Error_DatabaseDown" || fault.Detail.Code == "DbError")
                {
                    errorMessage = GameClient.Resources.Strings.ErrorLeaderboardDatabase;
                }
                else if (fault.Detail.Code == "Error_Timeout")
                {
                    errorMessage = GameClient.Resources.Strings.ErrorLeaderboardTimeout;
                }
                else if (!string.IsNullOrEmpty(fault.Detail.Message))
                {
                    errorMessage = fault.Detail.Message;
                }

                MessageBox.Show(errorMessage,
                                GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorServerUnreachable,
                                GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorLeaderboardTimeout,
                                GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorNetworkInterruption,
                                GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
                MessageBox.Show(GameClient.Resources.Strings.ErrorLeaderboardGeneral,
                                GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CloseClient(client);
            }
        }

        private static void CloseClient(LeaderboardServiceClient client)
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
            catch
            {
                client.Abort();
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