using GameServer.DTOs.Gameplay;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class GameplayService : IGameplayService
    {
        private readonly GameplayAppService _logic;

        public GameplayService()
        {
            var repository = new GameplayRepository();
            _logic = new GameplayAppService(repository);
        }

        public async Task<DiceRollDTO> RollDiceAsync(GameplayRequest request)
        {
            DiceRollDTO result;
            result = await _logic.RollDiceAsync(request);
            return result;
        }

        public async Task<GameStateDTO> GetGameStateAsync(GameplayRequest request)
        {
            GameStateDTO result;
            result = await _logic.GetGameStateAsync(request);
            return result;
        }
    }
}