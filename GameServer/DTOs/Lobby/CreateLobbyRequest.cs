using System.Runtime.Serialization;

namespace GameServer.DTOs.Lobby
{
    [DataContract]
    public class CreateLobbyRequest
    {
        [DataMember]
        public LobbySettingsDTO Settings { get; set; }
        [DataMember]
        public string HostUsername { get; set; }
    }
}