using System.ServiceModel; 

namespace GameServer
{
    // [ServiceContract] le dice a WCF que esta interfaz define un servicio público.
    [ServiceContract]
    public interface IGameService
    {
        // [OperationContract] marca cada método como una operación que un cliente puede llamar.
        [OperationContract]
        bool RegistrarUsuario(string username, string email, string password);

        [OperationContract]
        bool IniciarSesion(string username, string password);
    }
}