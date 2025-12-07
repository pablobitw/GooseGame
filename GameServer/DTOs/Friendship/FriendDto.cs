using System.Runtime.Serialization;

namespace GameServer.DTOs.Friendship
{
    [DataContract]
    public class FriendDto
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string AvatarPath { get; set; }

        [DataMember]
        public bool IsOnline { get; set; }
    }
}