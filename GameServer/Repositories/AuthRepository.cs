using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class AuthRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;

        public AuthRepository()
        {
            _context = new GameDatabase_Container();
        }

        public bool IsUsernameTaken(string username)
        {
            return _context.Players.Any(p => p.Username == username);
        }

        public Account GetAccountByEmail(string email)
        {
            return _context.Accounts.FirstOrDefault(a => a.Email == email);
        }

        public async Task<Account> GetAccountByEmailAsync(string email)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        }

        public async Task<Player> GetPlayerForLoginAsync(string usernameOrEmail)
        {
            return await _context.Players
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.Username == usernameOrEmail || p.Account.Email == usernameOrEmail);
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            return _context.Accounts.Any(a =>
                a.Email == email &&
                a.VerificationCode == code &&
                a.VerificationCode != null &&
                a.CodeExpiration >= DateTime.Now);
        }

        public void AddPlayer(Player player)
        {
            _context.Players.Add(player);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void SaveChanges()
        {
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}