using GameServer.DTOs.User;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IUserProfileService
    {
        [OperationContract]
        Task<UserProfileDto> GetUserProfileAsync(string email);

        [OperationContract]
        Task<UsernameChangeResult> ChangeUsernameAsync(string identifier, string newUsername, string verificationCode);

        [OperationContract]
        Task<bool> ChangeAvatarAsync(string email, string avatarName);

        [OperationContract]
        Task<bool> SendPasswordChangeCodeAsync(string email);

        [OperationContract]
        Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request);

        [OperationContract]
        Task<bool> DeactivateAccountAsync(DeactivateAccountRequest request);

        [OperationContract]
        Task<bool> SendUsernameChangeCodeAsync(string identifier);

        [OperationContract]
        Task<bool> UpdateLanguageAsync(string email, string languageCode);

        [OperationContract]
        Task<string> AddSocialLinkAsync(string identifier, string url);

        [OperationContract]
        Task<bool> RemoveSocialLinkAsync(string identifier, string url);
    }
}