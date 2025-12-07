using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class LobbyStateDTO
    {
        [DataMember]
        public List<PlayerLobbyDTO> Players { get; set; }
        [DataMember]
        public bool IsGameStarted { get; set; }
        [DataMember]
        public int BoardId { get; set; }
        [DataMember]
        public int MaxPlayers { get; set; }
        [DataMember]
        public bool IsPublic { get; set; }
    }
}