using System.Runtime.Serialization;

namespace GameServer.DTOs.User
{
    [DataContract]
    public enum UsernameChangeResult
    {
        [EnumMember]
        Success,
        [EnumMember]
        UsernameAlreadyExists,
        [EnumMember]
        LimitReached,
        [EnumMember]
        CooldownActive,
        [EnumMember]
        UserNotFound,
        [EnumMember]
        FatalError,
        [EnumMember]
        IncorrectPassword
    }
}