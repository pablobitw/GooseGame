using System;
using System.Collections.ObjectModel;
using System.ComponentModel; 
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.FriendshipServiceReference;
using GameClient.Models;

namespace GameClient.Views
{
    public partial class FriendshipPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<FriendDisplayModel> FriendsList { get; set; }
        public ObservableCollection<FriendRequestDisplayModel> FriendRequestsList { get; set; }

        private readonly string _currentUsername;

        private int _requestCount;
        public int RequestCount
        {
            get { return _requestCount; }
            set
            {
                _requestCount = value;
                OnPropertyChanged(nameof(RequestCount));
                OnPropertyChanged(nameof(RequestBadgeVisibility));
            }
        }

        public Visibility RequestBadgeVisibility => RequestCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public FriendshipPage(string username)
        {
            InitializeComponent();
            _currentUsername = username;

            this.DataContext = this;

            FriendsList = new ObservableCollection<FriendDisplayModel>();
            FriendRequestsList = new ObservableCollection<FriendRequestDisplayModel>();

            FriendsListBox.ItemsSource = FriendsList;
            FriendRequestsListBox.ItemsSource = FriendRequestsList;

            Loaded += FriendshipPage_Loaded;
        }

        private async void FriendshipPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadFriendsAsync();
            await LoadRequestsAsync();
        }

        private async Task LoadFriendsAsync()
        {
            try
            {
                using (var client = new FriendshipServiceClient())
                {
                    var friends = await client.GetFriendListAsync(_currentUsername);

                    FriendsList.Clear();
                    foreach (var friend in friends)
                    {
                        FriendsList.Add(new FriendDisplayModel
                        {
                            Username = friend.Username,
                            IsOnline = friend.IsOnline,
                        });
                    }
                }
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Tiempo de espera agotado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading friends: " + ex.Message);
            }
            UpdateEmptyStateMessages();
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                using (var client = new FriendshipServiceClient())
                {
                    var requests = await client.GetPendingRequestsAsync(_currentUsername);

                    FriendRequestsList.Clear();
                    foreach (var req in requests)
                    {
                        FriendRequestsList.Add(new FriendRequestDisplayModel
                        {
                            Username = req.Username
                        });
                    }

                    RequestCount = FriendRequestsList.Count;
                }
            }
            catch (CommunicationException)
            {
                Console.WriteLine("Error fetching requests.");
            }
            UpdateEmptyStateMessages();
        }

        private void UpdateEmptyStateMessages()
        {
            NoFriendsMessage.Visibility = FriendsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoRequestsMessage.Visibility = FriendRequestsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            string targetUser = SearchUserBox.Text.Trim();

            if (string.IsNullOrEmpty(targetUser))
            {
                MessageBox.Show("Por favor escribe un nombre de usuario.", "Campo Vacío", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (targetUser == _currentUsername)
            {
                MessageBox.Show("No puedes enviarte solicitud a ti mismo.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool sent = false;
                using (var client = new FriendshipServiceClient())
                {
                    sent = await client.SendFriendRequestAsync(_currentUsername, targetUser);
                }

                if (sent)
                {
                    MessageBox.Show($"¡Solicitud enviada a {targetUser}!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    SearchUserBox.Text = string.Empty;
                }
                else
                {
                    MessageBox.Show($"No se pudo enviar la solicitud a '{targetUser}'.\nVerifica que el usuario exista o no sean amigos ya.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de conexión con el servidor.", "Error de Red", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            string requesterUsername = button.Tag.ToString();

            try
            {
                bool success;
                using (var client = new FriendshipServiceClient())
                {
                    success = await client.RespondToFriendRequestAsync(_currentUsername, requesterUsername, true);
                }

                if (success)
                {
                    MessageBox.Show($"¡Ahora eres amigo de {requesterUsername}!", "Aceptado", MessageBoxButton.OK, MessageBoxImage.Information);

                    RemoveRequestFromList(requesterUsername);
                    FriendsList.Add(new FriendDisplayModel { Username = requesterUsername, IsOnline = true }); 
                    UpdateEmptyStateMessages();
                }
                else
                {
                    MessageBox.Show("Error al aceptar la solicitud.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    await LoadRequestsAsync(); 
                }
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de red.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            string requesterUsername = button.Tag.ToString();

            try
            {
                bool success;
                using (var client = new FriendshipServiceClient())
                {
                    success = await client.RespondToFriendRequestAsync(_currentUsername, requesterUsername, false);
                }

                if (success)
                {
                    MessageBox.Show($"Solicitud rechazada.", "Rechazado", MessageBoxButton.OK, MessageBoxImage.Information);
                    RemoveRequestFromList(requesterUsername);
                }
                else
                {
                    RemoveRequestFromList(requesterUsername);
                }
            }
            catch (CommunicationException)
            {
                MessageBox.Show("Error de red.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string friendUsername = button.Tag.ToString();

            var result = MessageBox.Show(
                $"¿Estás seguro de eliminar a {friendUsername}?",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = false;
                    using (var client = new FriendshipServiceClient())
                    {
                        deleted = await client.RemoveFriendAsync(_currentUsername, friendUsername);
                    }

                    if (deleted)
                    {
                        FriendDisplayModel friendToRemove = null;
                        foreach (var friend in FriendsList)
                        {
                            if (friend.Username == friendUsername)
                            {
                                friendToRemove = friend;
                                break;
                            }
                        }

                        if (friendToRemove != null)
                        {
                            FriendsList.Remove(friendToRemove);
                            UpdateEmptyStateMessages();
                        }
                        MessageBox.Show($"Amigo eliminado.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Error al eliminar. Intenta nuevamente.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (CommunicationException)
                {
                    MessageBox.Show("Error de conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveRequestFromList(string username)
        {
            FriendRequestDisplayModel itemToRemove = null;
            foreach (var req in FriendRequestsList)
            {
                if (req.Username == username)
                {
                    itemToRemove = req;
                    break;
                }
            }

            if (itemToRemove != null)
            {
                FriendRequestsList.Remove(itemToRemove);
                RequestCount = FriendRequestsList.Count;
                UpdateEmptyStateMessages();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;

            if (mainWindow != null)
            {
                mainWindow.ShowMainMenu();
            }
        }
    }
}