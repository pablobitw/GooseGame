using System.Runtime.Serialization;

namespace GameServer.DTOs.Friendship
{
    [DataContract]
    public enum FriendRequestResult
    {
        [EnumMember]
        Success,            

        [EnumMember]
        AlreadyFriends,     

        [EnumMember]
        Pending,            

        [EnumMember]
        TargetNotFound,     

        [EnumMember]
        GuestRestriction,
        [EnumMember]
        DatabaseError,
        [EnumMember]
        TimeOutError,

        [EnumMember]
        Error               
    }
}