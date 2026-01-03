using System.Runtime.Serialization;

namespace GameServer.DTOs.User
{
    [DataContract]
    public class DeactivateAccountRequest
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Password { get; set; }
    }
}