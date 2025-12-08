using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class GameplayRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;

        public GameplayRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task<Game> GetGameByLobbyCodeAsync(string lobbyCode)
        {
            return await _context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            return await _context.Players.FirstOrDefaultAsync(p => p.Username == username);
        }

        public async Task<Player> GetPlayerByIdAsync(int playerId)
        {
            return await _context.Players.FirstOrDefaultAsync(p => p.IdPlayer == playerId);
        }

        public async Task<List<Player>> GetPlayersInGameAsync(int gameId)
        {
            return await _context.Players
                .Where(p => p.GameIdGame == gameId)
                .OrderBy(p => p.IdPlayer)
                .ToListAsync();
        }

        public async Task<MoveRecord> GetLastMoveForPlayerAsync(int gameId, int playerId)
        {
            return await _context.MoveRecords
                .Where(m => m.PlayerIdPlayer == playerId && m.GameIdGame == gameId)
                .OrderByDescending(m => m.IdMoveRecord)
                .FirstOrDefaultAsync();
        }

        public async Task<MoveRecord> GetLastGlobalMoveAsync(int gameId)
        {
            return await _context.MoveRecords
                .Where(m => m.GameIdGame == gameId)
                .OrderByDescending(m => m.IdMoveRecord)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetMoveCountAsync(int gameId)
        {
            return await _context.MoveRecords.CountAsync(m => m.GameIdGame == gameId);
        }

        public async Task<Player> GetPlayerWithStatsByIdAsync(int playerId)
        {
            return await _context.Players
                .Include("PlayerStat") // Carga ansiosa explícita
                .FirstOrDefaultAsync(p => p.IdPlayer == playerId);
        }

        public async Task<List<Player>> GetPlayersWithStatsInGameAsync(int gameId)
        {
            return await _context.Players
                .Include("PlayerStat")
                .Where(p => p.GameIdGame == gameId)
                .ToListAsync();
        }

        public int GetMoveCount(int gameId)
        {
            return _context.MoveRecords.Count(m => m.GameIdGame == gameId);
        }

        public async Task<int> GetExtraTurnCountAsync(int gameId)
        {
            return await _context.MoveRecords
                .CountAsync(m => m.GameIdGame == gameId && m.ActionDescription.Contains("[EXTRA]"));
        }

        public async Task<List<string>> GetGameLogsAsync(int gameId, int count)
        {
            return await _context.MoveRecords
                .Where(m => m.GameIdGame == gameId)
                .OrderByDescending(m => m.IdMoveRecord)
                .Take(count)
                .Select(m => m.ActionDescription)
                .ToListAsync();
        }

        public async Task<MoveRecord> GetWinningMoveAsync(int gameId)
        {
            return await _context.MoveRecords
                .Where(m => m.GameIdGame == gameId && m.FinalPosition == 64)
                .OrderByDescending(m => m.IdMoveRecord)
                .FirstOrDefaultAsync();
        }

        public void AddMove(MoveRecord move)
        {
            _context.MoveRecords.Add(move);
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