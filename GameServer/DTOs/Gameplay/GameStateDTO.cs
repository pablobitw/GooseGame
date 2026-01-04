using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class GameStateDto
    {
        [DataMember]
        public string CurrentTurnUsername { get; set; }
        [DataMember]
        public bool IsMyTurn { get; set; }
        [DataMember]
        public int LastDiceOne { get; set; }
        [DataMember]
        public int LastDiceTwo { get; set; }
        [DataMember]
        public List<string> GameLog { get; set; }
        [DataMember]
        public List<PlayerPositionDto> PlayerPositions { get; set; }
        [DataMember]
        public bool IsGameOver { get; set; }
        [DataMember]
        public string WinnerUsername { get; set; }

        [DataMember]
        public bool IsKicked { get; set; }

        [DataMember]
        public bool IsBanned { get; set; }
    }
}