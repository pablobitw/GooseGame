using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class LobbyRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;

        public LobbyRepository()
        {
            _context = new GameDatabase_Container();
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            return await _context.Players.FirstOrDefaultAsync(p => p.Username == username);
        }

        public async Task<Game> GetGameByCodeAsync(string lobbyCode)
        {
            return await _context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
        }

        public async Task<Game> GetGameByIdAsync(int id)
        {
            return await _context.Games.FindAsync(id);
        }

        public async Task<List<Player>> GetPlayersInGameAsync(int gameId)
        {
            return await _context.Players.Where(p => p.GameIdGame == gameId).ToListAsync();
        }

        public bool IsLobbyCodeUnique(string code)
        {
            return !_context.Games.Any(g => g.LobbyCode == code && g.GameStatus != (int)GameStatus.Finished);
        }

        public void AddGame(Game game)
        {
            _context.Games.Add(game);
        }

        public void DeleteGameAndCleanDependencies(Game game)
        {
            var players = _context.Players.Where(p => p.GameIdGame == game.IdGame).ToList();
            foreach (var p in players)
            {
                p.GameIdGame = null;
            }

            var moves = _context.MoveRecords.Where(m => m.GameIdGame == game.IdGame);
            _context.MoveRecords.RemoveRange(moves);

            var sanctions = _context.Sanctions.Where(s => s.Game_IdGame == game.IdGame);
            _context.Sanctions.RemoveRange(sanctions);

            _context.Games.Remove(game);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}