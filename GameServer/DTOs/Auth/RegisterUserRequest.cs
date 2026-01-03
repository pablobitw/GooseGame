using System.Runtime.Serialization;

namespace GameServer.DTOs.Auth
{
    [DataContract]
    public class RegisterUserRequest
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string Password { get; set; }

        [DataMember]
        public string PreferredLanguage { get; set; }
    }
}