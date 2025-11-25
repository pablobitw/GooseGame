using System.ServiceModel;

namespace GameServer.Contracts
{
    [ServiceContract]
    public interface IFriendshipServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnFriendRequestReceived();

        [OperationContract(IsOneWay = true)]
        void OnFriendListUpdated();
    }
}