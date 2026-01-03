using System.Runtime.Serialization;

namespace GameServer.DTOs.User
{
    [DataContract]
    public class UserProfileDto
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string AvatarPath { get; set; }

        [DataMember]
        public int Coins { get; set; }

        [DataMember]
        public int MatchesPlayed { get; set; }

        [DataMember]
        public int MatchesWon { get; set; }

        [DataMember]
        public int UsernameChangeCount { get; set; }

        [DataMember]
        public string PreferredLanguage { get; set; }
    }
}