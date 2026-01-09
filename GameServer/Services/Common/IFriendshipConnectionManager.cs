using GameServer.Interfaces;
using System.Collections.Generic;

namespace GameServer.Helpers

{
    public interface IFriendshipConnectionManager
    {
        void AddClient(string username, IFriendshipServiceCallback callback);
        void RemoveClient(string username);
        IFriendshipServiceCallback GetClient(string username);
        bool IsClientConnected(string username);
    }

    public class FriendshipConnectionManager : IFriendshipConnectionManager
    {
        private static readonly Dictionary<string, IFriendshipServiceCallback> _clients = new Dictionary<string, IFriendshipServiceCallback>();
        private static readonly object _locker = new object();

        public void AddClient(string username, IFriendshipServiceCallback callback)
        {
            lock (_locker)
            {
                string key = username.ToLower();
                if (_clients.ContainsKey(key))
                    _clients[key] = callback;
                else
                    _clients.Add(key, callback);
            }
        }

        public void RemoveClient(string username)
        {
            lock (_locker)
            {
                string key = username.ToLower();
                if (_clients.ContainsKey(key))
                    _clients.Remove(key);
            }
        }

        public IFriendshipServiceCallback GetClient(string username)
        {
            lock (_locker)
            {
                string key = username.ToLower();
                return _clients.ContainsKey(key) ? _clients[key] : null;
            }
        }

        public bool IsClientConnected(string username)
        {
            lock (_locker)
            {
                return _clients.ContainsKey(username.ToLower());
            }
        }
    }
}