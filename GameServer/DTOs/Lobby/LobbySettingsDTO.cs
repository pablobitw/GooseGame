using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class LobbySettingsDto
    {
        [DataMember]
        public bool IsPublic { get; set; }
        [DataMember]
        public int MaxPlayers { get; set; }
        [DataMember]
        public int BoardId { get; set; }
    }
}