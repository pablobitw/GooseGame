using GameServer.DTOs.Gameplay;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IGameplayService
    {
        [OperationContract]
        Task<DiceRollDTO> RollDiceAsync(GameplayRequest request);

        [OperationContract]
        Task<GameStateDTO> GetGameStateAsync(GameplayRequest request);
    }
}