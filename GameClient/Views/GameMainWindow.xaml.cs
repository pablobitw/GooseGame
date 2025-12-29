using GameClient.GameServiceReference;
using GameClient.Helpers;
using GameClient.LobbyServiceReference;
using GameClient.UserProfileServiceReference;
using GameClient.Views;
using System;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient
{
    public partial class GameMainWindow : Window
    {
        private const string ErrorTitle = "Error"; 
        private readonly string _username;

        public GameMainWindow(string loggedInUsername)
        {
            InitializeComponent();
            _username = loggedInUsername;

            FriendshipServiceManager.Initialize(_username);
            LobbyServiceManager.Instance.Initialize(_username);

            FriendshipServiceManager.Instance.GameInvitationReceived += HandleInvitation;

            this.Closed += GameMainWindow_Closed;

            _ = LoadUserCurrency();
        }

        private async Task LoadUserCurrency()
        {
            try
            {
                CoinCountText.Text = "...";

                using (var client = new UserProfileServiceClient())
                {
                    var userProfile = await client.GetUserProfileAsync(_username);

                    if (userProfile != null)
                    {
                        CoinCountText.Text = userProfile.Coins.ToString();
                    }
                }
            }
            catch (TimeoutException)
            {
                CoinCountText.Text = "---";
                Console.WriteLine("Timeout loading currency.");
            }
            catch (EndpointNotFoundException)
            {
                CoinCountText.Text = "---";
                Console.WriteLine("Endpoint not found loading currency.");
            }
            catch (CommunicationException)
            {
                CoinCountText.Text = "---";
                Console.WriteLine("Communication error loading currency.");
            }
            catch (Exception ex)
            {
                CoinCountText.Text = "Err";
                Console.WriteLine($"Unexpected error loading currency: {ex.Message}");
            }
        }

        private bool IsGuestActionRestricted(string featureName)
        {
            if (UserSession.GetInstance().IsGuest)
            {
                string message = $"La función '{featureName}' solo está disponible para usuarios registrados.\n\n" +
                                 "¿Te gustaría crear una cuenta ahora para disfrutar de amigos, estadísticas y más?\n" +
                                 "(Se cerrará tu sesión actual)";

                var result = MessageBox.Show(message, "Modo Invitado", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    ReturnToRegister();
                }

                return true;
            }
            return false;
        }

        private void ReturnToRegister()
        {
            AuthWindow authWindow = new AuthWindow();
            authWindow.Show();
            authWindow.NavigateToRegister();
            this.Close();
        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted("Perfil de Usuario")) return;

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new UserProfilePage(_username));
        }

        private void FriendsButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted("Lista de Amigos")) return;

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new FriendshipPage(_username));
        }

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string videoPath = Path.Combine(baseDir, "Assets", "BACKGROUND_1.mp4");

                if (File.Exists(videoPath))
                {
                    var media = (MediaElement)sender;
                    media.Source = new Uri(videoPath, UriKind.Absolute);
                    media.LoadedBehavior = MediaState.Manual;
                    media.Play();
                }
                else
                {
                    MessageBox.Show($"No se encuentra el video en: {videoPath}\n\nRevisa las propiedades del archivo en Visual Studio.", "Error de Archivo");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando video: " + ex.Message);
            }
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            var media = (MediaElement)sender;
            media.Position = TimeSpan.Zero;
            media.Play();
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
                this.Close();
            }
        }

        private void LeaderboardButtonClick(object sender, RoutedEventArgs e)
        {
            if (IsGuestActionRestricted("Tabla de Clasificación")) return;

            MainMenuGrid.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new ScoreboardPage(_username));
        }

        private async void GameMainWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                if (FriendshipServiceManager.Instance != null)
                {
                    FriendshipServiceManager.Instance.Disconnect();
                }

                LobbyServiceManager.Instance.Dispose();

                using (var client = new GameServiceClient())
                {
                    await client.LogoutAsync(_username);
                }

                UserSession.GetInstance().Logout();
            }
            catch (CommunicationException ex)
            {
                Console.WriteLine($"Error closing connection: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Timeout closing connection: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error during shutdown: {ex.Message}");
            }
            finally
            {
                if (Application.Current.Windows.Count == 0)
                {
                    Application.Current.Shutdown();
                }
            }
        }

        public async Task ShowMainMenu()
        {
            MainFrame.Content = null;
            MainMenuGrid.Visibility = Visibility.Visible;
            await LoadUserCurrency();
        }

        private async void HandleInvitation(string host, string code)
        {
            if (UserSession.GetInstance().IsGuest) return;

            await this.Dispatcher.InvokeAsync(async () =>
            {
                var result = MessageBox.Show(
                    $"{host} te ha invitado a una partida. ¿Quieres unirte?",
                    "Invitación de Juego",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await AttemptJoinLobbyAsync(code);
                }
            });
        }

        private async Task AttemptJoinLobbyAsync(string code)
        {
            try
            {
                var request = new JoinLobbyRequest
                {
                    LobbyCode = code,
                    Username = _username
                };

                var joinResult = await LobbyServiceManager.Instance.JoinLobbyAsync(request);

                if (joinResult.Success)
                {
                    await Task.Delay(200);
                    MainMenuGrid.Visibility = Visibility.Collapsed;
                    MainFrame.Navigate(new LobbyPage(_username, code, joinResult));
                }
                else
                {
                    MessageBox.Show($"No se pudo unir: {joinResult.ErrorMessage}", ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de comunicación al unirse.", ErrorTitle);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Tiempo de espera agotado.", ErrorTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al intentar unirse: {ex.Message}", ErrorTitle);
            }
        }
    }
}