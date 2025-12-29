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
        private bool _disposed = false;

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
            catch (Exception ex)
            {
                Console.WriteLine($"[GameplayManager] Error initializing: {ex.Message}");
            }
        }

        public GameplayServiceClient GetClient()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GameplayServiceManager));

            if (_client == null || _client.State == CommunicationState.Faulted || _client.State == CommunicationState.Closed)
            {
                Initialize(_currentUsername);
            }
            return _client;
        }

        public void OnTurnChanged(GameStateDto newState)
        {
            TurnChanged?.Invoke(newState);
        }

        public void OnGameFinished(string winner)
        {
            GameFinished?.Invoke(winner);
        }

        public void OnPlayerKicked(string reason)
        {
            PlayerKicked?.Invoke(reason);
        }

        public void OnVoteKickStarted(string targetUsername, string reason)
        {
            VoteKickStarted?.Invoke(targetUsername, reason);
        }

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            try { return await GetClient().RollDiceAsync(request); }
            catch (Exception) { throw; }
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            try { return await GetClient().GetGameStateAsync(request); }
            catch (Exception) { throw; }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            try { return await GetClient().LeaveGameAsync(request); }
            catch (Exception) { return false; }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            try { await GetClient().InitiateVoteKickAsync(request); }
            catch (Exception) { throw; }
        }

        public async Task CastVoteAsync(VoteResponseDto vote)
        {
            try { await GetClient().CastVoteAsync(vote); }
            catch (Exception) { throw; }
        }

        public void Dispose()
        {
            DisposeInternal(true);
            GC.SuppressFinalize(this);
        }

        private void DisposeInternal(bool disposing)
        {
            if (_disposed) return;
            if (disposing) CloseClient();
            _disposed = true;
        }

        private void CloseClient()
        {
            if (_client == null) return;
            try
            {
                if (_client.State == CommunicationState.Opened) _client.Close();
                else _client.Abort();
            }
            catch (Exception) { _client.Abort(); }
        }
    }
}