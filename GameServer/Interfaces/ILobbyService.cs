using GameServer.DTOs.Lobby;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(ILobbyServiceCallback))]
    public interface ILobbyService
    {
        [OperationContract]
        Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request);

        [OperationContract]
        Task<bool> StartGameAsync(string lobbyCode);

        [OperationContract]
        Task DisbandLobbyAsync(string hostUsername);

        [OperationContract]
        Task<bool> LeaveLobbyAsync(string username);

        [OperationContract]
        Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request);

        [OperationContract]
        Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode);

        [OperationContract]
        Task<ActiveMatchDto[]> GetPublicMatchesAsync();

        [OperationContract]
        Task<bool> KickPlayerAsync(KickPlayerRequest request);


    }
}