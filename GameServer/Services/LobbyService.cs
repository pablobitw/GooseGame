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
        private readonly LobbyRepository _repository;
        private readonly LobbyAppService _logic;

        public LobbyService()
        {
            _repository = new LobbyRepository();
            _logic = new LobbyAppService(_repository);
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();

            var result = await _logic.CreateLobbyAsync(request);

            if (result.Success && callback != null)
            {
                ConnectionManager.RegisterLobbyClient(request.HostUsername, callback);
            }

            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();

            var result = await _logic.JoinLobbyAsync(request);

            if (result.Success && callback != null)
            {
                ConnectionManager.RegisterLobbyClient(request.Username, callback);
            }

            return result;
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            return await _logic.StartGameAsync(lobbyCode);
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            await _logic.DisbandLobbyAsync(hostUsername);
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            return await _logic.LeaveLobbyAsync(username);
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            return await _logic.GetLobbyStateAsync(lobbyCode);
        }

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            return await _logic.GetPublicMatchesAsync();
        }

        public async Task KickPlayerAsync(KickPlayerRequest request)
        {
            await _logic.KickPlayerAsync(request);
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}