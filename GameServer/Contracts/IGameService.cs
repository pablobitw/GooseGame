using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer
{
    [ServiceContract]
    public interface IGameService
    {
        [OperationContract]
        Task<bool> RegisterUser(string username, string email, string password); 
        // hacemos el método asíncrono para que no congele el servidor mientras espera operaciones lentas,
         // como enviar un corre devuelve una promesa de que en el futuro entregara un `bool

        [OperationContract]
        bool LogIn(string username, string password);

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
