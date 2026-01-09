using GameServer.Repositories;
using GameServer.Services.Logic;
using System;
using System.Threading.Tasks;

namespace GameServer.Helpers
{
    public interface ISanctionService : IDisposable
    {
        Task ProcessKickAsync(string username, string lobbyCode, string reason, string source);
    }

    public interface ILobbyServiceWrapper : IDisposable
    {
        Task SystemKickPlayerAsync(string lobbyCode, string username, string reason);
    }

    public interface IChatServiceFactory
    {
        ISanctionService CreateSanctionService();
        ILobbyServiceWrapper CreateLobbyService();
    }


    public class ChatServiceFactory : IChatServiceFactory
    {
        public ISanctionService CreateSanctionService()
        {
            return new SanctionServiceWrapper();
        }

        public ILobbyServiceWrapper CreateLobbyService()
        {
            return new LobbyServiceWrapper();
        }
    }

    public class SanctionServiceWrapper : ISanctionService
    {
        private readonly SanctionAppService _service;

        public SanctionServiceWrapper()
        {
            _service = new SanctionAppService();
        }

        public Task ProcessKickAsync(string username, string lobbyCode, string reason, string source)
        {
            return _service.ProcessKickAsync(username, lobbyCode, reason, source);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }

    public class LobbyServiceWrapper : ILobbyServiceWrapper
    {
        private readonly LobbyRepository _repo;
        private readonly LobbyAppService _service;

        public LobbyServiceWrapper()
        {
  
            _repo = new LobbyRepository();
            _service = new LobbyAppService(_repo);
        }

        public Task SystemKickPlayerAsync(string lobbyCode, string username, string reason)
        {
            return _service.SystemKickPlayerAsync(lobbyCode, username, reason);
        }

        public void Dispose()
        {
 
            _repo?.Dispose();
        }
    }
}