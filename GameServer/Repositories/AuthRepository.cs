using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories.Interfaces
{
    public class AuthRepository : IDisposable, IAuthRepository
    {
        private readonly GameDatabase_Container _context;
        private bool _disposed;

        public AuthRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public bool IsUsernameTaken(string username)
        {
            ThrowIfDisposed();
            return _context.Players.Any(p => p.Username == username);
        }

        public Account GetAccountByEmail(string email)
        {
            ThrowIfDisposed();
            return _context.Accounts.FirstOrDefault(a => a.Email == email);
        }

        public async Task<Account> GetAccountByEmailAsync(string email)
        {
            ThrowIfDisposed();
            return await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email).ConfigureAwait(false);
        }

        public async Task<Player> GetPlayerForLoginAsync(string usernameOrEmail)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.Username == usernameOrEmail || (p.Account != null && p.Account.Email == usernameOrEmail))
                .ConfigureAwait(false);
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            ThrowIfDisposed();
            return await _context.Players
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.Username == username)
                .ConfigureAwait(false);
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            ThrowIfDisposed();
            return _context.Accounts.Any(a =>
                a.Email == email &&
                a.VerificationCode == code &&
                a.VerificationCode != null &&
                a.CodeExpiration >= DateTime.Now);
        }

        public void AddPlayer(Player player)
        {
            ThrowIfDisposed();
            _context.Players.Add(player);
        }

        public async Task SaveChangesAsync()
        {
            ThrowIfDisposed();
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public void SaveChanges()
        {
            ThrowIfDisposed();
            _context.SaveChanges();
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
