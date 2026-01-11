using GameServer.Helpers;
using GameServer.Interfaces;

namespace GameServer.Services.Common
{
    public interface IGameplayConnectionManager
    {
        IGameplayServiceCallback GetGameplayClient(string username);
        void UnregisterGameplayClient(string username);
    }

    public class GameplayConnectionManagerWrapper : IGameplayConnectionManager
    {
        public IGameplayServiceCallback GetGameplayClient(string username)
        {
            return ConnectionManager.GetGameplayClient(username);
        }

        public void UnregisterGameplayClient(string username)
        {
            ConnectionManager.UnregisterGameplayClient(username);
        }
    }
}