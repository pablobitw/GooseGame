using GameServer.Contracts;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer
{
    [ServiceContract]
    public interface IGameService
    {
        [OperationContract]
        Task<RegistrationResult> RegisterUserAsync(string username, string email, string password);

        [OperationContract]
        Task<bool> LogInAsync(string usernameOrEmail, string password);
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

namespace GameServer.Contracts
{
    [DataContract]
    public enum RegistrationResult
    {
        [EnumMember]
        Success,
        [EnumMember]
        UsernameAlreadyExists,
        [EnumMember]
        EmailAlreadyExists,
        [EnumMember]
        EmailPendingVerification,
        [EnumMember]
        FatalError
    }
}