using GameServer.Helpers;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System;
using System.Threading.Tasks;

namespace GameServer.Services.Common
{
    public interface ISanctionServiceFactory
    {
        ILobbyServiceWrapper CreateLobbyService();
    }

    public class SanctionServiceFactory : ISanctionServiceFactory
    {
        public ILobbyServiceWrapper CreateLobbyService()
        {
            return new LobbyServiceWrapper();
        }
    }
}