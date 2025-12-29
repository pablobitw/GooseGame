using GameServer.DTOs;
using System;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class SanctionRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;
        private bool _disposed;

        public SanctionRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task AddSanctionAsync(Sanction sanction)
        {
            ThrowIfDisposed();
            _context.Sanctions.Add(sanction);
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
