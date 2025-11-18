using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GameServer.Contracts
{
    [ServiceContract]
    public interface ILobbyService
    {
        [OperationContract]
        Task<LobbyCreationResultDTO> CreateLobbyAsync(LobbySettingsDTO settings, string hostUsername);

        [OperationContract]
        Task<bool> StartGameAsync(string lobbyCode);

        [OperationContract]
        Task DisbandLobbyAsync(string hostUsername);

        [OperationContract]
        Task<JoinLobbyResultDTO> JoinLobbyAsync(string lobbyCode, string joiningUsername);

        [OperationContract]
        Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode);
    }

    [DataContract]
    public class LobbySettingsDTO
    {
        [DataMember]
        public bool IsPublic { get; set; }

        [DataMember]
        public int MaxPlayers { get; set; }

        [DataMember]
        public int BoardId { get; set; }
    }

    [DataContract]
    public class LobbyCreationResultDTO
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string LobbyCode { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }
    }

    [DataContract]
    public class JoinLobbyResultDTO
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }

        [DataMember]
        public int BoardId { get; set; }
        [DataMember]
        public int MaxPlayers { get; set; }
        [DataMember]
        public bool IsHost { get; set; }

        [DataMember]
        public List<PlayerLobbyDTO> PlayersInLobby { get; set; }
    }

    [DataContract]
    public class PlayerLobbyDTO
    {
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public bool IsHost { get; set; }
    }

    [DataContract]
    public class LobbyStateDTO
    {
        [DataMember]
        public List<PlayerLobbyDTO> Players { get; set; }
        [DataMember]
        public bool IsGameStarted { get; set; }
    }
}
