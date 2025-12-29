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
    public class GameplayService : IGameplayService, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayService));
        private readonly GameplayRepository _repository;
        private readonly GameplayAppService _logic;

        public GameplayService()
        {
            _repository = new GameplayRepository();
            _logic = new GameplayAppService(_repository);
        }

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            // CORRECCIÓN: Capturar callback ANTES del await
            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();
            if (callback != null && request != null)
            {
                ConnectionManager.RegisterGameplayClient(request.Username, callback);
            }

            return await _logic.RollDiceAsync(request).ConfigureAwait(false);
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            // CORRECCIÓN: Capturar callback ANTES del await
            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();
            if (callback != null && request != null)
            {
                ConnectionManager.RegisterGameplayClient(request.Username, callback);
            }

            try
            {
                return await _logic.GetGameStateAsync(request).ConfigureAwait(false);
            }
            catch (EntityException ex)
            {
                Log.ErrorFormat("Error de base de datos al obtener estado de juego para {0}: {1}", request?.Username, ex.Message);
                throw new FaultException("Ocurrió un error de base de datos.");
            }
            catch (TimeoutException ex)
            {
                Log.ErrorFormat("Timeout al obtener estado de juego para {0}: {1}", request?.Username, ex.Message);
                throw new FaultException("La operación excedió el tiempo de espera.");
            }
            catch (Exception ex)
            {
                Log.FatalFormat("Error crítico en GetGameState para {0}: {1}", request?.Username, ex.Message);
                throw new FaultException("Error interno al obtener el estado del juego.");
            }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            var result = await _logic.LeaveGameAsync(request).ConfigureAwait(false);
            if (result)
            {
                ConnectionManager.UnregisterGameplayClient(request.Username);
            }
            return result;
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            // CORRECCIÓN: Capturar callback para asegurar registro si no estaba
            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();
            if (callback != null && request != null)
            {
                ConnectionManager.RegisterGameplayClient(request.Username, callback);
            }

            try
            {
                await _logic.InitiateVoteKickAsync(request).ConfigureAwait(false);
            }
            catch (EntityException ex)
            {
                Log.ErrorFormat("Error DB voto kick {0}: {1}", request?.Username, ex.Message);
                throw new FaultException("No se pudo iniciar la votación.");
            }
            catch (InvalidOperationException ex)
            {
                Log.WarnFormat("Voto inválido {0}: {1}", request?.Username, ex.Message);
                throw new FaultException(ex.Message);
            }
        }

        public async Task CastVoteAsync(VoteResponseDto vote)
        {
            try
            {
                await _logic.CastVoteAsync(vote).ConfigureAwait(false);
            }
            catch (EntityException ex)
            {
                Log.ErrorFormat("Error DB cast vote {0}: {1}", vote?.Username, ex.Message);
                throw new FaultException("No se pudo registrar el voto.");
            }
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}