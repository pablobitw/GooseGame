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
            RegisterClient(request?.Username);
            try { return await _logic.RollDiceAsync(request).ConfigureAwait(false); }
            catch (Exception ex) { Log.Error("RollDice", ex); return new DiceRollDto { Success = false }; }
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            RegisterClient(request?.Username);
            try { return await _logic.GetGameStateAsync(request).ConfigureAwait(false); }
            catch (Exception ex) { Log.Error("GetGameState", ex); return null; }
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            try
            {
                var result = await _logic.LeaveGameAsync(request).ConfigureAwait(false);
                if (result) ConnectionManager.UnregisterGameplayClient(request.Username);
                return result;
            }
            catch (Exception ex) { Log.Error("LeaveGame", ex); return false; }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            RegisterClient(request?.Username);
            try { await _logic.InitiateVoteKickAsync(request).ConfigureAwait(false); }
            catch (Exception ex) { Log.Error("InitiateVote", ex); }
        }

        public async Task CastVoteAsync(VoteResponseDto vote)
        {
            try { await _logic.CastVoteAsync(vote).ConfigureAwait(false); }
            catch (Exception ex) { Log.Error("CastVote", ex); }
        }

        private void RegisterClient(string username)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();
            if (callback != null && !string.IsNullOrEmpty(username))
            {
                ConnectionManager.RegisterGameplayClient(username, callback);
            }
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}