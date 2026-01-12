using GameServer.DTOs.Gameplay;
using GameServer.Faults;
using GameServer.Helpers;
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
    public class GameplayService : IGameplayService, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayService));
        private readonly IGameplayRepository _repository;
        private readonly GameplayAppService _logic;

        public GameplayService() : this(new GameplayRepository()) { }

        public GameplayService(IGameplayRepository repository)
        {
            _repository = repository;
            _logic = new GameplayAppService(_repository);
        }

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            try
            {
                RegisterClientSafe(request?.Username);
                return await _logic.RollDiceAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            try
            {
                RegisterClientSafe(request?.Username);
                return await _logic.GetGameStateAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            try
            {
                var result = await _logic.LeaveGameAsync(request).ConfigureAwait(false);

                if (result)
                {
                    try
                    {
                        ConnectionManager.UnregisterGameplayClient(request.Username);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Error unregistering client {request.Username}", ex);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            try
            {
                RegisterClientSafe(request?.Username);
                await _logic.InitiateVoteKickAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task CastVoteAsync(VoteResponseDto vote)
        {
            try
            {
                await _logic.CastVoteAsync(vote).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw ExceptionManager.Map(ex);
            }
        }

        private void RegisterClientSafe(string username)
        {
            try
            {
                var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();
                if (callback != null && !string.IsNullOrEmpty(username))
                {
                    ConnectionManager.RegisterGameplayClient(username, callback);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to register gameplay callback for {username}", ex);
            }
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}