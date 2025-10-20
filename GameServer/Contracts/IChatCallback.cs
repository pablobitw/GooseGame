using System.ServiceModel;

namespace GameServer.Contracts

{
    public interface IChatCallback
    {
        [OperationContract(IsOneWay = true)]
        void ReceiveMessage(string username, string message);
    }
}
