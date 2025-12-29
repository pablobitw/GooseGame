using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class PlayerLobbyDto
    {
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public bool IsHost { get; set; }
    }
}