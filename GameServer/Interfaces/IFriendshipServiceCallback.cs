using System.ServiceModel;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IFriendshipServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnFriendRequestReceived();

        [OperationContract(IsOneWay = true)]
        void OnFriendListUpdated();

        [OperationContract(IsOneWay = true)]
        void OnGameInvitationReceived(string hostUsername, string lobbyCode);
    }
}