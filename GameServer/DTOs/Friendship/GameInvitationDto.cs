using System.Runtime.Serialization;

namespace GameServer.DTOs.Friendship
{
    [DataContract]
    public class GameInvitationDto
    {
        [DataMember]
        public string SenderUsername { get; set; }

        [DataMember]
        public string TargetUsername { get; set; }

        [DataMember]
        public string LobbyCode { get; set; }
    }
}