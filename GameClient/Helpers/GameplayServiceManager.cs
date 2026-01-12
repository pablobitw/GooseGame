using GameClient.GameplayServiceReference;
using System;
using System.Net.NetworkInformation;
using System.ServiceModel;
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

        public event Action<GameStateDto> TurnChanged;
        public event Action<string> GameFinished;
        public event Action<string> PlayerKicked;
        public event Action<string, string> VoteKickStarted;

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
                            _instance = new GameplayServiceManager();
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
            CloseClient();

            try
            {
                var context = new InstanceContext(this);
                _client = new GameplayServiceClient(context);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private GameplayServiceClient GetClient()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameplayServiceManager));

            if (_client == null ||
                _client.State == CommunicationState.Closed ||
                _client.State == CommunicationState.Faulted)
            {
                InitializeProxy();
            }

            if (_client == null)
                throw new CommunicationException(GameClient.Resources.Strings.Error_Communication);

            return _client;
        }

        public void OnTurnChanged(GameStateDto newState)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
                TurnChanged?.Invoke(newState));
        }

        public void OnGameFinished(string winner)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
                GameFinished?.Invoke(winner));
        }

        public void OnPlayerKicked(string reason)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
                PlayerKicked?.Invoke(reason));
        }

        public void OnVoteKickStarted(string targetUsername, string reason)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
                VoteKickStarted?.Invoke(targetUsername, reason));
        }

        public Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            return ExecuteAsync(c => c.RollDiceAsync(request));
        }

        public Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            return ExecuteAsync(c => c.GetGameStateAsync(request));
        }

        public Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            return ExecuteAsync(c => c.LeaveGameAsync(request));
        }

        public Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            return ExecuteAsync(c => c.InitiateVoteKickAsync(request));
        }

        public Task CastVoteAsync(VoteResponseDto vote)
        {
            return ExecuteAsync(c => c.CastVoteAsync(vote));
        }

        private async Task<T> ExecuteAsync<T>(Func<GameplayServiceClient, Task<T>> action)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                throw new CommunicationException(GameClient.Resources.Strings.Error_NoInternet);
            }

            try
            {
                return await action(GetClient());
            }
            catch (FaultException<ServiceFault> fault)
            {
                string msg = fault.Detail != null ? fault.Detail.Message : GameClient.Resources.Strings.Error_Unknown;
                string code = fault.Detail != null ? fault.Detail.Code : string.Empty;

                if (code == "Error_DatabaseDown" || code == "Error_DatabaseGeneric" || code == "Error_InternalData")
                {
                    UserSession.GetInstance().HandleCatastrophicError(msg);
                    return default;
                }

                throw new Exception(msg);
            }
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                UserSession.GetInstance().HandleCatastrophicError(GameClient.Resources.Strings.SafeZone_DatabaseError);
                throw;
            }
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is ObjectDisposedException)
            {
                Console.WriteLine($"[GameplayManager] Network error: {ex.Message}. Retrying...");

                try
                {
                    InitializeProxy();
                    return await action(GetClient());
                }
                catch (Exception retryEx)
                {
                    if (retryEx is EndpointNotFoundException)
                    {
                        UserSession.GetInstance().HandleCatastrophicError(GameClient.Resources.Strings.SafeZone_DatabaseError);
                        throw;
                    }

                    if (retryEx is TimeoutException)
                    {
                        throw new TimeoutException(GameClient.Resources.Strings.SafeZone_ServerTimeout, retryEx);
                    }

                    throw new CommunicationException(GameClient.Resources.Strings.Error_Communication, retryEx);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Unexpected error: {ex}");
                throw;
            }
        }

        private async Task ExecuteAsync(Func<GameplayServiceClient, Task> action)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                throw new CommunicationException(GameClient.Resources.Strings.Error_NoInternet);
            }

            try
            {
                await action(GetClient());
            }
            catch (FaultException<ServiceFault> fault)
            {
                string msg = fault.Detail != null ? fault.Detail.Message : GameClient.Resources.Strings.Error_Unknown;
                string code = fault.Detail != null ? fault.Detail.Code : string.Empty;

                if (code == "Error_DatabaseDown" || code == "Error_DatabaseGeneric" || code == "Error_InternalData")
                {
                    UserSession.GetInstance().HandleCatastrophicError(msg);
                    return;
                }

                throw new Exception(msg);
            }
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                UserSession.GetInstance().HandleCatastrophicError(GameClient.Resources.Strings.SafeZone_DatabaseError);
                throw;
            }
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is ObjectDisposedException)
            {
                Console.WriteLine($"[GameplayManager] Network error (void): {ex.Message}. Retrying...");

                try
                {
                    InitializeProxy();
                    await action(GetClient());
                }
                catch (Exception retryEx)
                {
                    if (retryEx is EndpointNotFoundException)
                    {
                        UserSession.GetInstance().HandleCatastrophicError(GameClient.Resources.Strings.SafeZone_DatabaseError);
                        throw;
                    }

                    if (retryEx is TimeoutException)
                    {
                        throw new TimeoutException(GameClient.Resources.Strings.SafeZone_ServerTimeout, retryEx);
                    }

                    throw new CommunicationException(GameClient.Resources.Strings.Error_Communication, retryEx);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Unexpected error (void): {ex}");
                throw;
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
            catch (Exception)
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