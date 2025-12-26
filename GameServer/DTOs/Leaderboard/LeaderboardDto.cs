using System.Runtime.Serialization;

namespace GameServer.DTOs
{
    [DataContract]
    public class LeaderboardDto
    {
        [DataMember]
        public int Rank { get; set; }

        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string AvatarPath { get; set; }

        [DataMember]
        public int Wins { get; set; }

        [DataMember]
        public bool IsCurrentUser { get; set; }
    }
}