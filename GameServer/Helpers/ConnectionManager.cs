using System;
using System.Collections.Generic;
using System.Linq; 
using System.ServiceModel;
using GameServer.Interfaces;

namespace GameServer.Helpers
{
    public static class ConnectionManager
    {
        private static readonly HashSet<string> _activeUsers =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _locker = new object();

        private static readonly Dictionary<string, ILobbyServiceCallback> _lobbyCallbacks =
            new Dictionary<string, ILobbyServiceCallback>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, IGameplayServiceCallback> _gameplayCallbacks =
            new Dictionary<string, IGameplayServiceCallback>(StringComparer.OrdinalIgnoreCase);

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
                _activeUsers.Remove(username);
                _lobbyCallbacks.Remove(username);
                _gameplayCallbacks.Remove(username);
            }
        }

        public static bool IsUserOnline(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            lock (_locker)
            {
                if (!_activeUsers.Contains(username)) return false;

                if (!HasAliveCallbackUnsafe(_gameplayCallbacks, username) &&
                    !HasAliveCallbackUnsafe(_lobbyCallbacks, username))
                {
                    _activeUsers.Remove(username);
                    return false;
                }

                return true;
            }
        }

        public static void RegisterLobbyClient(string username, ILobbyServiceCallback callback)
        {
            if (string.IsNullOrWhiteSpace(username) || callback == null) return;

            lock (_locker)
            {
                _activeUsers.Add(username);
                _lobbyCallbacks[username] = callback;
                RemoveIfDeadUnsafe(_lobbyCallbacks, username);
            }
        }

        public static void UnregisterLobbyClient(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            lock (_locker)
            {
                _lobbyCallbacks.Remove(username);
                CheckAndRemoveIfOrphan(username);
            }
        }

        public static ILobbyServiceCallback GetLobbyClient(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;

            lock (_locker)
            {
                if (!_lobbyCallbacks.TryGetValue(username, out ILobbyServiceCallback cb))
                {
                    return null;
                }

                if (!IsCallbackAlive(cb))
                {
                    _lobbyCallbacks.Remove(username);
                    CheckAndRemoveIfOrphan(username);
                    return null;
                }

                return cb;
            }
        }

        public static void RegisterGameplayClient(string username, IGameplayServiceCallback callback)
        {
            if (string.IsNullOrWhiteSpace(username) || callback == null) return;

            lock (_locker)
            {
                _activeUsers.Add(username);
                _gameplayCallbacks[username] = callback;
                RemoveIfDeadUnsafe(_gameplayCallbacks, username);
            }
        }

        public static void UnregisterGameplayClient(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            lock (_locker)
            {
                _gameplayCallbacks.Remove(username);
                CheckAndRemoveIfOrphan(username);
            }
        }

        public static IGameplayServiceCallback GetGameplayClient(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;

            lock (_locker)
            {
                if (!_gameplayCallbacks.TryGetValue(username, out IGameplayServiceCallback cb))
                {
                    return null;
                }

                if (!IsCallbackAlive(cb))
                {
                    _gameplayCallbacks.Remove(username);
                    CheckAndRemoveIfOrphan(username);
                    return null;
                }

                return cb;
            }
        }

        public static List<string> GetAllActiveGameplayUsers()
        {
            lock (_locker)
            {
                return _gameplayCallbacks.Keys.ToList();
            }
        }

        private static void CheckAndRemoveIfOrphan(string username)
        {
            if (!_lobbyCallbacks.ContainsKey(username) && !_gameplayCallbacks.ContainsKey(username))
            {
                _activeUsers.Remove(username);
            }
        }

        private static bool HasAliveCallbackUnsafe<T>(Dictionary<string, T> map, string username)
            where T : class
        {
            if (!map.TryGetValue(username, out T cb) || cb == null)
            {
                return false;
            }

            return IsCallbackAlive(cb);
        }

        private static void RemoveIfDeadUnsafe<T>(Dictionary<string, T> map, string username)
            where T : class
        {
            if (!map.TryGetValue(username, out T cb) || cb == null)
            {
                map.Remove(username);
                return;
            }

            if (!IsCallbackAlive(cb))
            {
                map.Remove(username);
            }
        }

        private static bool IsCallbackAlive(object callback)
        {
            if (callback is ICommunicationObject comm)
            {
                return comm.State != CommunicationState.Faulted &&
                       comm.State != CommunicationState.Closed &&
                       comm.State != CommunicationState.Closing;
            }

            return callback != null;
        }
    }
}