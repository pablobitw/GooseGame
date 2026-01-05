using GameServer.Repositories.Interfaces;
using System;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class UserProfileRepository : IUserProfileRepository, IDisposable
    {
        private readonly GameDatabase_Container _context;
        private bool _disposed;

        public UserProfileRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task<Player> GetPlayerWithDetailsAsync(string identifier)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Include(p => p.Account)
                .Include(p => p.PlayerStat)
                .Include(p => p.PlayerSocialLinks)
                .FirstOrDefaultAsync(p => (p.Account != null && p.Account.Email == identifier) || p.Username == identifier)
                .ConfigureAwait(false);
        }

        public async Task<bool> IsUsernameTakenAsync(string username)
        {
            ThrowIfDisposed();
            return await _context.Players.AnyAsync(p => p.Username == username).ConfigureAwait(false);
        }

        public async Task SaveChangesAsync()
        {
            ThrowIfDisposed();
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public void DeleteSocialLink(PlayerSocialLink link)
        {
            ThrowIfDisposed();
            _context.PlayerSocialLinks.Remove(link);
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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

        #endregion
    }
}