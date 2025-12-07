using System.ServiceModel;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IChatCallback
    {
        [OperationContract(IsOneWay = true)]
        void ReceiveMessage(string username, string message);
    }
}