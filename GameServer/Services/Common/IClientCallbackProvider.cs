using GameServer.Interfaces;
using System.ServiceModel;

namespace GameServer.Helpers

{
    public interface IClientCallbackProvider
    {
        IFriendshipServiceCallback GetCallback();
    }

    public class ClientCallbackProvider : IClientCallbackProvider
    {
        public IFriendshipServiceCallback GetCallback()
        {
            return OperationContext.Current?.GetCallbackChannel<IFriendshipServiceCallback>();
        }
    }
}