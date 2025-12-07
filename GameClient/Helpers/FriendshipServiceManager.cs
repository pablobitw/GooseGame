using System;
using System.ServiceModel;
using System.Threading.Tasks;
using GameClient.FriendshipServiceReference; 

namespace GameClient.Helpers
{
    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class FriendshipServiceManager : IFriendshipServiceCallback
    {
        public static FriendshipServiceManager Instance { get; private set; }

        public static void Initialize(string username)
        {
            if (Instance == null)
            {
                Instance = new FriendshipServiceManager(username);
            }
        }

        private FriendshipServiceClient _proxy;
        private readonly string _username;

        public event Action FriendListUpdated;
        public event Action RequestReceived;
        public event Action<string, string> GameInvitationReceived;

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
            catch (CommunicationException)
            {
                // Manejo de error de conexión inicial
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    _proxy.Disconnect(_username);
                    _proxy.Close();
                }
            }
            catch (Exception)
            {
                _proxy?.Abort();
            }
        }

        public void OnFriendRequestReceived() => RequestReceived?.Invoke();
        public void OnFriendListUpdated() => FriendListUpdated?.Invoke();
        public void OnGameInvitationReceived(string hostUsername, string lobbyCode) => GameInvitationReceived?.Invoke(hostUsername, lobbyCode);

        public async Task<FriendDto[]> GetFriendListAsync()
        {
            try { return await _proxy.GetFriendListAsync(_username); }
            catch (CommunicationException) { return new FriendDto[0]; }
        }

        public async Task<FriendDto[]> GetPendingRequestsAsync()
        {
            try { return await _proxy.GetPendingRequestsAsync(_username); }
            catch (CommunicationException) { return new FriendDto[0]; }
        }

        public async Task<bool> SendFriendRequestAsync(string targetUser)
        {
            try { return await _proxy.SendFriendRequestAsync(_username, targetUser); }
            catch (CommunicationException) { return false; }
        }

        public async Task RespondToFriendRequestAsync(string requester, bool accept)
        {
            try
            {
                var request = new RespondRequestDto
                {
                    RespondingUsername = _username,
                    RequesterUsername = requester,
                    IsAccepted = accept
                };

                await _proxy.RespondToFriendRequestAsync(request);
            }
            catch (CommunicationException) { }
        }

        public async Task<bool> RemoveFriendAsync(string friendUsername)
        {
            try { return await _proxy.RemoveFriendAsync(_username, friendUsername); }
            catch (CommunicationException) { return false; }
        }

        public void SendGameInvitation(string targetUser, string lobbyCode)
        {
            try
            {
                var invitation = new GameInvitationDto
                {
                    SenderUsername = _username,
                    TargetUsername = targetUser,
                    LobbyCode = lobbyCode
                };

                _proxy.SendGameInvitation(invitation);
            }
            catch { }
        }
    }
}