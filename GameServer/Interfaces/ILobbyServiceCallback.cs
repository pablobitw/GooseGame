using System.ServiceModel;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface ILobbyServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnPlayerKicked(string reason);
    }
}
