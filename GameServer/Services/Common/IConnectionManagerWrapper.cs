namespace GameServer.Helpers
{
    public interface IConnectionManagerWrapper
    {
        void AddUser(string username);
        void RemoveUser(string username);
        bool IsUserOnline(string username);
    }

    public class ConnectionManagerWrapper : IConnectionManagerWrapper
    {
        public void AddUser(string u) => GameServer.Helpers.ConnectionManager.AddUser(u);
        public void RemoveUser(string u) => GameServer.Helpers.ConnectionManager.RemoveUser(u);
        public bool IsUserOnline(string u) => GameServer.Helpers.ConnectionManager.IsUserOnline(u);
    }
}