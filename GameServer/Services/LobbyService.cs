using GameServer.DTOs.Lobby;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using GameServer.Helpers;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyService : ILobbyService, IDisposable
    {
        private readonly ILobbyRepository _repository;
        private readonly LobbyAppService _logic;

        public LobbyService()
        {
            _repository = new LobbyRepository();
            _logic = new LobbyAppService(_repository);
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            var result = await _logic.CreateLobbyAsync(request);

            if (result.Success)
            {
                var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
                if (callback != null)
                {
                    ConnectionManager.RegisterLobbyClient(request.HostUsername, callback);
                }
            }

            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            var result = await _logic.JoinLobbyAsync(request);

            if (result.Success)
            {
                var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
                if (callback != null)
                {
                    ConnectionManager.RegisterLobbyClient(request.Username, callback);
                }
            }

            return result;
        }


        public Task<bool> StartGameAsync(string lobbyCode)
        {
            return _logic.StartGameAsync(lobbyCode);
        }

        public Task DisbandLobbyAsync(string hostUsername)
        {
            return _logic.DisbandLobbyAsync(hostUsername);
        }

        public Task<bool> LeaveLobbyAsync(string username)
        {
            return _logic.LeaveLobbyAsync(username);
        }

        public Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            return _logic.GetLobbyStateAsync(lobbyCode);
        }

        public Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            return _logic.GetPublicMatchesAsync();
        }

        public Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            return _logic.KickPlayerAsync(request);
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}