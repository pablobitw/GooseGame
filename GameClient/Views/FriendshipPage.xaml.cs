using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameClient.FriendshipServiceReference;
using GameClient.Helpers;
using GameClient.Models;

namespace GameClient.Views
{
    public partial class FriendshipPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<FriendDisplayModel> FriendsList { get; set; }
        public ObservableCollection<FriendRequestDisplayModel> FriendRequestsList { get; set; }
        public ObservableCollection<FriendRequestDisplayModel> SentRequestsList { get; set; }

        private readonly string _currentUsername;
        private FriendshipServiceManager _friendshipManager;
        private Action _onConfirmAction;

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
            SentRequestsList = new ObservableCollection<FriendRequestDisplayModel>();

            FriendsListBox.ItemsSource = FriendsList;
            FriendRequestsListBox.ItemsSource = FriendRequestsList;
            SentRequestsListBox.ItemsSource = SentRequestsList;

            if (FriendshipServiceManager.Instance == null)
            {
                FriendshipServiceManager.Initialize(_currentUsername);
            }

            _friendshipManager = FriendshipServiceManager.Instance;

            if (_friendshipManager != null)
            {
                _friendshipManager.FriendListUpdated += HandleDataUpdate;
                _friendshipManager.RequestReceived += HandleDataUpdate;
            }

