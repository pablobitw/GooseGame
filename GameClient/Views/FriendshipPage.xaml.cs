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
    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public partial class FriendshipPage : Page, INotifyPropertyChanged, IFriendshipServiceCallback
    {
        public ObservableCollection<FriendDisplayModel> FriendsList { get; set; }
        public ObservableCollection<FriendRequestDisplayModel> FriendRequestsList { get; set; }

        private readonly string _currentUsername;
        private FriendshipServiceClient _proxy; 

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
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
            Unloaded += FriendshipPage_Unloaded;
        }

        private async void FriendshipPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var instanceContext = new InstanceContext(this);

                _proxy = new FriendshipServiceClient(instanceContext);

                _proxy.Connect(_currentUsername);

                await LoadFriendsAsync();
                await LoadRequestsAsync();
            }
            catch (CommunicationException)
            {
                MessageBox.Show("No se pudo conectar al servicio de chat/amigos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FriendshipPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    _proxy.Disconnect(_currentUsername);
                    _proxy.Close();
                }
            }
            catch (Exception) { _proxy?.Abort(); }
        }

        public void OnFriendRequestReceived()
        {
            this.Dispatcher.Invoke(async () =>
            {
                await LoadRequestsAsync();
            });
        }

        public void OnFriendListUpdated()
        {
            this.Dispatcher.Invoke(async () =>
            {
                await LoadFriendsAsync();
                await LoadRequestsAsync(); 
            });
        }

        private async Task LoadFriendsAsync()
        {
            try
            {
                var friends = await _proxy.GetFriendListAsync(_currentUsername);
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
            catch (Exception) { }
            UpdateEmptyStateMessages();
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                var requests = await _proxy.GetPendingRequestsAsync(_currentUsername);
                FriendRequestsList.Clear();
                foreach (var req in requests)
                {
                    FriendRequestsList.Add(new FriendRequestDisplayModel { Username = req.Username });
                }
                RequestCount = FriendRequestsList.Count;
            }
            catch (Exception) { }
            UpdateEmptyStateMessages();
        }

        private void UpdateEmptyStateMessages()
        {
            NoFriendsMessage.Visibility = FriendsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoRequestsMessage.Visibility = FriendRequestsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Acciones ---

        private async void SendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            string target = SearchUserBox.Text.Trim();
            if (string.IsNullOrEmpty(target) || target == _currentUsername) return;

            try
            {
                bool sent = await _proxy.SendFriendRequestAsync(_currentUsername, target);
                if (sent)
                {
                    MessageBox.Show($"Solicitud enviada a {target}.");
                    SearchUserBox.Text = string.Empty;
                }
                else MessageBox.Show("No se pudo enviar (Usuario no existe o ya son amigos).");
            }
            catch (CommunicationException) { MessageBox.Show("Error de red."); }
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string requester = btn.Tag.ToString();
            try
            {
                await _proxy.RespondToFriendRequestAsync(_currentUsername, requester, true);
            }
            catch (CommunicationException) { }
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string requester = btn.Tag.ToString();
            try
            {
                await _proxy.RespondToFriendRequestAsync(_currentUsername, requester, false);
                await LoadRequestsAsync();
            }
            catch (CommunicationException) { }
        }

        private async void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string friend = btn.Tag.ToString();

            if (MessageBox.Show($"¿Eliminar a {friend}?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = await _proxy.RemoveFriendAsync(_currentUsername, friend);
                }
                catch (CommunicationException) { }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null) mainWindow.ShowMainMenu();
        }
    }
}