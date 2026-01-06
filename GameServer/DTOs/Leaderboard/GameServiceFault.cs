using System.Runtime.Serialization;

namespace GameServer.DTOs
{
    [DataContract]
    public enum GameServiceErrorType
    {
        [EnumMember] DatabaseError,
        [EnumMember] OperationTimeout,
        [EnumMember] UnknownError,
        [EnumMember] EmptyData
    }

    [DataContract]
    public class GameServiceFault
    {
        [DataMember]
        public GameServiceErrorType ErrorType { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string Details { get; set; } 
    }
}