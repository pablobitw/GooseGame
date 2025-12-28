using System.Collections.Concurrent;

namespace GameServer.Chat.Moderation
{
    public static class WarningTracker
    {
        private static readonly ConcurrentDictionary<string,
            ConcurrentDictionary<string, int>> _warnings
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();

        private const int MaxWarnings = 3;

        public static WarningLevel RegisterWarning(string lobbyCode, string username)
        {
            var lobby = _warnings.GetOrAdd(
                lobbyCode,
                _ => new ConcurrentDictionary<string, int>()
            );

            var count = lobby.AddOrUpdate(username, 1, (_, current) => current + 1);

            if (count == 1)
                return WarningLevel.Warning;

            if (count == 2)
                return WarningLevel.LastWarning;

            return WarningLevel.Punishment;
        }

        public static void Reset(string lobbyCode, string username)
        {
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
