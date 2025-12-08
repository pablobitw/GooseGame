using GameServer.DTOs.Gameplay;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using log4net;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameplayService : IGameplayService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayService));

        public async Task<DiceRollDTO> RollDiceAsync(GameplayRequest request)
        {
            using (var repository = new GameplayRepository())
            {
                var logic = new GameplayAppService(repository);
                return await logic.RollDiceAsync(request);
            }
        }

        public async Task<GameStateDTO> GetGameStateAsync(GameplayRequest request)
        {
            using (var repository = new GameplayRepository())
            {
                try
                {
                    var logic = new GameplayAppService(repository);
                    return await logic.GetGameStateAsync(request);
                }
                catch (Exception ex)
                {
                    // Log de emergencia para ver si explota aquí
                    Log.Fatal($"CRASH PREVENIDO en GetGameState para {request?.Username}: {ex.Message}", ex);
                    throw new FaultException("Error interno del servidor al obtener estado.");
                }
            }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            using (var repository = new GameplayRepository())
            {
                var logic = new GameplayAppService(repository);
                return await logic.LeaveGameAsync(request);
            }
        }
    }
}