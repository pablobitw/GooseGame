using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using GameServer;

namespace GameServer.Repositories
{
    public class FriendshipRepository : IDisposable, IFriendshipRepository
    {
        private readonly GameDatabase_Container _context;
        private bool _disposed;

        public FriendshipRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            ThrowIfDisposed();
            return await _context.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Username == username)
                .ConfigureAwait(false);
        }

        public Player GetPlayerById(int id)
        {
            ThrowIfDisposed();
            return _context.Players
                .AsNoTracking()
                .FirstOrDefault(p => p.IdPlayer == id);
        }

        public Friendship GetFriendship(int userId1, int userId2)
        {
            ThrowIfDisposed();
            return _context.Friendships
                .Include(f => f.Player)
                .Include(f => f.Player1)
                .FirstOrDefault(f =>
                    (f.PlayerIdPlayer == userId1 && f.Player1_IdPlayer == userId2) ||
                    (f.PlayerIdPlayer == userId2 && f.Player1_IdPlayer == userId1));
        }

        public Friendship GetPendingRequest(int requesterId, int responderId)
        {
            ThrowIfDisposed();
            return _context.Friendships
                .Include(f => f.Player)
                .Include(f => f.Player1)
                .FirstOrDefault(f =>
                    f.PlayerIdPlayer == requesterId &&
                    f.Player1_IdPlayer == responderId &&
                    f.FriendshipStatus == (int)FriendshipStatus.Pending);
        }

        public List<Friendship> GetAcceptedFriendships(int playerId)
        {
            ThrowIfDisposed();
            var accepted = (int)FriendshipStatus.Accepted;
            return _context.Friendships
                .Include(f => f.Player)
                .Include(f => f.Player1)
                .AsNoTracking()
                .Where(f => (f.PlayerIdPlayer == playerId || f.Player1_IdPlayer == playerId) && f.FriendshipStatus == accepted)
                .ToList();
        }

        public List<Friendship> GetIncomingPendingRequests(int playerId)
        {
            ThrowIfDisposed();
            var pending = (int)FriendshipStatus.Pending;
            return _context.Friendships
                .Include(f => f.Player)
                .Include(f => f.Player1)
                .AsNoTracking()
                .Where(f => f.Player1_IdPlayer == playerId && f.FriendshipStatus == pending)
                .ToList();
        }

        public List<Friendship> GetOutgoingPendingRequests(int playerId)
        {
            ThrowIfDisposed();
            var pending = (int)FriendshipStatus.Pending;
            return _context.Friendships
                .Include(f => f.Player)
                .Include(f => f.Player1)
                .AsNoTracking()
                .Where(f => f.PlayerIdPlayer == playerId && f.FriendshipStatus == pending)
                .ToList();
        }

        public void AddFriendship(Friendship friendship)
        {
            ThrowIfDisposed();
            _context.Friendships.Add(friendship);
        }

        public void RemoveFriendship(Friendship friendship)
        {
            ThrowIfDisposed();
            if (_context.Entry(friendship).State == EntityState.Detached)
            {
                _context.Friendships.Attach(friendship);
            }
            _context.Friendships.Remove(friendship);
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

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _context?.Dispose();
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}