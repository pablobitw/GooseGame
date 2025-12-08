using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using GameClient.FriendshipServiceReference;
using GameClient.Helpers;
using GameClient.Models;

namespace GameClient.Views
{
    public partial class FriendshipPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<FriendDisplayModel> FriendsList { get; set; }
        public ObservableCollection<FriendRequestDisplayModel> FriendRequestsList { get; set; }

        private readonly string _currentUsername;
        private FriendshipServiceManager _friendshipManager;

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

            if (FriendshipServiceManager.Instance == null)
            {
                FriendshipServiceManager.Initialize(_currentUsername);
            }
            _friendshipManager = FriendshipServiceManager.Instance;

            _friendshipManager.FriendListUpdated += HandleDataUpdate;
            _friendshipManager.RequestReceived += HandleDataUpdate;

            Loaded += FriendshipPage_Loaded;
            Unloaded += FriendshipPage_Unloaded;
        }

        private async void FriendshipPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void FriendshipPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_friendshipManager != null)
            {
                _friendshipManager.FriendListUpdated -= HandleDataUpdate;
                _friendshipManager.RequestReceived -= HandleDataUpdate;
            }
        }

        private void HandleDataUpdate()
        {
            this.Dispatcher.Invoke(async () => await LoadDataAsync());
        }

        private async Task LoadDataAsync()
        {
            await LoadFriendsAsync();
            await LoadRequestsAsync();
        }

        private async Task LoadFriendsAsync()
        {
            var friends = await _friendshipManager.GetFriendListAsync();
            FriendsList.Clear();
            foreach (var friend in friends)
            {
                FriendsList.Add(new FriendDisplayModel
                {
                    Username = friend.Username,
                    IsOnline = friend.IsOnline,
                });
            }
            UpdateEmptyStateMessages();
        }

        private async Task LoadRequestsAsync()
        {
            var requests = await _friendshipManager.GetPendingRequestsAsync();
            FriendRequestsList.Clear();
            foreach (var req in requests)
            {
                FriendRequestsList.Add(new FriendRequestDisplayModel { Username = req.Username });
            }
            RequestCount = FriendRequestsList.Count;
            UpdateEmptyStateMessages();
        }

        private void UpdateEmptyStateMessages()
        {
            NoFriendsMessage.Visibility = FriendsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoRequestsMessage.Visibility = FriendRequestsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            string target = SearchUserBox.Text.Trim();
            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show(GameClient.Resources.Strings.EmptyUsernameError, GameClient.Resources.Strings.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (target == _currentUsername)
            {
                MessageBox.Show(GameClient.Resources.Strings.SelfRequestError, GameClient.Resources.Strings.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool sent = await _friendshipManager.SendFriendRequestAsync(target);
            if (sent)
            {
                MessageBox.Show(string.Format(GameClient.Resources.Strings.RequestSentSuccess, target), GameClient.Resources.Strings.InfoTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                SearchUserBox.Text = string.Empty;
            }
            else
            {
                MessageBox.Show(GameClient.Resources.Strings.RequestSentError, GameClient.Resources.Strings.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            string requester = btn.Tag.ToString();

            await _friendshipManager.RespondToFriendRequestAsync(requester, true);
            await LoadDataAsync();
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            string requester = btn.Tag.ToString();

            await _friendshipManager.RespondToFriendRequestAsync(requester, false);
            await LoadDataAsync();
        }

        private async void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            string friend = btn.Tag.ToString();

            var result = MessageBox.Show(string.Format(GameClient.Resources.Strings.DeleteConfirmation, friend),
                                         GameClient.Resources.Strings.ConfirmationTitle,
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                bool deleted = await _friendshipManager.RemoveFriendAsync(friend);
                if (deleted)
                {
                    MessageBox.Show(GameClient.Resources.Strings.FriendDeletedSuccess, GameClient.Resources.Strings.InfoTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadDataAsync();
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null) mainWindow.ShowMainMenu();
        }
    }
}