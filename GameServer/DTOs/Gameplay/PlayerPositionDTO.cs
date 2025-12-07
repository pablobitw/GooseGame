using System.Runtime.Serialization;

namespace GameServer.DTOs.Gameplay
{
    [DataContract]
    public class PlayerPositionDTO
    {
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public int CurrentTile { get; set; }
        [DataMember]
        public bool IsOnline { get; set; }
        [DataMember]
        public string AvatarPath { get; set; }
        [DataMember]
        public bool IsMyTurn { get; set; }
    }
}