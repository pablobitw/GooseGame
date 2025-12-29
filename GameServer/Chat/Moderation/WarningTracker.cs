using System;
using System.Collections.Concurrent;

namespace GameServer.Chat.Moderation
{
    public static class WarningTracker
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _warnings
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();

        public static WarningLevel RegisterWarning(string lobbyCode, string username)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
                throw new ArgumentException("lobbyCode no debe ser null o espacio en blanco", nameof(lobbyCode));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("username no debe ser null o espacio en blanco", nameof(username));

            var lobby = _warnings.GetOrAdd(
                lobbyCode,
                _ => new ConcurrentDictionary<string, int>()
            );

            var count = lobby.AddOrUpdate(username, 1, (_, current) => current + 1);

            WarningLevel result;
            if (count == 1)
            {
                result = WarningLevel.Warning;
            }
            else if (count == 2)
            {
                result = WarningLevel.LastWarning;
            }
            else
            {
                result = WarningLevel.Punishment;
            }

            return result;
        }

        public static void Reset(string lobbyCode, string username)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
                throw new ArgumentException("lobbyCode no debe ser null o espacio en blanco", nameof(lobbyCode));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("username no debe ser null o espacio en blancoe", nameof(username));

            if (_warnings.TryGetValue(lobbyCode, out var lobby))
            {
                lobby.TryRemove(username, out _);

                if (lobby.IsEmpty)
                {
                    _warnings.TryRemove(lobbyCode, out _);
                }
            }
        }
    }
}
