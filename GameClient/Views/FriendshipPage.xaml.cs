using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.ServiceModel;
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

        private string GetResourceString(string key)
        {
            var res = GameClient.Resources.Strings.ResourceManager.GetString(key);
            return string.IsNullOrEmpty(res) ? key : res;
        }

        private void ShowTranslatedMessageBox(string messageKey, string titleKey, MessageBoxImage icon)
        {
            string message = GetResourceString(messageKey);
            string title = GetResourceString(titleKey);
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
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
                await LoadDataAsync();
            });
        }

        private async Task LoadDataAsync()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Friends_Error_NoInternet", "Friends_Title_Error", MessageBoxImage.Error);
                return;
            }

            try
            {
                await LoadFriendsAsync();
                await LoadRequestsAsync();
                await LoadSentRequestsAsync();
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine("Timeout loading friends data.");
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }
        }

        private async Task LoadFriendsAsync()
        {
            if (_friendshipManager == null) return;

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
            if (_friendshipManager == null) return;

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
            if (_friendshipManager == null) return;

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
                ShowTranslatedMessageBox("Friends_Req_Self", "Friends_Title_Error", MessageBoxImage.Warning);
                return;
            }

            if (_friendshipManager == null)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Friends_Error_NoInternet", "Friends_Title_Error", MessageBoxImage.Error);
                return;
            }

            try
            {
                var result = await _friendshipManager.SendFriendRequestAsync(target);

                if (result == FriendRequestResult.Success)
                {
                    string msgFormat = GetResourceString("Friends_Req_Sent");
                    string title = GameClient.Resources.Strings.DialogSuccessTitle;
                    ShowCustomDialog(title, string.Format(msgFormat, target), FontAwesome.WPF.FontAwesomeIcon.CheckCircle);

                    SearchUserBox.Text = string.Empty;
                    await LoadSentRequestsAsync();
                }
                else
                {
                    HandleRequestError(result);
                }
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Friends_Error_Timeout", "Friends_Title_Error", MessageBoxImage.Warning);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
            }
            catch (Exception)
            {
                ShowTranslatedMessageBox("Friends_Error_General", "Friends_Title_Error", MessageBoxImage.Error);
            }
        }

        private void HandleRequestError(FriendRequestResult result)
        {
            string msgKey = "Friends_Error_General";
            string titleKey = "Friends_Title_Error";
            MessageBoxImage icon = MessageBoxImage.Error;

            switch (result)
            {
                case FriendRequestResult.AlreadyFriends:
                case FriendRequestResult.Pending:
                    msgKey = "Friends_Req_Already";
                    titleKey = "DialogInfoTitle";
                    icon = MessageBoxImage.Information;
                    break;
                case FriendRequestResult.TargetNotFound:
                    msgKey = "Friends_Req_NotFound";
                    titleKey = "DialogWarningTitle";
                    icon = MessageBoxImage.Warning;
                    break;
                case FriendRequestResult.GuestRestriction:
                    msgKey = "Friends_Req_Guest";
                    break;
                case FriendRequestResult.DatabaseError:
                    msgKey = "Friends_Error_Database";
                    break;
                case FriendRequestResult.Error:
                    msgKey = "Friends_Error_General";
                    break;
            }
            ShowTranslatedMessageBox(msgKey, titleKey, icon);
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            await RespondToRequest(sender, true);
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            await RespondToRequest(sender, false);
        }

        private async Task RespondToRequest(object sender, bool accept)
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

                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    ShowTranslatedMessageBox("Friends_Error_NoInternet", "Friends_Title_Error", MessageBoxImage.Error);
                    btn.IsEnabled = true;
                    return;
                }

                try
                {
                    // CAMBIO: Ahora recibe Enum en lugar de bool
                    var result = await _friendshipManager.RespondToFriendRequestAsync(requester, accept);

                    if (result == FriendRequestResult.Success)
                    {
                        await LoadDataAsync();
                    }
                    else
                    {
                        HandleRequestError(result);
                    }
                }
                catch (EndpointNotFoundException)
                {
                    ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
                }
                catch (TimeoutException)
                {
                    ShowTranslatedMessageBox("Friends_Error_Timeout", "Friends_Title_Error", MessageBoxImage.Warning);
                }
                catch (CommunicationException)
                {
                    ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
                }
                catch (Exception)
                {
                    ShowTranslatedMessageBox("Friends_Error_General", "Friends_Title_Error", MessageBoxImage.Error);
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }

        private void CancelSentRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string target = btn.Tag.ToString();
                string msgFormat = GameClient.Resources.Strings.FriendCancelRequestConfirm;
                string msg = string.Format(msgFormat, target);

                ShowCustomDialog(GameClient.Resources.Strings.DialogConfirmTitle, msg, FontAwesome.WPF.FontAwesomeIcon.Undo, true, async () =>
                {
                    await RemoveFriendOrRequest(target);
                });
            }
        }

        private void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string friend = btn.Tag.ToString();
                string msgFormat = GameClient.Resources.Strings.FriendDeleteConfirm;
                string msg = string.Format(msgFormat, friend);

                ShowCustomDialog(GameClient.Resources.Strings.DialogConfirmTitle, msg, FontAwesome.WPF.FontAwesomeIcon.UserTimes, true, async () =>
                {
                    await RemoveFriendOrRequest(friend);
                });
            }
        }

        private async Task RemoveFriendOrRequest(string targetUsername)
        {
            if (_friendshipManager == null)
            {
                ShowErrorMessage(GameClient.Resources.Strings.ErrorTitle);
                return;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTranslatedMessageBox("Friends_Error_NoInternet", "Friends_Title_Error", MessageBoxImage.Error);
                return;
            }

            try
            {
                // CAMBIO: Ahora recibe Enum en lugar de bool
                var result = await _friendshipManager.RemoveFriendAsync(targetUsername);

                if (result == FriendRequestResult.Success)
                {
                    await LoadDataAsync();
                }
                else
                {
                    HandleRequestError(result);
                }
            }
            catch (EndpointNotFoundException)
            {
                ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
            }
            catch (CommunicationException)
            {
                ShowTranslatedMessageBox("Friends_Error_ServerDown", "Friends_Title_Error", MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                ShowTranslatedMessageBox("Friends_Error_Timeout", "Friends_Title_Error", MessageBoxImage.Warning);
            }
            catch (Exception)
            {
                ShowTranslatedMessageBox("Friends_Error_General", "Friends_Title_Error", MessageBoxImage.Error);
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
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
            ShowCustomDialog(GameClient.Resources.Strings.FriendActionBlockedTitle,
                             GameClient.Resources.Strings.FriendPasteBlocked,
                             FontAwesome.WPF.FontAwesomeIcon.Lock);
        }
    }
}