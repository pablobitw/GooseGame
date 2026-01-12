using GameServer.DTOs.Gameplay;
using GameServer.Faults;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IGameplayServiceCallback))]
    public interface IGameplayService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<DiceRollDto> RollDiceAsync(GameplayRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<GameStateDto> GetGameStateAsync(GameplayRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<bool> LeaveGameAsync(GameplayRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task InitiateVoteKickAsync(VoteRequestDto request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task CastVoteAsync(VoteResponseDto vote);
    }

    [ServiceContract]
    public interface IGameplayServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnVoteKickStarted(string targetUsername, string reason);

        [OperationContract(IsOneWay = true)]
        void OnPlayerKicked(string reason);

        [OperationContract(IsOneWay = true)]
        void OnTurnChanged(GameStateDto newState);

        [OperationContract(IsOneWay = true)]
        void OnGameFinished(string winner);
    }
}