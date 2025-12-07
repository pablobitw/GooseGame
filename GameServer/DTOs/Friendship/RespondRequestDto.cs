using System.Runtime.Serialization;

namespace GameServer.DTOs.Friendship
{
    [DataContract]
    public class RespondRequestDto
    {
        [DataMember]
        public string RespondingUsername { get; set; }

        [DataMember]
        public string RequesterUsername { get; set; }

        [DataMember]
        public bool IsAccepted { get; set; }
    }
}