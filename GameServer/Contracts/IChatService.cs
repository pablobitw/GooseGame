using System.ServiceModel;

namespace GameServer.Contracts
{
    [ServiceContract(CallbackContract = typeof(IChatCallback))]
    public interface IChatService
    {
        [OperationContract(IsOneWay = true)]
        void JoinLobbyChat(string username, string lobbyCode);

        [OperationContract(IsOneWay = true)]
        void SendLobbyMessage(string username, string lobbyCode, string message);

        [OperationContract(IsOneWay = true)]
        void LeaveLobbyChat(string username, string lobbyCode);
    }
}