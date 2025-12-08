using GameServer.DTOs.Lobby;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class LobbyService : ILobbyService
    {
        private readonly LobbyAppService _logic;

        public LobbyService()
        {
            var repository = new LobbyRepository();
            _logic = new LobbyAppService(repository);
        }

        public async Task<LobbyCreationResultDTO> CreateLobbyAsync(CreateLobbyRequest request)
        {
            LobbyCreationResultDTO result;
            result = await _logic.CreateLobbyAsync(request);
            return result;
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            bool result;
            result = await _logic.StartGameAsync(lobbyCode);
            return result;
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            await _logic.DisbandLobbyAsync(hostUsername);
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            bool result;
            result = await _logic.LeaveLobbyAsync(username);
            return result;
        }

        public async Task<JoinLobbyResultDTO> JoinLobbyAsync(JoinLobbyRequest request)
        {
            JoinLobbyResultDTO result;
            result = await _logic.JoinLobbyAsync(request);
            return result;
        }

        public async Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode)
        {
            LobbyStateDTO result;
            result = await _logic.GetLobbyStateAsync(lobbyCode);
            return result;
        }
        public async Task<ActiveMatchDTO[]> GetPublicMatchesAsync()
        {
            return await _logic.GetPublicMatchesAsync();
        }
    }
}