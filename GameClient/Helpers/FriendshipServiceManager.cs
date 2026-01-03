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
            catch (CommunicationException ex)
            {
                SessionManager.ForceLogout($"Error de conexión con el servicio de amigos: {ex.Message}");
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("Tiempo de espera agotado al conectar con el servicio de amigos.");
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
            catch (CommunicationException)
            {
                _proxy.Abort();
            }
            catch (TimeoutException)
            {
                _proxy.Abort();
            }
            catch (Exception)
            {
                _proxy.Abort();
            }
        }

        public void OnFriendRequestReceived() => RequestReceived?.Invoke();
        public void OnFriendListUpdated() => FriendListUpdated?.Invoke();
        public void OnGameInvitationReceived(string hostUsername, string lobbyCode) => GameInvitationReceived?.Invoke(hostUsername, lobbyCode);

        public async Task<FriendDto[]> GetFriendListAsync()
        {
            try
            {
                return await _proxy.GetFriendListAsync(_username);
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Se perdió la conexión al obtener la lista de amigos.");
                return Array.Empty<FriendDto>();
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió al solicitar la lista de amigos.");
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<FriendDto[]> GetPendingRequestsAsync()
        {
            try
            {
                return await _proxy.GetPendingRequestsAsync(_username);
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout("Se perdió la conexión al obtener solicitudes pendientes.");
                return Array.Empty<FriendDto>();
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió al solicitar solicitudes pendientes.");
                return Array.Empty<FriendDto>();
            }
        }

        public async Task<bool> SendFriendRequestAsync(string targetUser)
        {
            try
            {
                return await _proxy.SendFriendRequestAsync(_username, targetUser);
            }
            catch (FaultException ex)
            {
                Console.WriteLine($"[FriendshipManager] Error lógico al enviar solicitud: {ex.Message}");
                return false;
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout($"Se perdió la conexión al enviar solicitud a {targetUser}.");
                return false;
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor tardó demasiado en procesar la solicitud de amistad.");
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
            catch (CommunicationException)
            {
                SessionManager.ForceLogout($"Se perdió la conexión al responder la solicitud de {requester}.");
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió al intentar aceptar/rechazar la solicitud.");
            }
        }

        public async Task<bool> RemoveFriendAsync(string friendUsername)
        {
            try
            {
                return await _proxy.RemoveFriendAsync(_username, friendUsername);
            }
            catch (CommunicationException)
            {
                SessionManager.ForceLogout($"Se perdió la conexión al intentar eliminar a {friendUsername}.");
                return false;
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor tardó demasiado en eliminar al amigo.");
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
            catch (CommunicationException)
            {
                SessionManager.ForceLogout($"Se perdió la conexión al invitar a {targetUser}.");
            }
            catch (TimeoutException)
            {
                SessionManager.ForceLogout("El servidor no respondió al enviar la invitación.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipManager] Error inesperado al invitar: {ex.Message}");
            }
        }
    }
}