using System.Runtime.Serialization;

namespace GameServer.DTOs.Chat
{
    [DataContract]
    public class JoinChatRequest
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string LobbyCode { get; set; }
    }
}