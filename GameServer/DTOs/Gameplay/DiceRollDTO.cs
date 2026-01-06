using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class DiceRollDto
    {
        [DataMember]
        public bool Success { get; set; } = true; 

        [DataMember]
        public GameplayErrorType ErrorType { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }

        [DataMember]
        public int DiceOne { get; set; }
        [DataMember]
        public int DiceTwo { get; set; }
        [DataMember]
        public int Total { get; set; }
    }
}