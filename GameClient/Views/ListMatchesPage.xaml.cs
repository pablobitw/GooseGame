using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class ListMatchesPage : Page
    {
        private readonly string _username;
        public ObservableCollection<MatchItem> Matches { get; set; }

        public ListMatchesPage(string username)
        {
            InitializeComponent();
            _username = username;
            Matches = new ObservableCollection<MatchItem>();
            MatchesListBox.ItemsSource = Matches;

            this.Loaded += ListMatchesPage_Loaded;
        }

        private async void ListMatchesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadMatchesAsync();
        }

        private async Task LoadMatchesAsync()
        {
            RefreshButton.IsEnabled = false;
            Matches.Clear();
            NoMatchesText.Visibility = Visibility.Collapsed;

            try
            {
                var activeLobbies = await LobbyServiceManager.Instance.GetPublicMatchesAsync();

                if (activeLobbies != null && activeLobbies.Any())
                {
                    foreach (var lobby in activeLobbies)
                    {
                        string mapName = lobby.BoardId == 1
                            ? GameClient.Resources.Strings.BoardTypeNormal
                            : GameClient.Resources.Strings.BoardTypeSpecial;

                        Matches.Add(new MatchItem
                        {
                            LobbyCode = lobby.LobbyCode,
                            HostName = lobby.HostUsername,
                            MapName = mapName,
                            PlayerCount = $"{lobby.CurrentPlayers}/{lobby.MaxPlayers}"
                        });
                    }
                }
                else
                {
                    NoMatchesText.Visibility = Visibility.Visible;
                }
            }
            catch (TimeoutException)
            {
                MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException)
            {
                MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
                NoMatchesText.Visibility = Visibility.Visible;
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadMatchesAsync();
        }

        private async void JoinButton_Click(object sender, RoutedEventArgs e)
        {
            if (MatchesListBox.SelectedItem is MatchItem selectedMatch)
            {
                JoinButton.IsEnabled = false;

                try
                {
                    var request = new JoinLobbyRequest
                    {
                        LobbyCode = selectedMatch.LobbyCode,
                        Username = _username
                    };

                    var result = await LobbyServiceManager.Instance.JoinLobbyAsync(request);

                    if (result.Success)
                    {
                        await Task.Delay(200);
                        NavigationService.Navigate(new LobbyPage(_username, selectedMatch.LobbyCode, result));
                    }
                    else
                    {
                        MessageBox.Show(result.ErrorMessage, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        await LoadMatchesAsync();
                    }
                }
                catch (TimeoutException)
                {
                    MessageBox.Show(GameClient.Resources.Strings.TimeoutLabel, GameClient.Resources.Strings.ErrorTitle);
                }
                catch (EndpointNotFoundException)
                {
                    MessageBox.Show(GameClient.Resources.Strings.EndpointNotFoundLabel, GameClient.Resources.Strings.ErrorTitle);
                }
                catch (CommunicationException)
                {
                    MessageBox.Show(GameClient.Resources.Strings.ComunicationLabel, GameClient.Resources.Strings.ErrorTitle);
                }
                finally
                {
                    JoinButton.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show(GameClient.Resources.Strings.SelectMatchWarning, GameClient.Resources.Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }

    public class MatchItem
    {
        public string LobbyCode { get; set; }
        public string HostName { get; set; }
        public string MapName { get; set; }
        public string PlayerCount { get; set; }
    }
}