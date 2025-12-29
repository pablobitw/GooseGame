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
        private bool _disposed;

        public LobbyRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            ThrowIfDisposed();
            return await _context.Players
                .FirstOrDefaultAsync(p => p.Username == username)
                .ConfigureAwait(false);
        }

        public async Task<Game> GetGameByCodeAsync(string lobbyCode)
        {
            ThrowIfDisposed();
            return await _context.Games
                .FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode)
                .ConfigureAwait(false);
        }

        public async Task<Game> GetGameByIdAsync(int id)
        {
            ThrowIfDisposed();
            return await _context.Games.FindAsync(id).ConfigureAwait(false);
        }

        public async Task<List<Player>> GetPlayersInGameAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Where(p => p.GameIdGame == gameId)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public bool IsLobbyCodeUnique(string code)
        {
            ThrowIfDisposed();
            return !_context.Games.Any(g => g.LobbyCode == code && g.GameStatus != (int)GameStatus.Finished);
        }

        public void AddGame(Game game)
        {
            ThrowIfDisposed();
            _context.Games.Add(game);
        }

        public void DeleteGameAndCleanDependencies(Game game)
        {
            ThrowIfDisposed();

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

        public async Task<List<Game>> GetActivePublicGamesAsync()
        {
            ThrowIfDisposed();
            return await _context.Games
                .Where(g => g.IsPublic && g.GameStatus == (int)GameStatus.WaitingForPlayers)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<string> GetUsernameByIdAsync(int playerId)
        {
            ThrowIfDisposed();
            var player = await _context.Players.FindAsync(playerId).ConfigureAwait(false);
            return player?.Username ?? "Desconocido";
        }

        public async Task<int> CountPlayersInGameAsync(int gameId)
        {
            ThrowIfDisposed();
            return await _context.Players.CountAsync(p => p.GameIdGame == gameId).ConfigureAwait(false);
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
