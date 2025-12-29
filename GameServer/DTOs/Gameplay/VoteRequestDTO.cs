using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class VoteRequestDto
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string TargetUsername { get; set; }
        
        [DataMember] 
        public string Reason { get; set; }
    }
}
