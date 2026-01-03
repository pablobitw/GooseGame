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
            if (Instance != null && Instance._username == username)
                return;

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
            catch
            {
                HandleFatalConnectionError("No fue posible conectar con el servicio de amigos.");
            }
        }

        private void HandleFatalConnectionError(string message)
        {
            AbortProxy();
            Reset();
            SessionManager.ForceLogout(message);
        }

        private void AbortProxy()
        {
            try
            {
                _proxy?.Abort();
            }
            catch
            {
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
        public void OnGameInvitationReceived(string hostUsername, string lobbyCode)
            => GameInvitationReceived?.Invoke(hostUsername, lobbyCode);

        public async Task<FriendDto[]> GetFriendListAsync()
        {
            try
            {
                return await _proxy.GetFriendListAsync(_username);
            }
            catch
            {
                HandleFatalConnectionError("Se perdió la conexión al obtener la lista de amigos.");
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<FriendDto[]> GetPendingRequestsAsync()
        {
            try
            {
                return await _proxy.GetPendingRequestsAsync(_username);
            }
            catch
            {
                HandleFatalConnectionError("Se perdió la conexión al obtener solicitudes pendientes.");
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<bool> SendFriendRequestAsync(string targetUser)
        {
            try
            {
                return await _proxy.SendFriendRequestAsync(_username, targetUser);
            }
            catch (FaultException)
            {
                return false;
            }
            catch
            {
                HandleFatalConnectionError($"Se perdió la conexión al enviar solicitud a {targetUser}.");
                return false;
            }
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
            catch
            {
                HandleFatalConnectionError("Se perdió la conexión al responder una solicitud de amistad.");
            }
        }

        public async Task<bool> RemoveFriendAsync(string friendUsername)
        {
            try
            {
                return await _proxy.RemoveFriendAsync(_username, friendUsername);
            }
            catch
            {
                HandleFatalConnectionError("Se perdió la conexión al eliminar un amigo.");
                return false;
            }
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
            catch
            {
                HandleFatalConnectionError("Se perdió la conexión al enviar una invitación.");
            }
        }
    }
}
