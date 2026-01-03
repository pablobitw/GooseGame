using System.Runtime.Serialization;

namespace GameServer.DTOs.Auth
{
    [DataContract]
    public class LoginResponseDto
    {
        [DataMember]
        public bool IsSuccess { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string PreferredLanguage { get; set; } 
    }
}