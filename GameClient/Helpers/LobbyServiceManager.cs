using GameClient.LobbyServiceReference;
using System;
using System.ServiceModel;
using System.Threading;
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
        private bool _disposed;

        private CancellationTokenSource _connectionMonitorCts;
        private string _currentLobbyCode; 

        public event Action ConnectionLost;

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
            InitializeProxy();
        }

        private void InitializeProxy()
        {
            if (_client != null && _client.State == CommunicationState.Opened)
                return;

            try
            {
                CloseClient();
                var context = new InstanceContext(this);
                _client = new LobbyServiceClient(context);
                _client.InnerChannel.Faulted += OnChannelFaulted;
            }
            catch (Exception)
            {
                _client = null;
            }
        }

        private void OnChannelFaulted(object sender, EventArgs e)
        {
            HandleConnectionFailure();
        }

        private void HandleConnectionFailure()
        {
            StopConnectionMonitoring();
            InvalidateClient();
            ConnectionLost?.Invoke();
        }

        private LobbyServiceClient GetClient()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LobbyServiceManager));
            InitializeProxy();
            if (_client == null)
                throw new CommunicationException("WCF Client is null.");
            return _client;
        }


        private void StartConnectionMonitoring(string lobbyCode)
        {
            StopConnectionMonitoring(); 
            _currentLobbyCode = lobbyCode;
            _connectionMonitorCts = new CancellationTokenSource();

            Task.Run(async () => await ConnectionLoop(_connectionMonitorCts.Token));
        }

        private void StopConnectionMonitoring()
        {
            if (_connectionMonitorCts != null)
            {
                _connectionMonitorCts.Cancel();
                _connectionMonitorCts.Dispose();
                _connectionMonitorCts = null;
            }
            _currentLobbyCode = null;
        }

        private async Task ConnectionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, token);

                    if (_client == null || _client.State != CommunicationState.Opened)
                    {
                        throw new CommunicationException("Client closed locally.");
                    }

               
                    await _client.GetLobbyStateAsync(_currentLobbyCode);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    if (!token.IsCancellationRequested)
                    {
                        HandleConnectionFailure();
                    }
                    break;
                }
            }
        }

        public void OnPlayerKicked(string reason) => PlayerKicked?.Invoke(reason);
        public void OnPlayerJoined(PlayerLobbyDto player) => PlayerJoined?.Invoke(player);
        public void OnPlayerLeft(string username) => PlayerLeft?.Invoke(username);
        public void OnGameStarted() => GameStarted?.Invoke();
        public void OnLobbyDisbanded() => LobbyDisbanded?.Invoke();
        public void OnLobbyMessageReceived(string username, string message) => MessageReceived?.Invoke(username, message);


        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            var client = GetClient();
            var result = await client.CreateLobbyAsync(request);

            if (result.Success)
            {
                StartConnectionMonitoring(result.LobbyCode);
            }
            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            var client = GetClient();
            var result = await client.JoinLobbyAsync(request);

            if (result.Success)
            {
                StartConnectionMonitoring(request.LobbyCode);
            }
            return result;
        }

        public Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            return ExecuteAsync(c => c.GetLobbyStateAsync(lobbyCode), null);
        }

        public Task<bool> StartGameAsync(string lobbyCode)
        {
            return ExecuteAsync(c => c.StartGameAsync(lobbyCode), false);
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            StopConnectionMonitoring(); 
            return await ExecuteAsync(c => c.LeaveLobbyAsync(username), false);
        }

        public async Task DisbandLobbyAsync(string username)
        {
            StopConnectionMonitoring(); 
            await ExecuteAsync(c => c.DisbandLobbyAsync(username));
        }

        public Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            return ExecuteAsync(c => c.GetPublicMatchesAsync(), new ActiveMatchDto[0], triggerGlobalEvent: false);
        }

        public Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            return ExecuteAsync(c => c.KickPlayerAsync(request), false);
        }


        private async Task<T> ExecuteAsync<T>(Func<LobbyServiceClient, Task<T>> action, T defaultValue, bool triggerGlobalEvent = true)
        {
            try
            {
                var client = GetClient();
                return await action(client);
            }
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is FaultException)
            {
                if (triggerGlobalEvent)
                {
                    HandleConnectionFailure();
                }
                return defaultValue;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        private async Task ExecuteAsync(Func<LobbyServiceClient, Task> action, bool triggerGlobalEvent = true)
        {
            try
            {
                var client = GetClient();
                await action(client);
            }
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is FaultException)
            {
                if (triggerGlobalEvent)
                {
                    HandleConnectionFailure();
                }
            }
            catch (Exception) { }
        }

        private void InvalidateClient()
        {
            if (_client != null)
            {
                try { _client.InnerChannel.Faulted -= OnChannelFaulted; _client.Abort(); } catch { }
                _client = null;
            }
        }

        private void CloseClient()
        {
            if (_client == null) return;
            try
            {
                _client.InnerChannel.Faulted -= OnChannelFaulted;
                if (_client.State == CommunicationState.Opened) _client.Close();
                else _client.Abort();
            }
            catch { _client.Abort(); }
            finally { _client = null; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopConnectionMonitoring();
            CloseClient();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}