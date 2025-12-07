using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class JoinLobbyRequest
    {
        [DataMember]
        public string LobbyCode { get; set; }
        [DataMember]
        public string Username { get; set; }
    }
}