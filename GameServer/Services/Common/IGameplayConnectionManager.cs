using GameServer.Helpers;
using GameServer.Interfaces;

namespace GameServer.Services.Common
{
    public interface IGameplayConnectionManager
    {
        IGameplayServiceCallback GetClient(string username);
        void UnregisterClient(string username);
    }

    public class GameplayConnectionManagerWrapper : IGameplayConnectionManager
    {
        public IGameplayServiceCallback GetClient(string username)
        {
            return ConnectionManager.GetGameplayClient(username);
        }

        public void UnregisterClient(string username)
        {
            ConnectionManager.UnregisterGameplayClient(username);
        }
    }
}