using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public interface IGameplayRepository
    {
        void AddMove(MoveRecord move);
        void AddSanction(Sanction sanction);
        void Dispose();
        Task<int> GetExtraTurnCountAsync(int gameId);
        Task<Game> GetGameByIdAsync(int gameId);
        Task<Game> GetGameByLobbyCodeAsync(string lobbyCode);
        Task<List<string>> GetGameLogsAsync(int gameId, int count);
        Task<MoveRecord> GetLastGlobalMoveAsync(int gameId);
        Task<MoveRecord> GetLastMoveForPlayerAsync(int gameId, int playerId);
        int GetMoveCount(int gameId);
        Task<int> GetMoveCountAsync(int gameId);
        Task<Player> GetPlayerByIdAsync(int playerId);
        Task<Player> GetPlayerByUsernameAsync(string username);
        Task<List<Player>> GetPlayersInGameAsync(int gameId);
        Task<List<Player>> GetPlayersWithStatsInGameAsync(int gameId);
        Task<Player> GetPlayerWithStatsByIdAsync(int playerId);
        Task<MoveRecord> GetWinningMoveAsync(int gameId);
        Task SaveChangesAsync();
    }
}