using GameClient.LobbyServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace GameClient.Helpers
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class LobbyServiceManager : ILobbyServiceCallback, IDisposable
    {
        private static LobbyServiceManager _instance;
        private static readonly object _lock = new object();

        private LobbyServiceClient _client;
        private string _currentUsername;
        private bool _disposed;

        public event Action<string> PlayerKicked;
        public event Action<PlayerLobbyDto> PlayerJoined;
        public event Action<string> PlayerLeft;
        public event Action GameStarted;
        public event Action LobbyDisbanded;
        public event Action<string, string> MessageReceived;

        private LobbyServiceManager() { }

        public static LobbyServiceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LobbyServiceManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Initialize(string username)
        {
            _currentUsername = username;
            CloseClient();

            try
            {
                InstanceContext context = new InstanceContext(this);
                _client = new LobbyServiceClient(context);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al conectar con el servidor:\n" + ex.Message,
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public LobbyServiceClient GetClient()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LobbyServiceManager));

            if (_client == null ||
                _client.State == CommunicationState.Closed ||
                _client.State == CommunicationState.Faulted)
            {
                Initialize(_currentUsername);
            }

            return _client;
        }


        public void OnPlayerKicked(string reason)
        {
            PlayerKicked?.Invoke(reason);
        }

        public void OnPlayerJoined(PlayerLobbyDto player)
        {
            PlayerJoined?.Invoke(player);
        }

        public void OnPlayerLeft(string username)
        {
            PlayerLeft?.Invoke(username);
        }

        public void OnGameStarted()
        {
            GameStarted?.Invoke();
        }

        public void OnLobbyDisbanded()
        {
            LobbyDisbanded?.Invoke();
        }

        public void OnLobbyMessageReceived(string username, string message)
        {
            MessageReceived?.Invoke(username, message);
        }


        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            try
            {
                return await GetClient().CreateLobbyAsync(request);
            }
            catch (EndpointNotFoundException ex)
            {
                throw new Exception("No se puede conectar al servidor.", ex);
            }
            catch (TimeoutException ex)
            {
                throw new Exception("El servidor tardó demasiado en responder.", ex);
            }
            catch (CommunicationException ex)
            {
                throw new Exception("Error de comunicación con el servidor.", ex);
            }
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            try
            {
                return await GetClient().JoinLobbyAsync(request);
            }
            catch (EndpointNotFoundException ex)
            {
                throw new Exception("No se puede conectar al servidor.", ex);
            }
            catch (TimeoutException ex)
            {
                throw new Exception("El servidor tardó demasiado en responder.", ex);
            }
            catch (CommunicationException ex)
            {
                throw new Exception("Error de comunicación con el servidor.", ex);
            }
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            try
            {
                return await GetClient().GetLobbyStateAsync(lobbyCode);
            }
            catch (CommunicationException)
            {
                throw;
            }
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                return await GetClient().StartGameAsync(lobbyCode);
            }
            catch (CommunicationException)
            {
                throw;
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            try
            {
                return await GetClient().LeaveLobbyAsync(username);
            }
            catch (CommunicationException)
            {
                throw;
            }
        }

        public async Task DisbandLobbyAsync(string username)
        {
            try
            {
                await GetClient().DisbandLobbyAsync(username);
            }
            catch (CommunicationException)
            {
                throw;
            }
        }

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            try
            {
                return await GetClient().GetPublicMatchesAsync();
            }
            catch (CommunicationException)
            {
                throw;
            }
        }

        public async Task KickPlayerAsync(KickPlayerRequest request)
        {
            try
            {
                await GetClient().KickPlayerAsync(request);
            }
            catch (CommunicationException)
            {
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            CloseClient();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void CloseClient()
        {
            if (_client == null) return;

            try
            {
                if (_client.State == CommunicationState.Opened)
                    _client.Close();
                else
                    _client.Abort();
            }
            catch
            {
                _client.Abort();
            }
        }
    }
}
