using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class DiceRollDto
    {
        [DataMember]
        public int DiceOne { get; set; }
        [DataMember]
        public int DiceTwo { get; set; }
        [DataMember]
        public int Total { get; set; }
    }
}