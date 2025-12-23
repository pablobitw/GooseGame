using GameServer.DTOs.Auth;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IGameService
    {
        [OperationContract]
        Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request);

        [OperationContract]
        Task<bool> LogInAsync(string usernameOrEmail, string password);

        [OperationContract]
        Task<GuestLoginResult> LoginAsGuestAsync();

        [OperationContract]
        void Logout(string username);

        [OperationContract]
        bool VerifyAccount(string email, string code);

        [OperationContract]
        Task<bool> RequestPasswordResetAsync(string email);

        [OperationContract]
        bool VerifyRecoveryCode(string email, string code);

        [OperationContract]
        bool UpdatePassword(string email, string newPassword);

        [OperationContract]
        Task<bool> ResendVerificationCodeAsync(string email);
    }
}