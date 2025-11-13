using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Contracts
{
    [ServiceContract]
    public interface ILobbyService
    {
        [OperationContract]
        Task<LobbyCreationResultDTO> CreateLobbyAsync(LobbySettingsDTO settings, string hostUsername);

        [OperationContract]
        Task<bool> StartGameAsync(string lobbyCode);
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
}