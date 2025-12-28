using GameServer.DTOs.Gameplay;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IGameplayServiceCallback))]
    public interface IGameplayService
    {
        [OperationContract]
        Task<DiceRollDTO> RollDiceAsync(GameplayRequest request);

        [OperationContract]
        Task<GameStateDTO> GetGameStateAsync(GameplayRequest request);

        [OperationContract]
        Task<bool> LeaveGameAsync(GameplayRequest request);

        [OperationContract]
        Task InitiateVoteKickAsync(VoteRequestDTO request);

        [OperationContract]
        Task CastVoteAsync(VoteResponseDTO vote);
    }

    [ServiceContract]
    public interface IGameplayServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnVoteKickStarted(string targetUsername, string reason);

        [OperationContract(IsOneWay = true)]
        void OnPlayerKicked(string reason);
    }
}