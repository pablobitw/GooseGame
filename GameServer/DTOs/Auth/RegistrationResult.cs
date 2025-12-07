using System.Runtime.Serialization;

namespace GameServer.DTOs.Auth
{
    [DataContract]
    public enum RegistrationResult
    {
        [EnumMember]
        Success,
        [EnumMember]
        UsernameAlreadyExists,
        [EnumMember]
        EmailAlreadyExists,
        [EnumMember]
        EmailPendingVerification,
        [EnumMember]
        FatalError
    }
}