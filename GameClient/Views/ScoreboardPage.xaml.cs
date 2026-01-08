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
            LeaderboardList.ItemsSource = null;

            try
            {
                using (var client = new LeaderboardServiceClient())
                {
                    var leaderboardData = await client.GetGlobalLeaderboardAsync(_username);

                    if (leaderboardData == null || leaderboardData.Length == 0)
                    {
                        MessageBox.Show(
                            GameClient.Resources.Strings.LabelEmptyLeaderboard,
                            GameClient.Resources.Strings.InformationTitle,
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        LeaderboardList.ItemsSource = leaderboardData;
                    }
                }
            }

            catch (FaultException<GameServiceFault> fault)
            {
                string errorMessage;

                switch (fault.Detail.ErrorType)
                {
                    case GameServiceErrorType.DatabaseError:
                        errorMessage = GameClient.Resources.Strings.ErrorLeaderboardDatabase;
                        break;

                    case GameServiceErrorType.OperationTimeout:
                        errorMessage = GameClient.Resources.Strings.ErrorLeaderboardTimeout;
                        break;

                    case GameServiceErrorType.EmptyData:
                        errorMessage = GameClient.Resources.Strings.LabelEmptyLeaderboard;
                        break;

                    default:
                        errorMessage = !string.IsNullOrEmpty(fault.Detail.Message)
                            ? fault.Detail.Message
                            : GameClient.Resources.Strings.ErrorLeaderboardGeneral;
                        break;
                }

                MessageBox.Show(errorMessage, GameClient.Resources.Strings.LeaderboardLoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            catch (EndpointNotFoundException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.ErrorServerUnreachable,
                    GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            catch (TimeoutException)
            {
     
                MessageBox.Show(
                    GameClient.Resources.Strings.ErrorLeaderboardTimeout,
                    GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            catch (CommunicationException)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.ErrorNetworkInterruption,
                    GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
     
            catch (Exception)
            {
          
                MessageBox.Show(
                    GameClient.Resources.Strings.ErrorLeaderboardGeneral,
                    GameClient.Resources.Strings.LeaderboardLoadErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
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