using System.Runtime.Serialization;

namespace GameServer.DTOs.User
{
    [DataContract]
    public class ChangePasswordRequest
    {
        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public string NewPassword { get; set; }
    }
}