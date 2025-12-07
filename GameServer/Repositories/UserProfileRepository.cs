using System;
using System.Data.Entity;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class UserProfileRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;

        public UserProfileRepository()
        {
            _context = new GameDatabase_Container();
        }

        public async Task<Player> GetPlayerWithDetailsAsync(string identifier)
        {
            return await _context.Players
                .Include(p => p.Account)
                .Include(p => p.PlayerStat)
                .FirstOrDefaultAsync(p => p.Account.Email == identifier || p.Username == identifier);
        }

        public async Task<bool> IsUsernameTakenAsync(string username)
        {
            return await _context.Players.AnyAsync(p => p.Username == username);
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