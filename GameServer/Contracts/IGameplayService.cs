using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Contracts
{
    [ServiceContract]
    public interface IGameplayService
    {
        [OperationContract]
        Task<DiceRollDTO> RollDiceAsync(string lobbyCode, string username);

        [OperationContract]
        Task<GameStateDTO> GetGameStateAsync(string lobbyCode, string requestingUsername);
    }

    [DataContract]
    public class DiceRollDTO
    {
        [DataMember]
        public int DiceOne { get; set; }
        [DataMember]
        public int DiceTwo { get; set; }
        [DataMember]
        public int Total { get; set; }
    }

    [DataContract]
    public class GameStateDTO
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
    }
}