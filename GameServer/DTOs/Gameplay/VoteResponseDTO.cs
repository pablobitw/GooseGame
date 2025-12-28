using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class VoteResponseDTO
    {
        [DataMember]
        public string Username { get; set; } 

        [DataMember]
        public bool AcceptKick { get; set; }
    }
}