using GameServer.DTOs;
using System;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class SanctionRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;

        public SanctionRepository()
        {
            _context = new GameDatabase_Container();
        }

        public async Task AddSanctionAsync(Sanction sanction)
        {
            _context.Sanctions.Add(sanction);
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}