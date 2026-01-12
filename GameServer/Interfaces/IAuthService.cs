using GameServer.DTOs.Auth;
using GameServer.Faults;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IAuthService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<LoginResponseDto> LogInAsync(string usernameOrEmail, string password);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<GuestLoginResult> LoginAsGuestAsync();

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void Logout(string username);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        bool VerifyAccount(string email, string code);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> RequestPasswordResetAsync(string email);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        bool VerifyRecoveryCode(string email, string code);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        bool UpdatePassword(string email, string newPassword);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> ResendVerificationCodeAsync(string email);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> ChangeUserPasswordAsync(string username, string currentPassword, string newPassword);
    }
}