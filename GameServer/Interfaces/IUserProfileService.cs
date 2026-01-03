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
        Task<UsernameChangeResult> ChangeUsernameAsync(string email, string newUsername);

        [OperationContract]
        Task<bool> ChangeAvatarAsync(string email, string avatarName);

        [OperationContract]
        Task<bool> SendPasswordChangeCodeAsync(string email);

        [OperationContract]
        Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request);

        [OperationContract]
        Task<bool> DeactivateAccountAsync(DeactivateAccountRequest request);
    
    }
}