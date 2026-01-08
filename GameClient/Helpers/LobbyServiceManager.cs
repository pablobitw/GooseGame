using GameClient.LobbyServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameClient.Helpers
{
    [CallbackBehavior(
        UseSynchronizationContext = false,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
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
            InitializeProxy();
        }

        private void InitializeProxy()
        {
            if (_client != null)
            {
                if (_client.State == CommunicationState.Opened)
                    return;

                if (_client.State == CommunicationState.Faulted)
                {
                    try { _client.Abort(); } catch { }
                    _client = null;
                }
                else if (_client.State != CommunicationState.Opening)
                {
                    try { _client.Close(); } catch { }
                    _client = null;
                }
                else
                {
                    return;
                }
            }

            try
            {
                var context = new InstanceContext(this);
                _client = new LobbyServiceClient(context);
            }
            catch (Exception)
            {
                _client = null;
            }
        }

        private LobbyServiceClient GetClient()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LobbyServiceManager));

            InitializeProxy();

            if (_client == null)
                throw new CommunicationException("No se pudo inicializar el cliente WCF.");

            return _client;
        }

        public void OnPlayerKicked(string reason) => PlayerKicked?.Invoke(reason);
        public void OnPlayerJoined(PlayerLobbyDto player) => PlayerJoined?.Invoke(player);
        public void OnPlayerLeft(string username) => PlayerLeft?.Invoke(username);
        public void OnGameStarted() => GameStarted?.Invoke();
        public void OnLobbyDisbanded() => LobbyDisbanded?.Invoke();
        public void OnLobbyMessageReceived(string username, string message) => MessageReceived?.Invoke(username, message);

        public Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            return ExecuteAsync(c => c.CreateLobbyAsync(request));
        }

        public Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            return ExecuteAsync(c => c.JoinLobbyAsync(request));
        }

        public Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            return ExecuteAsync(c => c.GetLobbyStateAsync(lobbyCode));
        }

        public Task<bool> StartGameAsync(string lobbyCode)
        {
            return ExecuteAsync(c => c.StartGameAsync(lobbyCode));
        }

        public Task<bool> LeaveLobbyAsync(string username)
        {
            return ExecuteAsync(c => c.LeaveLobbyAsync(username));
        }

        public Task DisbandLobbyAsync(string username)
        {
            return ExecuteAsync(c => c.DisbandLobbyAsync(username));
        }

        public Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            return ExecuteAsync(c => c.GetPublicMatchesAsync());
        }

        public Task KickPlayerAsync(KickPlayerRequest request)
        {
            return ExecuteAsync(c => c.KickPlayerAsync(request));
        }

        private async Task<T> ExecuteAsync<T>(Func<LobbyServiceClient, Task<T>> action)
        {
            try
            {
                var client = GetClient();
                return await action(client);
            }
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                throw;
            }
            catch (TimeoutException)
            {
                InvalidateClient();
                throw;
            }
            catch (CommunicationException)
            {
                InvalidateClient();
                throw;
            }
            catch (Exception)
            {
                InvalidateClient();
                throw;
            }
        }

        private async Task ExecuteAsync(Func<LobbyServiceClient, Task> action)
        {
            try
            {
                var client = GetClient();
                await action(client);
            }
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                throw;
            }
            catch (TimeoutException)
            {
                InvalidateClient();
                throw;
            }
            catch (CommunicationException)
            {
                InvalidateClient();
                throw;
            }
            catch (Exception)
            {
                InvalidateClient();
                throw;
            }
        }

        private void InvalidateClient()
        {
            if (_client != null)
            {
                try { _client.Abort(); } catch { }
                _client = null;
            }
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
            finally
            {
                _client = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            CloseClient();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}