using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
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
        public bool IsPublic { get; set; }
        [DataMember]
        public List<PlayerLobbyDTO> PlayersInLobby { get; set; }
    }
}