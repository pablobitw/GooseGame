using System.ServiceModel;

namespace GameServer.Contracts

{
    [ServiceContract(CallbackContract = typeof(IChatCallback))]
    public interface IChatService
    {
        [OperationContract]
        void JoinChat(string username);

        [OperationContract(IsOneWay = true)]
        void SendMessage(string username, string message);

        [OperationContract(IsOneWay = true)]
        void Leave(string username);
    }
}
