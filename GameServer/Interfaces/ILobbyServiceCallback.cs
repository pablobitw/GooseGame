using System.ServiceModel;
using GameServer.DTOs.Lobby; 

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface ILobbyServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnPlayerJoined(PlayerLobbyDto player);

        [OperationContract(IsOneWay = true)]
        void OnPlayerLeft(string username);

        [OperationContract(IsOneWay = true)]
        void OnGameStarted(); 

        [OperationContract(IsOneWay = true)]
        void OnLobbyDisbanded(); 

        [OperationContract(IsOneWay = true)]
        void OnPlayerKicked(string reason);

        [OperationContract(IsOneWay = true)]
        void OnLobbyMessageReceived(string username, string message); 
    }
}