            Loaded += FriendshipPage_Loaded;
            Unloaded += FriendshipPage_Unloaded;
        }

        private void ShowCustomDialog(string title, string message, FontAwesome.WPF.FontAwesomeIcon icon, bool isConfirmation = false, Action onConfirm = null)
        {
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Icon = icon;

            CancelBtn.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
            CancelColumn.Width = isConfirmation ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            ConfirmBtn.Content = isConfirmation
                ? GameClient.Resources.Strings.DialogConfirmBtn
                : GameClient.Resources.Strings.DialogOkBtn;

            CancelBtn.Content = GameClient.Resources.Strings.DialogCancelBtn;

            _onConfirmAction = onConfirm;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            if (sender == ConfirmBtn)
            {
                _onConfirmAction?.Invoke();
            }
            _onConfirmAction = null;
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
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadDataAsync();
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                }
            });
        }

        private async Task LoadDataAsync()
        {
            try
            {
                await LoadFriendsAsync();
                await LoadRequestsAsync();
                await LoadSentRequestsAsync();
            }
            catch (System.ServiceModel.CommunicationException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
            }
            catch (TimeoutException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
            }
        }

        private async Task LoadFriendsAsync()
        {
            if (_friendshipManager == null)
            {
                return;
            }

            try
            {
                var friends = await _friendshipManager.GetFriendListAsync();

                if (Dispatcher.CheckAccess())
                {
                    UpdateFriendsList(friends);
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => UpdateFriendsList(friends));
                }
            }
            catch (System.ServiceModel.CommunicationException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
        }

        private void UpdateFriendsList(FriendDto[] friends)
        {
            FriendsList.Clear();

            if (friends != null)
            {
                foreach (var friend in friends)
                {
                    FriendsList.Add(new FriendDisplayModel
                    {
                        Username = friend.Username,
                        IsOnline = friend.IsOnline,
                    });
                }
            }

            UpdateEmptyStateMessages();
        }

        private async Task LoadRequestsAsync()
        {
            if (_friendshipManager == null)
            {
                return;
            }

            try
            {
                var requests = await _friendshipManager.GetPendingRequestsAsync();

                if (Dispatcher.CheckAccess())
                {
                    UpdateRequestsList(requests);
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => UpdateRequestsList(requests));
                }
            }
            catch (System.ServiceModel.CommunicationException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
        }

        private void UpdateRequestsList(FriendDto[] requests)
        {
            FriendRequestsList.Clear();

            if (requests != null)
            {
                foreach (var req in requests)
                {
                    FriendRequestsList.Add(new FriendRequestDisplayModel { Username = req.Username });
                }
            }

            RequestCount = FriendRequestsList.Count;
            UpdateEmptyStateMessages();
        }

        private async Task LoadSentRequestsAsync()
        {
            if (_friendshipManager == null)
            {
                return;
            }

            try
            {
                var sent = await _friendshipManager.GetSentRequestsAsync();

                if (Dispatcher.CheckAccess())
                {
                    UpdateSentRequestsList(sent);
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => UpdateSentRequestsList(sent));
                }
            }
            catch (System.ServiceModel.CommunicationException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
        }

        private void UpdateSentRequestsList(FriendDto[] sent)
        {
            SentRequestsList.Clear();

            if (sent != null)
            {
                foreach (var req in sent)
                {
                    SentRequestsList.Add(new FriendRequestDisplayModel { Username = req.Username });
                }
            }

            UpdateEmptyStateMessages();
        }

        private void UpdateEmptyStateMessages()
        {
            NoFriendsMessage.Visibility = FriendsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoRequestsMessage.Visibility = FriendRequestsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoSentRequestsMessage.Visibility = SentRequestsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            string target = SearchUserBox.Text.Trim();

            if (string.IsNullOrEmpty(target))
            {
                ShowWarningMessage(GameClient.Resources.Strings.EmptyUsernameError);
                return;
            }

            if (target.Equals(_currentUsername, StringComparison.OrdinalIgnoreCase))
            {
                ShowCustomDialog(
                    GameClient.Resources.Strings.DialogErrorTitle,
                    GameClient.Resources.Strings.FriendSelfRequestError,
                    FontAwesome.WPF.FontAwesomeIcon.UserTimes);
                return;
            }

            if (_friendshipManager == null)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                return;
            }

            try
            {
                var result = await _friendshipManager.SendFriendRequestAsync(target);

                if (result == FriendRequestResult.Success)
                {
                    ShowSuccessMessage(string.Format(GameClient.Resources.Strings.FriendRequestSentSuccess, target));
                    SearchUserBox.Text = string.Empty;
                    await LoadSentRequestsAsync();
                }
                else if (result == FriendRequestResult.AlreadyFriends)
                {
                    ShowCustomDialog(
                        GameClient.Resources.Strings.DialogInfoTitle,
                        GameClient.Resources.Strings.FriendAlreadyFriends,
                        FontAwesome.WPF.FontAwesomeIcon.Users);
                }
                else if (result == FriendRequestResult.Pending)
                {
                    ShowWarningMessage(GameClient.Resources.Strings.FriendAlreadyFriends);
                }
                else if (result == FriendRequestResult.TargetNotFound)
                {
                    ShowCustomDialog(
                        GameClient.Resources.Strings.DialogWarningTitle,
                        GameClient.Resources.Strings.FriendNotFound,
                        FontAwesome.WPF.FontAwesomeIcon.SearchMinus);
                }
                else
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                }
            }
            catch (System.ServiceModel.CommunicationException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
            }
            catch (TimeoutException)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
            }
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string requester = btn.Tag.ToString();
                btn.IsEnabled = false;

                if (_friendshipManager == null)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                    btn.IsEnabled = true;
                    return;
                }

                try
                {
                    bool success = await _friendshipManager.RespondToFriendRequestAsync(requester, true);

                    if (success)
                    {
                        await LoadDataAsync();
                    }
                    else
                    {
                        ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                        btn.IsEnabled = true;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                    btn.IsEnabled = true;
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                    btn.IsEnabled = true;
                }
            }
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string requester = btn.Tag.ToString();
                btn.IsEnabled = false;

                if (_friendshipManager == null)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                    btn.IsEnabled = true;
                    return;
                }

                try
                {
                    bool success = await _friendshipManager.RespondToFriendRequestAsync(requester, false);

                    if (success)
                    {
                        await LoadDataAsync();
                    }
                    else
                    {
                        ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                        btn.IsEnabled = true;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                    btn.IsEnabled = true;
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                    btn.IsEnabled = true;
                }
            }
        }

        private void CancelSentRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string target = btn.Tag.ToString();

                ShowCustomDialog(
                    GameClient.Resources.Strings.DialogConfirmTitle,
                    GameClient.Resources.Strings.FriendCancelRequestConfirm,
                    FontAwesome.WPF.FontAwesomeIcon.Undo,
                    true,
                    async () =>
                    {
                        if (_friendshipManager == null)
                        {
                            ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                            return;
                        }

                        try
                        {
                            bool success = await _friendshipManager.RemoveFriendAsync(target);

                            if (success)
                            {
                                await LoadSentRequestsAsync();
                            }
                            else
                            {
                                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                            }
                        }
                        catch (System.ServiceModel.CommunicationException)
                        {
                            ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                        }
                        catch (TimeoutException)
                        {
                            ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                        }
                    });
            }
        }

        private void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string friend = btn.Tag.ToString();

                string confirmMessage = string.Format(GameClient.Resources.Strings.FriendDeleteConfirm, friend);

                ShowCustomDialog(
                    GameClient.Resources.Strings.DialogConfirmTitle,
                    confirmMessage,
                    FontAwesome.WPF.FontAwesomeIcon.UserTimes,
                    true,
                    async () =>
                    {
                        if (_friendshipManager == null)
                        {
                            ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                            return;
                        }

                        try
                        {
                            bool success = await _friendshipManager.RemoveFriendAsync(friend);

                            if (!success)
                            {
                                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                            }
                            else
                            {
                                await LoadFriendsAsync();
                            }
                        }
                        catch (System.ServiceModel.CommunicationException)
                        {
                            ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                        }
                        catch (TimeoutException)
                        {
                            ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                        }
                    });
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as GameMainWindow;
            if (mainWindow != null)
            {
                await mainWindow.ShowMainMenu();
            }
        }

        private void ShowErrorMessage(string message)
        {
            string title = GameClient.Resources.Strings.DialogErrorTitle;
            if (Dispatcher.CheckAccess())
            {
                ShowCustomDialog(title, message, FontAwesome.WPF.FontAwesomeIcon.TimesCircle);
            }
            else
            {
                Dispatcher.Invoke(() => ShowCustomDialog(title, message, FontAwesome.WPF.FontAwesomeIcon.TimesCircle));
            }
        }

        private void ShowWarningMessage(string message)
        {
            string title = GameClient.Resources.Strings.DialogWarningTitle;
            if (Dispatcher.CheckAccess())
            {
                ShowCustomDialog(title, message, FontAwesome.WPF.FontAwesomeIcon.ExclamationTriangle);
            }
            else
            {
                Dispatcher.Invoke(() => ShowCustomDialog(title, message, FontAwesome.WPF.FontAwesomeIcon.ExclamationTriangle));
            }
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            ShowCustomDialog(
                GameClient.Resources.Strings.FriendActionBlockedTitle,
                GameClient.Resources.Strings.FriendPasteBlocked,
                FontAwesome.WPF.FontAwesomeIcon.Lock);
        }

        private void ShowSuccessMessage(string message)
        {
            string title = GameClient.Resources.Strings.DialogSuccessTitle;
            if (Dispatcher.CheckAccess())
            {
                ShowCustomDialog(title, message, FontAwesome.WPF.FontAwesomeIcon.CheckCircle);
            }
            else
            {
                Dispatcher.Invoke(() => ShowCustomDialog(title, message, FontAwesome.WPF.FontAwesomeIcon.CheckCircle));
            }
        }
    }
}