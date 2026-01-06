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

        private FriendshipServiceClient _proxy;
        private readonly string _username;

        public event Action FriendListUpdated;
        public event Action RequestReceived;
        public event Action<string, string> GameInvitationReceived;
        public event Action<string> FriendRequestPopUpReceived;

        private FriendshipServiceManager(string username)
        {
            _username = username;
        }

        public static void Initialize(string username)
        {
            if (Instance != null && Instance._username == username)
            {
                Instance.CheckConnection();
                return;
            }

            Instance?.Disconnect();
            Instance = new FriendshipServiceManager(username);
            Instance.CheckConnection();
        }

        public static void Reset()
        {
            Instance?.Disconnect();
            Instance = null;
        }

        private void CheckConnection()
        {
            if (IsProxyValid()) return;

            try
            {
                if (_proxy != null)
                {
                    try { _proxy.Abort(); } catch { }
                    _proxy = null;
                }

                var context = new InstanceContext(this);
                _proxy = new FriendshipServiceClient(context);
                _proxy.Connect(_username);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipManager] Error al intentar reconectar: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_proxy == null) return;

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
            catch
            {
                _proxy.Abort();
            }
            finally
            {
                _proxy = null;
            }
        }

        public void OnFriendRequestReceived() => RequestReceived?.Invoke();
        public void OnFriendListUpdated() => FriendListUpdated?.Invoke();
        public void OnGameInvitationReceived(string hostUsername, string lobbyCode) => GameInvitationReceived?.Invoke(hostUsername, lobbyCode);
        public void OnFriendRequestPopUp(string senderUsername) => FriendRequestPopUpReceived?.Invoke(senderUsername);

        public async Task<FriendDto[]> GetFriendListAsync()
        {
            return await ExecuteProxyCall(async () => await _proxy.GetFriendListAsync(_username));
        }

        public async Task<FriendDto[]> GetPendingRequestsAsync()
        {
            return await ExecuteProxyCall(async () => await _proxy.GetPendingRequestsAsync(_username));
        }

        public async Task<FriendDto[]> GetSentRequestsAsync()
        {
            return await ExecuteProxyCall(async () => await _proxy.GetSentRequestsAsync(_username));
        }

        public async Task<FriendRequestResult> SendFriendRequestAsync(string targetUser)
        {
            return await ExecuteProxyCall(async () => await _proxy.SendFriendRequestAsync(_username, targetUser));
        }

        public async Task<FriendRequestResult> RespondToFriendRequestAsync(string requester, bool accept)
        {
            var request = new RespondRequestDto
            {
                RespondingUsername = _username,
                RequesterUsername = requester,
                IsAccepted = accept
            };
            return await ExecuteProxyCall(async () => await _proxy.RespondToFriendRequestAsync(request));
        }

        public async Task<FriendRequestResult> RemoveFriendAsync(string friendUsername)
        {
            return await ExecuteProxyCall(async () => await _proxy.RemoveFriendAsync(_username, friendUsername));
        }

        public void SendGameInvitation(string targetUser, string lobbyCode)
        {
            try
            {
                CheckConnection();

                if (IsProxyValid())
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
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipManager] Error enviando invitación: {ex.Message}");
                ForceInvalidateProxy();
            }
        }

        private async Task<T> ExecuteProxyCall<T>(Func<Task<T>> action)
        {
            CheckConnection();

            if (!IsProxyValid())
            {
                throw new EndpointNotFoundException("Server unavailable");
            }

            try
            {
                return await action();
            }
            catch (CommunicationException)
            {
                ForceInvalidateProxy();
                throw;
            }
            catch (TimeoutException)
            {
                ForceInvalidateProxy();
                throw;
            }
            catch (ObjectDisposedException)
            {
                ForceInvalidateProxy();
                throw new CommunicationException("Proxy disposed.");
            }
        }

        private bool IsProxyValid()
        {
            return _proxy != null && _proxy.State == CommunicationState.Opened;
        }

        private void ForceInvalidateProxy()
        {
            if (_proxy != null)
            {
                try { _proxy.Abort(); } catch { }
                _proxy = null;
            }
        }
    }
}