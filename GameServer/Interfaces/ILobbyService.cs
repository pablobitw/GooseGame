using GameServer.DTOs.Lobby;
using GameServer.Faults;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(ILobbyServiceCallback))]
    public interface ILobbyService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> StartGameAsync(string lobbyCode);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task DisbandLobbyAsync(string hostUsername);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> LeaveLobbyAsync(string username);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<ActiveMatchDto[]> GetPublicMatchesAsync();

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> KickPlayerAsync(KickPlayerRequest request);
    }
}