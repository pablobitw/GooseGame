using GameClient.LobbyServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace GameClient.Helpers
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyServiceManager : ILobbyServiceCallback, IDisposable
    {
        private static LobbyServiceManager _instance;
        private static readonly object _lock = new object();

        private LobbyServiceClient _client;
        private string _currentUsername;

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

            if (_client != null)
            {
                try
                {
                    if (_client.State == CommunicationState.Opened)
                        _client.Close();
                }
                catch
                {
                    _client.Abort();
                }
            }

            try
            {
                InstanceContext context = new InstanceContext(this);
                _client = new LobbyServiceClient(context);

                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Cliente inicializado para {username}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error al inicializar: {ex.Message}");
                MessageBox.Show(
                    $"Error al conectar con el servidor:\n{ex.Message}\n\nAsegúrate de que el servidor esté corriendo.",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public LobbyServiceClient GetClient()
        {
            if (_client == null ||
                _client.State == CommunicationState.Faulted ||
                _client.State == CommunicationState.Closed)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Reinicializando cliente para {_currentUsername}");
                Initialize(_currentUsername);
            }

            if (_client == null)
            {
                throw new Exception("No se pudo conectar con el Servicio de Lobby. Verifica tu conexión.");
            }

            return _client;
        }

        public void OnPlayerKicked(string reason)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] OnPlayerKicked llamado: {reason}");
                PlayerKicked?.Invoke(reason);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en OnPlayerKicked: {ex.Message}");
            }
        }

        public async Task<LobbyCreationResultDTO> CreateLobbyAsync(CreateLobbyRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] CreateLobbyAsync llamado");
                return await GetClient().CreateLobbyAsync(request);
            }
            catch (EndpointNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] EndpointNotFoundException: {ex.Message}");
                throw new Exception("No se puede conectar al servidor. Verifica que esté corriendo.");
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] CommunicationException: {ex.Message}");
                throw new Exception($"Error de comunicación: {ex.Message}");
            }
        }

        public async Task<JoinLobbyResultDTO> JoinLobbyAsync(JoinLobbyRequest request)
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
                throw new Exception("No se puede conectar al servidor. Verifica que esté corriendo.");
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] CommunicationException: {ex.Message}");
                throw new Exception($"Error de comunicación: {ex.Message}");
            }
        }

        public async Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode)
        {
            try
            {
                return await GetClient().GetLobbyStateAsync(lobbyCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en GetLobbyStateAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                return await GetClient().StartGameAsync(lobbyCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en StartGameAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            try
            {
                return await GetClient().LeaveLobbyAsync(username);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en LeaveLobbyAsync: {ex.Message}");
                throw;
            }
        }

        public async Task DisbandLobbyAsync(string username)
        {
            try
            {
                await GetClient().DisbandLobbyAsync(username);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en DisbandLobbyAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<ActiveMatchDTO[]> GetPublicMatchesAsync()
        {
            try
            {
                return await GetClient().GetPublicMatchesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en GetPublicMatchesAsync: {ex.Message}");
                throw;
            }
        }

        public async Task KickPlayerAsync(KickPlayerRequest request)
        {
            try
            {
                await GetClient().KickPlayerAsync(request);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en KickPlayerAsync: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Dispose llamado");
                if (_client != null)
                {
                    if (_client.State == CommunicationState.Opened)
                        _client.Close();
                    else
                        _client.Abort();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyServiceManager] Error en Dispose: {ex.Message}");
            }
        }
    }
}