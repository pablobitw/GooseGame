using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class LobbyCreationResultDto
    {
        [DataMember]
        public bool Success { get; set; }
        [DataMember]
        public string LobbyCode { get; set; }
        [DataMember]
        public string ErrorMessage { get; set; }
    }
}