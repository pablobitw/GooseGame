using GameServer.DTOs.User;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.ServiceModel; 
using System.Threading.Tasks;

namespace GameServer.Services
{

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class UserProfileService : IUserProfileService
    {
        private readonly UserProfileAppService _logic;

        public UserProfileService()
        {
            var repository = new UserProfileRepository();
            _logic = new UserProfileAppService(repository);
        }

        public async Task<UserProfileDto> GetUserProfileAsync(string email)
        {
            UserProfileDto result;
            result = await _logic.GetUserProfileAsync(email);
            return result;
        }

        public async Task<UsernameChangeResult> ChangeUsernameAsync(string email, string newUsername)
        {
            UsernameChangeResult result;
            result = await _logic.ChangeUsernameAsync(email, newUsername);
            return result;
        }

        public async Task<bool> ChangeAvatarAsync(string email, string avatarName)
        {
            bool result;
            result = await _logic.ChangeAvatarAsync(email, avatarName);
            return result;
        }

        public async Task<bool> SendPasswordChangeCodeAsync(string email)
        {
            bool result;
            result = await _logic.SendPasswordChangeCodeAsync(email);
            return result;
        }

        public async Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request)
        {
            bool result;
            result = await _logic.ChangePasswordWithCodeAsync(request);
            return result;
        }
    }
}