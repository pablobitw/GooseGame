using GameClient.GameplayServiceReference;
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
            CloseClient();

            try
            {
                var context = new InstanceContext(this);
                _client = new GameplayServiceClient(context);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_ConnectionLost);
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
                Initialize(_currentUsername);
            }

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
            try
            {
                return await action(GetClient());
            }
            catch (FaultException ex)
            {
                await ShowWarningAsync(ex.Message);
                return default(T);
            }
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_DatabaseError);
                return default(T);
            }
            catch (TimeoutException)
            {
                InvalidateClient();
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_ServerTimeout);
                return default(T);
            }
            catch (CommunicationException)
            {
                InvalidateClient();
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_ConnectionLost);
                return default(T);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await ShowErrorAsync();
                return default(T);
            }
        }

        private async Task ExecuteAsync(Func<GameplayServiceClient, Task> action)
        {
            try
            {
                await action(GetClient());
            }
            catch (FaultException ex)
            {
                await ShowWarningAsync(ex.Message);
            }
            catch (EndpointNotFoundException)
            {
                InvalidateClient();
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_DatabaseError);
            }
            catch (TimeoutException)
            {
                InvalidateClient();
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_ServerTimeout);
            }
            catch (CommunicationException)
            {
                InvalidateClient();
                SessionManager.ForceLogout(GameClient.Resources.Strings.SafeZone_ConnectionLost);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await ShowErrorAsync();
            }
        }

        private Task ShowWarningAsync(string message)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    message,
                    GameClient.Resources.Strings.GameplayWarningTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning)).Task;
        }

        private Task ShowErrorAsync()
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    GameClient.Resources.Strings.ErrorGameplayUnexpected,
                    GameClient.Resources.Strings.DialogErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information)).Task;
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