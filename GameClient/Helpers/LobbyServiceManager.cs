using GameClient.LobbyServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

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
            CloseClient();

            try
            {
                var context = new InstanceContext(this);
                _client = new LobbyServiceClient(context);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    GameClient.Resources.Strings.SafeZone_DatabaseError, 
                    GameClient.Resources.Strings.DialogErrorTitle,
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
                throw new InvalidOperationException(GameClient.Resources.Strings.SafeZone_DatabaseError);
            }
            catch (TimeoutException)
            {
                InvalidateClient();
                throw new TimeoutException(GameClient.Resources.Strings.SafeZone_ServerTimeout);
            }
            catch (CommunicationException)
            {
                InvalidateClient();
                throw new CommunicationException(GameClient.Resources.Strings.SafeZone_ConnectionLost);
            }
            catch (Exception)
            {
                InvalidateClient();
                throw new Exception(GameClient.Resources.Strings.SafeZone_DatabaseError);
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
                throw new InvalidOperationException(GameClient.Resources.Strings.SafeZone_DatabaseError);
            }
            catch (TimeoutException)
            {
                InvalidateClient();
                throw new TimeoutException(GameClient.Resources.Strings.SafeZone_ServerTimeout);
            }
            catch (CommunicationException)
            {
                InvalidateClient();
                throw new CommunicationException(GameClient.Resources.Strings.SafeZone_ConnectionLost);
            }
            catch (Exception)
            {
                InvalidateClient();
                throw new Exception(GameClient.Resources.Strings.SafeZone_DatabaseError);
            }
        }

        private void InvalidateClient()
        {
            CloseClient();
            _client = null;
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

        public void Dispose()
        {
            if (_disposed) return;

            CloseClient();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}