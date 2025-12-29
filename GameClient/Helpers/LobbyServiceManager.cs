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
        private bool _disposed = false;

        public event Action<string> PlayerKicked;

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
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Cliente inicializado para {username}");
            }
            catch (InvalidOperationException ex)
            {
                HandleInitializationError(ex);
            }
        }

        private static void HandleInitializationError(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error al inicializar: {ex.Message}");
            MessageBox.Show(
                $"Error al conectar con el servidor:\n{ex.Message}\n\nAsegúrate de que el servidor esté corriendo.",
                "Error de Conexión",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        public LobbyServiceClient GetClient()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LobbyServiceManager));
            }

            if (_client == null ||
                _client.State == CommunicationState.Faulted ||
                _client.State == CommunicationState.Closed)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Reinicializando cliente para {_currentUsername}");
                Initialize(_currentUsername);
            }

            if (_client == null)
            {
                throw new InvalidOperationException("No se pudo conectar con el Servicio de Lobby. El cliente es nulo.");
            }

            return _client;
        }

        public void OnPlayerKicked(string reason)
        {
            System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] OnPlayerKicked llamado: {reason}");
            PlayerKicked?.Invoke(reason);
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] CreateLobbyAsync llamado");
                return await GetClient().CreateLobbyAsync(request);
            }
            catch (EndpointNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] EndpointNotFoundException: {ex.Message}");
                throw new EndpointNotFoundException("No se puede conectar al servidor. Verifica que esté corriendo.", ex);
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] TimeoutException: {ex.Message}");
                throw new TimeoutException("El servidor tardó demasiado en responder.", ex);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] CommunicationException: {ex.Message}");
                throw new CommunicationException($"Error de comunicación al crear lobby: {ex.Message}", ex);
            }
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] JoinLobbyAsync llamado para {request.Username}");
                var result = await GetClient().JoinLobbyAsync(request);
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] JoinLobbyAsync resultado: Success={result.Success}");
                return result;
            }
            catch (EndpointNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] EndpointNotFoundException: {ex.Message}");
                throw new EndpointNotFoundException("No se puede conectar al servidor. Verifica que esté corriendo.", ex);
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] TimeoutException: {ex.Message}");
                throw new TimeoutException("El servidor tardó demasiado en responder.", ex);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] CommunicationException: {ex.Message}");
                throw new CommunicationException($"Error de comunicación al unirse: {ex.Message}", ex);
            }
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            try
            {
                return await GetClient().GetLobbyStateAsync(lobbyCode);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error WCF en GetLobbyStateAsync: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Timeout en GetLobbyStateAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                return await GetClient().StartGameAsync(lobbyCode);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error WCF en StartGameAsync: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Timeout en StartGameAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            try
            {
                return await GetClient().LeaveLobbyAsync(username);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error WCF en LeaveLobbyAsync: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Timeout en LeaveLobbyAsync: {ex.Message}");
                throw;
            }
        }

        public async Task DisbandLobbyAsync(string username)
        {
            try
            {
                await GetClient().DisbandLobbyAsync(username);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error WCF en DisbandLobbyAsync: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Timeout en DisbandLobbyAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            try
            {
                return await GetClient().GetPublicMatchesAsync();
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error WCF en GetPublicMatchesAsync: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Timeout en GetPublicMatchesAsync: {ex.Message}");
                throw;
            }
        }

        public async Task KickPlayerAsync(KickPlayerRequest request)
        {
            try
            {
                await GetClient().KickPlayerAsync(request);
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error WCF en KickPlayerAsync: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Timeout en KickPlayerAsync: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            DisposeInternal(true);
            GC.SuppressFinalize(this);
        }

        private void DisposeInternal(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Dispose llamado");
                CloseClient();
            }

            _disposed = true;
        }

        private void CloseClient()
        {
            if (_client == null) return;

            try
            {
                if (_client.State == CommunicationState.Opened)
                {
                    _client.Close();
                }
                else
                {
                    _client.Abort();
                }
            }
            catch (CommunicationException)
            {
                _client.Abort();
            }
            catch (TimeoutException)
            {
                _client.Abort();
            }
            catch (Exception)
            {
                _client.Abort();
            }
        }
    }
}