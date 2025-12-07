using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
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