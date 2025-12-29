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
        private bool _disposed;

        public GameplayRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task<Game> GetGameByLobbyCodeAsync(string lobbyCode)
        {
            ThrowIfDisposed();
            return await _context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode).ConfigureAwait(false);
        }

        public async Task<Game> GetGameByIdAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.Games.FindAsync(gameId).ConfigureAwait(false);
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            ThrowIfDisposed();
            return await _context.Players.FirstOrDefaultAsync(p => p.Username == username).ConfigureAwait(false);
        }

        public async Task<Player> GetPlayerByIdAsync(int playerId)
        {
            ThrowIfDisposed();
            return await _context.Players.FirstOrDefaultAsync(p => p.IdPlayer == playerId).ConfigureAwait(false);
        }

        public async Task<List<Player>> GetPlayersInGameAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Where(p => p.GameIdGame == gameId)
                .OrderBy(p => p.IdPlayer)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<MoveRecord> GetLastMoveForPlayerAsync(int gameId, int playerId)
        {
            ThrowIfDisposed();
            return await _context.MoveRecords
                .Where(m => m.PlayerIdPlayer == playerId && m.GameIdGame == gameId)
                .OrderByDescending(m => m.IdMoveRecord)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<MoveRecord> GetLastGlobalMoveAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.MoveRecords
                .Where(m => m.GameIdGame == gameId)
                .OrderByDescending(m => m.IdMoveRecord)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> GetMoveCountAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.MoveRecords.CountAsync(m => m.GameIdGame == gameId).ConfigureAwait(false);
        }

        public async Task<Player> GetPlayerWithStatsByIdAsync(int playerId)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Include("PlayerStat")
                .FirstOrDefaultAsync(p => p.IdPlayer == playerId)
                .ConfigureAwait(false);
        }

        public async Task<List<Player>> GetPlayersWithStatsInGameAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Include("PlayerStat")
                .Where(p => p.GameIdGame == gameId)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public void AddSanction(Sanction sanction)
        {
            ThrowIfDisposed();
            _context.Sanctions.Add(sanction);
        }

        public int GetMoveCount(int gameId)
        {
            ThrowIfDisposed();
            return _context.MoveRecords.Count(m => m.GameIdGame == gameId);
        }

        public async Task<int> GetExtraTurnCountAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.MoveRecords
                .CountAsync(m => m.GameIdGame == gameId && m.ActionDescription.Contains("[EXTRA]"))
                .ConfigureAwait(false);
        }

        public async Task<List<string>> GetGameLogsAsync(int gameId, int count)
        {
            ThrowIfDisposed();
            return await _context.MoveRecords
                .Where(m => m.GameIdGame == gameId)
                .OrderByDescending(m => m.IdMoveRecord)
                .Take(count)
                .Select(m => m.ActionDescription)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<MoveRecord> GetWinningMoveAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.MoveRecords
                .Where(m => m.GameIdGame == gameId && m.FinalPosition == 64)
                .OrderByDescending(m => m.IdMoveRecord)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public void AddMove(MoveRecord move)
        {
            ThrowIfDisposed();
            _context.MoveRecords.Add(move);
        }

        public async Task SaveChangesAsync()
        {
            ThrowIfDisposed();
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the repository.
        /// </summary>
        /// <param name="disposing">True when called from Dispose, false when called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _context?.Dispose();
            }

            _disposed = true;
        }


        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
