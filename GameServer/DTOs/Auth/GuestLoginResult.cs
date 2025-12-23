using System.Runtime.Serialization;

namespace GameServer.DTOs.Auth
{
    [DataContract]
    public class GuestLoginResult
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}