using GameServer.DTOs.Lobby; 
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(ILobbyServiceCallback))]
    public interface ILobbyService
    {
        [OperationContract]
        Task<LobbyCreationResultDTO> CreateLobbyAsync(CreateLobbyRequest request);

        [OperationContract]
        Task<bool> StartGameAsync(string lobbyCode);

        [OperationContract]
        Task DisbandLobbyAsync(string hostUsername);

        [OperationContract]
        Task<bool> LeaveLobbyAsync(string username);

        [OperationContract]
        Task<JoinLobbyResultDTO> JoinLobbyAsync(JoinLobbyRequest request);

        [OperationContract]
        Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode);

        [OperationContract]
        Task<ActiveMatchDTO[]> GetPublicMatchesAsync();

        [OperationContract]
        Task KickPlayerAsync(KickPlayerRequest request);
    }
}