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
                    ShowErrorMessage("Error de comunicación con el servidor al actualizar.");
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage("Tiempo de espera agotado al actualizar.");
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
                ShowErrorMessage("Error de comunicación con el servidor. Por favor, verifica tu conexión.");
            }
            catch (TimeoutException)
            {
                ShowErrorMessage("El servidor tardó demasiado en responder. Inténtalo de nuevo.");
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
                ShowWarningMessage("No puedes enviarte una solicitud a ti mismo.");
                return;
            }

            if (_friendshipManager == null)
            {
                ShowErrorMessage("No hay conexión con el servidor.");
                return;
            }

            try
            {
                var result = await _friendshipManager.SendFriendRequestAsync(target);

                if (result == FriendRequestResult.Success)
                {
                    ShowSuccessMessage($"Solicitud enviada a {target}.");
                    SearchUserBox.Text = string.Empty;
                    await LoadSentRequestsAsync();
                }
                else if (result == FriendRequestResult.AlreadyFriends)
                {
                    ShowInfoMessage("Ya eres amigo de este jugador.");
                }
                else if (result == FriendRequestResult.Pending)
                {
                    ShowWarningMessage("Ya hay una solicitud pendiente con este jugador.");
                }
                else if (result == FriendRequestResult.TargetNotFound)
                {
                    ShowWarningMessage("El usuario no existe.");
                }
                else
                {
                    ShowErrorMessage("No se pudo enviar la solicitud. Verifica tu conexión.");
                }
            }
            catch (System.ServiceModel.CommunicationException)
            {
                ShowErrorMessage("Error de comunicación con el servidor.");
            }
            catch (TimeoutException)
            {
                ShowErrorMessage("El servidor tardó demasiado en responder.");
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
                    ShowErrorMessage("No hay conexión con el servidor.");
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
                        ShowErrorMessage("No se pudo aceptar la solicitud en este momento.");
                        btn.IsEnabled = true;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage("Error de comunicación con el servidor.");
                    btn.IsEnabled = true;
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage("El servidor tardó demasiado en responder.");
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
                    ShowErrorMessage("No hay conexión con el servidor.");
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
                        ShowErrorMessage("No se pudo rechazar la solicitud.");
                        btn.IsEnabled = true;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage("Error de comunicación con el servidor.");
                    btn.IsEnabled = true;
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage("El servidor tardó demasiado en responder.");
                    btn.IsEnabled = true;
                }
            }
        }

        private async void CancelSentRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string target = btn.Tag.ToString();
                btn.IsEnabled = false;

                if (_friendshipManager == null)
                {
                    ShowErrorMessage("No hay conexión con el servidor.");
                    btn.IsEnabled = true;
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
                        ShowErrorMessage("No se pudo cancelar la solicitud enviada.");
                        btn.IsEnabled = true;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage("Error de comunicación con el servidor.");
                    btn.IsEnabled = true;
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage("El servidor tardó demasiado en responder.");
                    btn.IsEnabled = true;
                }
            }
        }

        private async void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string friend = btn.Tag.ToString();
                var result = MessageBox.Show(
                    $"¿Eliminar a {friend} de tus amigos?",
                    "Confirmación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                btn.IsEnabled = false;

                if (_friendshipManager == null)
                {
                    ShowErrorMessage("No hay conexión con el servidor.");
                    btn.IsEnabled = true;
                    return;
                }

                try
                {
                    bool success = await _friendshipManager.RemoveFriendAsync(friend);

                    if (!success)
                    {
                        ShowErrorMessage("No se pudo eliminar al amigo. Inténtalo de nuevo.");
                        btn.IsEnabled = true;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    ShowErrorMessage("Error de comunicación con el servidor.");
                    btn.IsEnabled = true;
                }
                catch (TimeoutException)
                {
                    ShowErrorMessage("El servidor tardó demasiado en responder.");
                    btn.IsEnabled = true;
                }
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
            if (Dispatcher.CheckAccess())
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Dispatcher.Invoke(() => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void ShowWarningMessage(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                MessageBox.Show(message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                Dispatcher.Invoke(() => MessageBox.Show(message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
        }

        private void ShowInfoMessage(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                MessageBox.Show(message, "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Dispatcher.Invoke(() => MessageBox.Show(message, "Información", MessageBoxButton.OK, MessageBoxImage.Information));
            }
        }
        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("Por seguridad, el pegado está deshabilitado en el buscador.",
                                "Acción bloqueada",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }


        private void ShowSuccessMessage(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                MessageBox.Show(message, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Dispatcher.Invoke(() => MessageBox.Show(message, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information));
            }
        }
    }
}