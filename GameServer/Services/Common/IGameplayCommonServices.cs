using GameServer.DTOs.Gameplay;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GameServer.Services.Common
{
    public interface IGameplayStateManager
    {
        bool TryAddProcessingGame(int gameId);
        void RemoveProcessingGame(int gameId);
        int AddOrUpdateAfkStrike(string username);
        void RemoveAfkStrike(string username);
    }

    public class GameplayStateManager : IGameplayStateManager
    {
        private static readonly ConcurrentDictionary<int, bool> _processingGames = new ConcurrentDictionary<int, bool>();
        private static readonly ConcurrentDictionary<string, int> _afkStrikes = new ConcurrentDictionary<string, int>();

        public bool TryAddProcessingGame(int gameId) => _processingGames.TryAdd(gameId, true);
        public void RemoveProcessingGame(int gameId) => _processingGames.TryRemove(gameId, out _);
        public int AddOrUpdateAfkStrike(string username) => _afkStrikes.AddOrUpdate(username, 1, (key, oldValue) => oldValue + 1);
        public void RemoveAfkStrike(string username) => _afkStrikes.TryRemove(username, out _);
    }

    public interface IGameplayRepositoryFactory
    {
        IGameplayRepository Create();
    }

    public class GameplayRepositoryFactory : IGameplayRepositoryFactory
    {
        public IGameplayRepository Create() => new GameplayRepository();
    }

    public interface IVoteLogic
    {
        Task InitiateVoteAsync(VoteRequestDto request);
        Task CastVoteAsync(VoteResponseDto request);
        void CancelVote(int gameId);
    }

    public interface ISanctionAppService : System.IDisposable
    {
        Task ProcessKickAsync(string username, string lobbyCode, string reason, string source);
    }

    public interface ISanctionFactory
    {
        ISanctionAppService Create();
    }

    public class SanctionFactory : ISanctionFactory
    {
        public ISanctionAppService Create() => new SanctionAppService();
    }
}