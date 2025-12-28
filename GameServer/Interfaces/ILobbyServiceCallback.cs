using System.ServiceModel;

namespace GameServer.Interfaces
{
    public interface ILobbyServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnPlayerKicked(string reason);
    }
}