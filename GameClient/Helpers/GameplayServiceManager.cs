using GameClient.GameplayServiceReference;
using GameClient.Helpers;
using System;
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

            if (_client == null) throw new CommunicationException("Client could not be initialized");

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
            return ExecuteAsync(c => c.RollDiceAsync(request), new DiceRollDto { Success = false, ErrorMessage = "Error crítico." });
        }

        public Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            return ExecuteAsync(c => c.GetGameStateAsync(request), new GameStateDto { Success = false, ErrorMessage = "Error crítico." });
        }

        public Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            return ExecuteAsync(c => c.LeaveGameAsync(request), false);
        }

        public Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            return ExecuteAsync(c => c.InitiateVoteKickAsync(request));
        }

        public Task CastVoteAsync(VoteResponseDto vote)
        {
            return ExecuteAsync(c => c.CastVoteAsync(vote));
        }

        private async Task<T> ExecuteAsync<T>(Func<GameplayServiceClient, Task<T>> action, T defaultValue)
        {
            try
            {
                return await action(GetClient());
            }
     
            catch (FaultException<ServiceFault> fault)
            {

                string errorMsg = fault.Detail != null ? fault.Detail.Message : "Error crítico en el servidor.";
                UserSession.GetInstance().HandleCatastrophicError(errorMsg);
                return defaultValue;
            }

            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                UserSession.GetInstance().HandleCatastrophicError(GameClient.Resources.Strings.SafeZone_DatabaseError); 
                return defaultValue;
            }

            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is ObjectDisposedException)
            {
                Console.WriteLine($"[GameplayManager] Error de red: {ex.Message}. Reintentando...");

                try
                {
                    InitializeProxy();
                    return await action(GetClient());
                }
                catch (Exception retryEx)
                {
                    Console.WriteLine($"[GameplayManager] Falló el reintento: {retryEx.Message}");
            
                    return defaultValue;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Error inesperado: {ex}");
                return defaultValue;
            }
        }

        private async Task ExecuteAsync(Func<GameplayServiceClient, Task> action)
        {
            try
            {
                await action(GetClient());
            }

            catch (FaultException<ServiceFault> fault)
            {
                string errorMsg = fault.Detail != null ? fault.Detail.Message : "Error crítico en el servidor.";
                UserSession.GetInstance().HandleCatastrophicError(errorMsg);
            }
 
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                UserSession.GetInstance().HandleCatastrophicError(GameClient.Resources.Strings.SafeZone_DatabaseError);
            }

            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is ObjectDisposedException)
            {
                Console.WriteLine($"[GameplayManager] Error de red (void): {ex.Message}. Reintentando...");
                try
                {
                    InitializeProxy();
                    await action(GetClient());
                }
                catch (Exception retryEx)
                {
                    Console.WriteLine($"[GameplayManager] Falló el reintento (void): {retryEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameplayManager] Error inesperado (void): {ex}");
            }
        }

        private static Task ShowWarningAsync(string message)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    message,
                    GameClient.Resources.Strings.DialogWarningTitle ?? "Advertencia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning)).Task;
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