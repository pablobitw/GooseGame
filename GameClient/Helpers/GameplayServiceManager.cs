using GameClient.GameplayServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace GameClient.Helpers
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
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
            CloseClient();

            try
            {
                InstanceContext context = new InstanceContext(this);
                _client = new GameplayServiceClient(context);
            }
            catch (EndpointNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("No se pudo encontrar el servidor.");
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
            }
            catch (CommunicationException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("Se perdió la conexión con el servidor.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(
                        "Ocurrió un error inesperado al iniciar la sesión.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning));
            }
        }

        private GameplayServiceClient GetClient()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameplayServiceManager));

            if (_client == null ||
                _client.State == CommunicationState.Faulted ||
                _client.State == CommunicationState.Closed)
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

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            try
            {
                return await GetClient().RollDiceAsync(request);
            }
            catch (FaultException ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            }
            catch (EndpointNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no está disponible.");
                return null;
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
                return null;
            }
            catch (CommunicationException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("Se perdió la conexión con el servidor.");
                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show("Ocurrió un error inesperado.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
                return null;
            }
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            try
            {
                return await GetClient().GetGameStateAsync(request);
            }
            catch (FaultException ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            }
            catch (EndpointNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no está disponible.");
                return null;
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
                return null;
            }
            catch (CommunicationException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("Se perdió la conexión con el servidor.");
                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show("Ocurrió un error inesperado.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
                return null;
            }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            try
            {
                return await GetClient().LeaveGameAsync(request);
            }
            catch (FaultException ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning));
                return false;
            }
            catch (EndpointNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no está disponible.");
                return false;
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
                return false;
            }
            catch (CommunicationException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("Se perdió la conexión con el servidor.");
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show("Ocurrió un error inesperado.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
                return false;
            }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            try
            {
                await GetClient().InitiateVoteKickAsync(request);
            }
            catch (FaultException ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            catch (EndpointNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no está disponible.");
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
            }
            catch (CommunicationException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("Se perdió la conexión con el servidor.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show("Ocurrió un error inesperado.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
            }
        }

        public async Task CastVoteAsync(VoteResponseDto vote)
        {
            try
            {
                await GetClient().CastVoteAsync(vote);
            }
            catch (FaultException ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(ex.Message, "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            catch (EndpointNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no está disponible.");
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("El servidor no respondió a tiempo.");
            }
            catch (CommunicationException ex)
            {
                Console.Error.WriteLine(ex);
                SessionManager.ForceLogout("Se perdió la conexión con el servidor.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show("Ocurrió un error inesperado.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
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
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                _client.Abort();
            }
            finally
            {
                _client = null;
            }
        }
    }
}
