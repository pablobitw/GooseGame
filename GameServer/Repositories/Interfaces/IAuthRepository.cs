using System;
using System.Threading.Tasks;

namespace GameServer.Repositories.Interfaces
{
    public interface IAuthRepository : IDisposable
    {
        void AddPlayer(Player player);

        Account GetAccountByEmail(string email);
        Task<Account> GetAccountByEmailAsync(string email);
        Task<Player> GetPlayerByUsernameAsync(string username);
        Task<Player> GetPlayerForLoginAsync(string usernameOrEmail);
        bool IsUsernameTaken(string username);

        void SaveChanges();
        Task SaveChangesAsync();

        bool VerifyRecoveryCode(string email, string code);
        Task<bool> IsAccountSanctionedAsync(int accountId);
    }
}
