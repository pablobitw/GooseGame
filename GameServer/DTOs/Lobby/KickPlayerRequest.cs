using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class KickPlayerRequest
    {
        [DataMember]
        public string LobbyCode { get; set; }

        [DataMember]
        public string TargetUsername { get; set; }

        [DataMember]
        public string RequestorUsername { get; set; }
    }
}