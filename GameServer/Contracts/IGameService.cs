using GameServer.Contracts;
using GameServer.GameServer.Contracts;
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
        Task<bool> LogInAsync(string username, string password);

        [OperationContract]
        bool VerifyAccount(string email, string code);

             [OperationContract]
        Task<bool> RequestPasswordReset(string email);

           [OperationContract]
        bool VerifyRecoveryCode(string email, string code);

            [OperationContract]
        bool UpdatePassword(string email, string newPassword);
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

    }
