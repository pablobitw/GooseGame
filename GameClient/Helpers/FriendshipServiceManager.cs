using System;
using System.ServiceModel;
using System.Threading.Tasks;
using GameClient.FriendshipServiceReference;

namespace GameClient.Helpers
{
    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    public class FriendshipServiceManager : IFriendshipServiceCallback
    {
        public static FriendshipServiceManager Instance { get; private set; }

        public static void Initialize(string username)
        {
            if (Instance != null && Instance._username == username)
            {
                return;
            }

            Instance?.Disconnect();
            Instance = new FriendshipServiceManager(username);
        }

        public static void Reset()
        {
            Instance?.Disconnect();
            Instance = null;
        }

        private FriendshipServiceClient _proxy;
        private readonly string _username;

        public event Action FriendListUpdated;
        public event Action RequestReceived;
        public event Action<string, string> GameInvitationReceived;
        public event Action<string> FriendRequestPopUpReceived;

        private FriendshipServiceManager(string username)
        {
            _username = username;
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                var context = new InstanceContext(this);
                _proxy = new FriendshipServiceClient(context);
                _proxy.Connect(_username);
            }
            catch (EndpointNotFoundException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendServiceConnect);
            }
            catch (CommunicationException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendServiceComm);
            }
            catch (TimeoutException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendServiceTimeout);
            }
        }

        private void HandleFatalConnectionError(string message)
        {
            if (_proxy != null)
            {
                try
                {
                    _proxy.Abort();
                }
                catch
                {
                }
                _proxy = null;
            }
            Reset();
            SessionManager.ForceLogout(message);
        }

        public void Disconnect()
        {
            if (_proxy == null)
            {
                return;
            }

            try
            {
                if (_proxy.State == CommunicationState.Opened)
                {
                    _proxy.Disconnect(_username);
                    _proxy.Close();
                }
                else
                {
                    _proxy.Abort();
                }
            }
            catch (CommunicationException)
            {
                _proxy.Abort();
            }
            catch (TimeoutException)
            {
                _proxy.Abort();
            }
            finally
            {
                _proxy = null;
            }
        }

        public void OnFriendRequestReceived()
        {
            RequestReceived?.Invoke();
        }

        public void OnFriendListUpdated()
        {
            FriendListUpdated?.Invoke();
        }

        public void OnGameInvitationReceived(string hostUsername, string lobbyCode)
        {
            GameInvitationReceived?.Invoke(hostUsername, lobbyCode);
        }

        public void OnFriendRequestPopUp(string senderUsername)
        {
            FriendRequestPopUpReceived?.Invoke(senderUsername);
        }

        public async Task<FriendDto[]> GetFriendListAsync()
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    return await _proxy.GetFriendListAsync(_username);
                }
                return Array.Empty<FriendDto>();
            }
            catch (CommunicationObjectFaultedException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListChannel);
                return Array.Empty<FriendDto>();
            }
            catch (CommunicationException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListComm);
                return Array.Empty<FriendDto>();
            }
            catch (TimeoutException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListTimeout);
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<FriendDto[]> GetPendingRequestsAsync()
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    return await _proxy.GetPendingRequestsAsync(_username);
                }
                return Array.Empty<FriendDto>();
            }
            catch (CommunicationObjectFaultedException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListChannel);
                return Array.Empty<FriendDto>();
            }
            catch (CommunicationException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorPendingRequestsComm);
                return Array.Empty<FriendDto>();
            }
            catch (TimeoutException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListTimeout);
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<FriendDto[]> GetSentRequestsAsync()
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    return await _proxy.GetSentRequestsAsync(_username);
                }
                return Array.Empty<FriendDto>();
            }
            catch (CommunicationObjectFaultedException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListChannel);
                return Array.Empty<FriendDto>();
            }
            catch (CommunicationException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorSentRequestsComm);
                return Array.Empty<FriendDto>();
            }
            catch (TimeoutException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorFriendListTimeout);
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<FriendRequestResult> SendFriendRequestAsync(string targetUser)
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    return await _proxy.SendFriendRequestAsync(_username, targetUser);
                }
                return FriendRequestResult.Error;
            }
            catch (CommunicationObjectFaultedException)
            {
                return FriendRequestResult.Error;
            }
            catch (CommunicationException)
            {
                return FriendRequestResult.Error;
            }
            catch (TimeoutException)
            {
                return FriendRequestResult.Error;
            }
        }

        public async Task<bool> RespondToFriendRequestAsync(string requester, bool accept)
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    var request = new RespondRequestDto
                    {
                        RespondingUsername = _username,
                        RequesterUsername = requester,
                        IsAccepted = accept
                    };

                    await _proxy.RespondToFriendRequestAsync(request);
                    return true;
                }
                return false;
            }
            catch (CommunicationObjectFaultedException)
            {
                return false;
            }
            catch (CommunicationException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        public async Task<bool> RemoveFriendAsync(string friendUsername)
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    return await _proxy.RemoveFriendAsync(_username, friendUsername);
                }
                return false;
            }
            catch (CommunicationObjectFaultedException)
            {
                return false;
            }
            catch (CommunicationException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        public void SendGameInvitation(string targetUser, string lobbyCode)
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    var invitation = new GameInvitationDto
                    {
                        SenderUsername = _username,
                        TargetUsername = targetUser,
                        LobbyCode = lobbyCode
                    };

                    _proxy.SendGameInvitation(invitation);
                }
            }
            catch (CommunicationObjectFaultedException)
            {
                HandleFatalConnectionError(string.Format(GameClient.Resources.Strings.ErrorInviteFriendChannel, targetUser));
            }
            catch (CommunicationException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorInviteFriendComm);
            }
            catch (TimeoutException)
            {
                HandleFatalConnectionError(GameClient.Resources.Strings.ErrorInviteFriendTimeout);
            }
        }
    }
}