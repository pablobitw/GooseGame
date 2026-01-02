using System.Collections.Generic;
using System.Threading.Tasks;
using GameServer.DTOs.Lobby; 

namespace GameServer.Repositories
{
    public interface ILobbyRepository
    {
        void AddGame(Game game);
        Task<int> CountPlayersInGameAsync(int gameId);
        void DeleteGameAndCleanDependencies(Game game);
        void Dispose();
        Task<List<Game>> GetActivePublicGamesAsync();
        Task<Game> GetGameByCodeAsync(string lobbyCode);
        Task<Game> GetGameByIdAsync(int id);
        Task<Player> GetPlayerByUsernameAsync(string username);
        Task<List<Player>> GetPlayersInGameAsync(int gameId);
        Task<string> GetUsernameByIdAsync(int playerId);
        bool IsLobbyCodeUnique(string code);
        Task SaveChangesAsync();
    }
}