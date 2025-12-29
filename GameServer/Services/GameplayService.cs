using GameServer.DTOs.Gameplay;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using log4net;
using System;
using System.Data.Entity.Core;
using System.Data.SqlClient;
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
                return await logic.RollDiceAsync(request);
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
                    return await logic.GetGameStateAsync(request);
                }
                catch (EntityException ex)
                {
                    Log.Error($"Database error getting game state for {request?.Username}", ex);
                    throw new FaultException("Database error occurred.");
                }
                catch (TimeoutException ex)
                {
                    Log.Error($"Timeout getting game state for {request?.Username}", ex);
                    throw new FaultException("The operation timed out.");
                }
                catch (Exception ex)
                {
                    Log.Fatal($"CRASH PREVENTED in GetGameState for {request?.Username}: {ex.Message}", ex);
                    throw new FaultException("Internal server error fetching game state.");
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


        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            using (var repository = new GameplayRepository())
            {
                try
                {
                    var logic = new GameplayAppService(repository);
                    await logic.InitiateVoteKickAsync(request);
                }
                catch (EntityException ex)
                {
                    Log.Error($"Database error initiating vote kick by {request?.Username}", ex);
                    throw new FaultException("Unable to access data to initiate vote.");
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warn($"Invalid vote attempt by {request?.Username}: {ex.Message}");
                    throw new FaultException(ex.Message); 
                }
            }
        }

        public async Task CastVoteAsync(VoteResponseDto request)
        {
            using (var repository = new GameplayRepository())
            {
                try
                {
                    var logic = new GameplayAppService(repository);
                    await logic.CastVoteAsync(request);
                }
                catch (EntityException ex)
                {
                    Log.Error($"Database error casting vote by {request?.Username}", ex);
                    throw new FaultException("Unable to submit vote.");
                }
                catch (TimeoutException ex)
                {
                    Log.Error($"Timeout casting vote by {request?.Username}", ex);
                    throw new FaultException("Vote submission timed out.");
                }
            }
        }
    }
}