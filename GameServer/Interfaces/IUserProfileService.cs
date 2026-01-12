using GameServer.DTOs.User;
using GameServer.Faults;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IUserProfileService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<UserProfileDto> GetUserProfileAsync(string email);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<UsernameChangeResult> ChangeUsernameAsync(string identifier, string newUsername, string verificationCode);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> ChangeAvatarAsync(string email, string avatarName);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> SendPasswordChangeCodeAsync(string email);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> DeactivateAccountAsync(DeactivateAccountRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> SendUsernameChangeCodeAsync(string identifier);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> UpdateLanguageAsync(string email, string languageCode);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<string> AddSocialLinkAsync(string identifier, string url);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> RemoveSocialLinkAsync(string identifier, string url);
    }
}