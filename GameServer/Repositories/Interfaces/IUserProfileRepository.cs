using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Repositories.Interfaces
{
    public interface IUserProfileRepository
    {
        Task<Player> GetPlayerWithDetailsAsync(string identifier);
        Task<bool> IsUsernameTakenAsync(string newUsername);
        void DeleteSocialLink(PlayerSocialLink link);
        Task SaveChangesAsync();
    }
}