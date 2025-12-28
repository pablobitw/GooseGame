using System;
using System.Collections.Generic;
using GameServer.Interfaces;

namespace GameServer.Helpers
{
    public static class ConnectionManager
    {
        private static readonly HashSet<string> _activeUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _locker = new object();

        private static readonly Dictionary<string, ILobbyServiceCallback> _lobbyCallbacks = new Dictionary<string, ILobbyServiceCallback>();
        private static readonly Dictionary<string, IGameplayServiceCallback> _gameplayCallbacks = new Dictionary<string, IGameplayServiceCallback>();

        public static bool AddUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            lock (_locker)
            {
                if (_activeUsers.Contains(username))
                {
                    return false;
                }

                _activeUsers.Add(username);
                return true;
            }
        }

        public static void RemoveUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            lock (_locker)
            {
                if (_activeUsers.Contains(username))
                {
                    _activeUsers.Remove(username);
                }

                if (_lobbyCallbacks.ContainsKey(username))
                {
                    _lobbyCallbacks.Remove(username);
                }

                if (_gameplayCallbacks.ContainsKey(username))
                {
                    _gameplayCallbacks.Remove(username);
                }
            }
        }

        public static bool IsUserOnline(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            lock (_locker)
            {
                return _activeUsers.Contains(username);
            }
        }

        public static void RegisterLobbyClient(string username, ILobbyServiceCallback callback)
        {
            lock (_locker)
            {
                if (!_lobbyCallbacks.ContainsKey(username))
                {
                    _lobbyCallbacks.Add(username, callback);
                }
                else
                {
                    _lobbyCallbacks[username] = callback;
                }
            }
        }

        public static void UnregisterLobbyClient(string username)
        {
            lock (_locker)
            {
                if (_lobbyCallbacks.ContainsKey(username))
                {
                    _lobbyCallbacks.Remove(username);
                }
            }
        }

        public static ILobbyServiceCallback GetLobbyClient(string username)
        {
            lock (_locker)
            {
                if (_lobbyCallbacks.ContainsKey(username))
                {
                    return _lobbyCallbacks[username];
                }
                return null;
            }
        }

        public static void RegisterGameplayClient(string username, IGameplayServiceCallback callback)
        {
            lock (_locker)
            {
                if (!_gameplayCallbacks.ContainsKey(username))
                {
                    _gameplayCallbacks.Add(username, callback);
                }
                else
                {
                    _gameplayCallbacks[username] = callback;
                }
            }
        }

        public static void UnregisterGameplayClient(string username)
        {
            lock (_locker)
            {
                if (_gameplayCallbacks.ContainsKey(username))
                {
                    _gameplayCallbacks.Remove(username);
                }
            }
        }

        public static IGameplayServiceCallback GetGameplayClient(string username)
        {
            lock (_locker)
            {
                if (_gameplayCallbacks.ContainsKey(username))
                {
                    return _gameplayCallbacks[username];
                }
                return null;
            }
        }
    }
}