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
            return await _logic.GetUserProfileAsync(email);
        }

        public async Task<bool> SendUsernameChangeCodeAsync(string email)
        {
            return await _logic.SendUsernameChangeCodeAsync(email);
        }

        public async Task<UsernameChangeResult> ChangeUsernameAsync(string email, string newUsername, string verificationCode)
        {
            return await _logic.ChangeUsernameAsync(email, newUsername, verificationCode);
        }

        public async Task<bool> ChangeAvatarAsync(string email, string avatarName)
        {
            return await _logic.ChangeAvatarAsync(email, avatarName);
        }

        public async Task<bool> SendPasswordChangeCodeAsync(string email)
        {
            return await _logic.SendPasswordChangeCodeAsync(email);
        }

        public async Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request)
        {
            return await _logic.ChangePasswordWithCodeAsync(request);
        }

        public async Task<bool> DeactivateAccountAsync(DeactivateAccountRequest request)
        {
            return await _logic.DeactivateAccountAsync(request);
        }

        public async Task<bool> UpdateLanguageAsync(string email, string languageCode)
        {
            return await _logic.UpdateLanguageAsync(email, languageCode);
        }

        public async Task<string> AddSocialLinkAsync(string identifier, string url)
        {
            return await _logic.AddSocialLinkAsync(identifier, url);
        }

        public async Task<bool> RemoveSocialLinkAsync(string identifier, string url)
        {
            return await _logic.RemoveSocialLinkAsync(identifier, url);
        }
    }
}