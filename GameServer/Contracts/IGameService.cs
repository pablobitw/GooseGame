using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer
{
    [ServiceContract]
    public interface IGameService
    {
        [OperationContract]
        Task<bool> RegisterUser(string username, string email, string password);


        [OperationContract]
        public async Task<bool> LogIn(string username, string password);

        [OperationContract]
        bool VerifyAccount(string email, string code);

             [OperationContract]
        Task<bool> RequestPasswordReset(string email);

           [OperationContract]
        bool VerifyRecoveryCode(string email, string code);

            [OperationContract]
        bool UpdatePassword(string email, string newPassword);
    }
    
}
