using GameClient.GameplayServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GameClient.Helpers
{
    [CallbackBehavior(
        UseSynchronizationContext = false,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class GameplayServiceManager : IGameplayServiceCallback, IDisposable
    {
        private static GameplayServiceManager _instance;
        private static readonly object _lock = new object();

        private GameplayServiceClient _client;
        private string _currentUsername;
        private bool _disposed;

        private int _connectionLostRaised;

        public string LastConnectionErrorMessage { get; private set; } = string.Empty;

        public event Action<GameStateDto> TurnChanged;
        public event Action<string> GameFinished;
        public event Action<string> PlayerKicked;
        public event Action<string, string> VoteKickStarted;
        public event Action ConnectionLost;

        private GameplayServiceManager() { }

        public static GameplayServiceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GameplayServiceManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Initialize(string username)
        {
            _currentUsername = username;
            LastConnectionErrorMessage = string.Empty;
            Interlocked.Exchange(ref _connectionLostRaised, 0);
            InitializeProxy();
        }

        private void InitializeProxy()
        {
            CloseClient();

            try
            {
                var context = new InstanceContext(this);
                _client = new GameplayServiceClient(context);
                _client.InnerChannel.Faulted += OnChannelFaulted;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Error inicializando proxy: {ex}");
            }
        }

        private void OnChannelFaulted(object sender, EventArgs e)
        {
            HandleConnectionFailure(new CommunicationException(GameClient.Resources.Strings.Gameplay_Error_Communication));
        }

        private GameplayServiceClient GetClient()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameplayServiceManager));
            }

            if (_client == null ||
                _client.State == CommunicationState.Closed ||
                _client.State == CommunicationState.Faulted)
            {
                InitializeProxy();
            }

            if (_client == null)
            {
                throw new CommunicationException(GameClient.Resources.Strings.Gameplay_Error_Communication);
            }

            return _client;
        }

        public void OnTurnChanged(GameStateDto newState) =>
            Application.Current.Dispatcher.InvokeAsync(() => TurnChanged?.Invoke(newState));

        public void OnGameFinished(string winner) =>
            Application.Current.Dispatcher.InvokeAsync(() => GameFinished?.Invoke(winner));

        public void OnPlayerKicked(string reason) =>
            Application.Current.Dispatcher.InvokeAsync(() => PlayerKicked?.Invoke(reason));

        public void OnVoteKickStarted(string targetUsername, string reason) =>
            Application.Current.Dispatcher.InvokeAsync(() => VoteKickStarted?.Invoke(targetUsername, reason));

        public Task<DiceRollDto> RollDiceAsync(GameplayRequest request) =>
            ExecuteAsync(c => c.RollDiceAsync(request));

        public Task<GameStateDto> GetGameStateAsync(GameplayRequest request) =>
            ExecuteAsync(c => c.GetGameStateAsync(request));

        public Task<bool> LeaveGameAsync(GameplayRequest request) =>
            ExecuteAsync(c => c.LeaveGameAsync(request));

        public Task InitiateVoteKickAsync(VoteRequestDto request) =>
            ExecuteAsync(c => c.InitiateVoteKickAsync(request));

        public Task CastVoteAsync(VoteResponseDto vote) =>
            ExecuteAsync(c => c.CastVoteAsync(vote));

        private async Task<T> ExecuteAsync<T>(Func<GameplayServiceClient, Task<T>> action)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                var ex = new CommunicationException(GameClient.Resources.Strings.Error_NoInternet);
                HandleConnectionFailure(ex);
                throw ex;
            }

            try
            {
                return await action(GetClient()).ConfigureAwait(false);
            }
            catch (FaultException<ServiceFault> fault)
            {
                HandleBusinessFault(fault);
                throw;
            }
            catch (Exception ex) when (ex is EndpointNotFoundException || ex is CommunicationException || ex is TimeoutException)
            {
                HandleConnectionFailure(ex);
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Unexpected: {ex}");
                throw;
            }
        }

        private async Task ExecuteAsync(Func<GameplayServiceClient, Task> action)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                var ex = new CommunicationException(GameClient.Resources.Strings.Error_NoInternet);
                HandleConnectionFailure(ex);
                throw ex;
            }

            try
            {
                await action(GetClient()).ConfigureAwait(false);
            }
            catch (FaultException<ServiceFault> fault)
            {
                HandleBusinessFault(fault);
                throw;
            }
            catch (Exception ex) when (ex is EndpointNotFoundException || ex is CommunicationException || ex is TimeoutException)
            {
                HandleConnectionFailure(ex);
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Unexpected (void): {ex}");
                throw;
            }
        }

        private void HandleBusinessFault(FaultException<ServiceFault> fault)
        {
            string msg = fault.Detail?.Message ?? GameClient.Resources.Strings.Error_Unknown;
            string code = fault.Detail?.Code ?? string.Empty;

            if (code == "Error_DatabaseDown" || code == "Error_DatabaseGeneric" || code == "Error_InternalData")
            {
                UserSession.GetInstance().HandleCatastrophicError(msg);
            }
        }

        private void HandleConnectionFailure(Exception ex)
        {
            LastConnectionErrorMessage = ResolveConnectionErrorMessage(ex);
            InvalidateClient();

            if (Interlocked.Exchange(ref _connectionLostRaised, 1) != 0)
            {
                return;
            }

            Application.Current.Dispatcher.InvokeAsync(() => ConnectionLost?.Invoke());
        }

        private static string ResolveConnectionErrorMessage(Exception ex)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return GameClient.Resources.Strings.Error_NoInternet;
            }

            if (ex is TimeoutException)
            {
                return GameClient.Resources.Strings.Gameplay_Error_Timeout;
            }

            return GameClient.Resources.Strings.Gameplay_Error_Communication;
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
                _client.InnerChannel.Faulted -= OnChannelFaulted;

                if (!NetworkInterface.GetIsNetworkAvailable() || _client.State == CommunicationState.Faulted)
                {
                    _client.Abort();
                }
                else
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
