using System.Runtime.Serialization;

namespace GameServer.DTOs.Chat
{
    [DataContract]
    public class ChatMessageDto
    {
        [DataMember]
        public string Sender { get; set; }

        [DataMember]
        public string LobbyCode { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}