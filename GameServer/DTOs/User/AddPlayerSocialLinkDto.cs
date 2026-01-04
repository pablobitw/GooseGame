using System.Runtime.Serialization;

namespace GameServer.DTOs.User
{
    [DataContract]
    public class AddPlayerSocialLinkDto
    {
        [DataMember]
        public string SocialType { get; set; }

        [DataMember]
        public string Url { get; set; }
    }
}
