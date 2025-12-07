using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class GameplayRequest
    {
        [DataMember]
        public string LobbyCode { get; set; }

        [DataMember]
        public string Username { get; set; }
    }
}