using GameServer.DTOs.Gameplay;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IGameplayServiceCallback))]
    public interface IGameplayService
    {
        [OperationContract]
        Task<DiceRollDto> RollDiceAsync(GameplayRequest request);

        [OperationContract]
        Task<GameStateDto> GetGameStateAsync(GameplayRequest request);

        [OperationContract]
        Task<bool> LeaveGameAsync(GameplayRequest request);

        [OperationContract]
        Task InitiateVoteKickAsync(VoteRequestDto request);

        [OperationContract]
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