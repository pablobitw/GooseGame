using GameServer.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Services.Common
{
    public interface IChatSessionManager
    {
        void RegisterClient(string username, IChatCallback callback);
        void UnregisterClient(string username);
        IChatCallback GetClientCallback(string username);

        void AddUserToLobby(string lobbyCode, string username);
        void RemoveUserFromLobby(string lobbyCode, string username);
        List<string> GetLobbyParticipants(string lobbyCode);
    }

    public class ChatSessionManager : IChatSessionManager
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _lobbies
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        private static readonly ConcurrentDictionary<string, IChatCallback> _callbacks
            = new ConcurrentDictionary<string, IChatCallback>();

        public void RegisterClient(string username, IChatCallback callback)
        {
            if (string.IsNullOrEmpty(username) || callback == null) return;
            _callbacks.AddOrUpdate(username, callback, (k, v) => callback);
        }

        public void UnregisterClient(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            _callbacks.TryRemove(username, out _);
        }

        public IChatCallback GetClientCallback(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            _callbacks.TryGetValue(username, out var callback);
            return callback;
        }

        public void AddUserToLobby(string lobbyCode, string username)
        {
            if (string.IsNullOrEmpty(lobbyCode) || string.IsNullOrEmpty(username)) return;
            var lobbyChatters = _lobbies.GetOrAdd(lobbyCode, _ => new ConcurrentDictionary<string, string>());
            lobbyChatters[username] = username;
        }

        public void RemoveUserFromLobby(string lobbyCode, string username)
        {
            if (!string.IsNullOrEmpty(lobbyCode) && _lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                lobbyChatters.TryRemove(username, out _);
            }
        }

        public List<string> GetLobbyParticipants(string lobbyCode)
        {
            if (!string.IsNullOrEmpty(lobbyCode) && _lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                return lobbyChatters.Keys.ToList();
            }
            return new List<string>();
        }
    }
}