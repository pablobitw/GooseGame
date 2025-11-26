using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Managers
{
    /// <summary>
    /// Singleton thread-safe para gestionar sesiones activas.
    /// </summary>
    public static class ConnectionManager
    {
        private static readonly HashSet<string> _activeUsers = new HashSet<string>();
        private static readonly object _locker = new object();

        public static bool AddUser(string username)
        {
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
            lock (_locker)
            {
                if (_activeUsers.Contains(username))
                {
                    _activeUsers.Remove(username);
                }
            }
        }

        public static bool IsUserOnline(string username)
        {
            lock (_locker)
            {
                return _activeUsers.Contains(username);
            }
        }
    }
}