using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class ActiveMatchDto
    {
        [DataMember]
        public string LobbyCode { get; set; }

        [DataMember]
        public string HostUsername { get; set; }

        [DataMember]
        public int BoardId { get; set; }

        [DataMember]
        public int CurrentPlayers { get; set; }

        [DataMember]
        public int MaxPlayers { get; set; }
    }
}