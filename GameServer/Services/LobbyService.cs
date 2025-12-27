using GameServer.DTOs.Lobby;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using GameServer.Helpers;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
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
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                return await logic.CreateLobbyAsync(request);
            }
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                return await logic.StartGameAsync(lobbyCode);
            }
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                await logic.DisbandLobbyAsync(hostUsername);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                return await logic.LeaveLobbyAsync(username);
            }
        }

        public async Task<JoinLobbyResultDTO> JoinLobbyAsync(JoinLobbyRequest request)
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);

                var result = await logic.JoinLobbyAsync(request);

                if (result.Success)
                {
                    var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
                    ConnectionManager.RegisterLobbyClient(request.Username, callback);
                }

                return result;
            }
        }

        public async Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode)
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                return await logic.GetLobbyStateAsync(lobbyCode);
            }
        }

        public async Task<ActiveMatchDTO[]> GetPublicMatchesAsync()
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                return await logic.GetPublicMatchesAsync();
            }
        }

        public async Task KickPlayerAsync(KickPlayerRequest request)
        {
            using (var repo = new LobbyRepository())
            {
                var logic = new LobbyAppService(repo);
                await logic.KickPlayerAsync(request);
            }
        }
    }
}