using System.Runtime.Serialization;

namespace GameServer.DTOs.Chat
{
    [DataContract(Name = "ChatOperationResult")]
    public enum ChatOperationResult
    {
        [EnumMember] Success,
        [EnumMember] SpamBlocked,
        [EnumMember] ContentBlocked,
        [EnumMember] TargetNotFound,
        [EnumMember] LobbyNotFound,
        [EnumMember] MessageTooLong,
        [EnumMember] InternalError,
        [EnumMember] GeneralError 
    }
}