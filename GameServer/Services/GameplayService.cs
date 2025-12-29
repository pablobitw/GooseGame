using GameServer.DTOs.Gameplay;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using log4net;
using System;
using System.Data.Entity.Core;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameplayService : IGameplayService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayService));

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            using (var repository = new GameplayRepository())
            {
                var logic = new GameplayAppService(repository);
                return await logic.RollDiceAsync(request).ConfigureAwait(false);
            }
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            if (callback != null && !string.IsNullOrEmpty(request?.Username))
            {
                ConnectionManager.RegisterGameplayClient(request.Username, callback);
            }

            using (var repository = new GameplayRepository())
            {
                try
                {
                    var logic = new GameplayAppService(repository);
                    return await logic.GetGameStateAsync(request).ConfigureAwait(false);
                }
                catch (EntityException ex)
                {
                    Log.ErrorFormat("Error de base de datos al obtener estado de juego para {0}", request?.Username);
                    Log.Error("Detalle de la excepción:", ex);
                    throw new FaultException("Ocurrió un error de base de datos.");
                }
                catch (TimeoutException ex)
                {
                    Log.ErrorFormat("Timeout al obtener estado de juego para {0}", request?.Username);
                    Log.Error("Detalle de la excepción:", ex);
                    throw new FaultException("La operación excedió el tiempo de espera.");
                }
                catch (Exception ex)
                {
                    Log.FatalFormat("CRASH PREVENIDO en GetGameState para {0}: {1}", request?.Username, ex.Message);
                    Log.Fatal("Detalle de la excepción:", ex);
                    throw new FaultException("Error interno al obtener el estado del juego.");
                }
            }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            using (var repository = new GameplayRepository())
            {
                var logic = new GameplayAppService(repository);
                return await logic.LeaveGameAsync(request).ConfigureAwait(false);
            }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            using (var repository = new GameplayRepository())
            {
                try
                {
                    var logic = new GameplayAppService(repository);
                    await logic.InitiateVoteKickAsync(request).ConfigureAwait(false);
                }
                catch (EntityException ex)
                {
                    Log.ErrorFormat("Error de base de datos al iniciar votación de expulsión por {0}", request?.Username);
                    Log.Error("Detalle de la excepción:", ex);
                    throw new FaultException("No se pudo acceder a los datos para iniciar la votación.");
                }
                catch (InvalidOperationException ex)
                {
                    Log.WarnFormat("Intento de voto inválido por {0}: {1}", request?.Username, ex.Message);
                    Log.Warn("Detalle de la excepción:", ex);
                    throw new FaultException(ex.Message);
                }
            }
        }

        public async Task CastVoteAsync(VoteResponseDto vote)
        {
            using (var repository = new GameplayRepository())
            {
                try
                {
                    var logic = new GameplayAppService(repository);
                    await logic.CastVoteAsync(vote).ConfigureAwait(false);
                }
                catch (EntityException ex)
                {
                    Log.ErrorFormat("Error de base de datos al emitir voto por {0}", vote?.Username);
                    Log.Error("Detalle de la excepción:", ex);
                    throw new FaultException("No se pudo registrar el voto.");
                }
                catch (TimeoutException ex)
                {
                    Log.ErrorFormat("Timeout al emitir voto por {0}", vote?.Username);
                    Log.Error("Detalle de la excepción:", ex);
                    throw new FaultException("El envío del voto excedió el tiempo de espera.");
                }
            }
        }
    }
}
