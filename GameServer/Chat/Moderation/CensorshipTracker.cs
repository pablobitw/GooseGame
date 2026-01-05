using System.Collections.Concurrent;

namespace GameServer.Chat.Moderation
{
    public static class CensorshipTracker
    {
        private const int MaxCensoredMessages = 5;

        private static readonly ConcurrentDictionary<string, int> _counters
            = new ConcurrentDictionary<string, int>();

        private static string BuildKey(string lobbyCode, string username)
            => $"{lobbyCode}:{username}".ToLowerInvariant();

        public static bool RegisterCensoredMessage(string lobbyCode, string username)
        {
            var key = BuildKey(lobbyCode, username);

            var newCount = _counters.AddOrUpdate(
                key,
                1,
                (_, current) => current + 1
            );

            if (newCount >= MaxCensoredMessages)
            {
                _counters.TryRemove(key, out _);
                return true; 
            }

            return false;
        }

        public static void Reset(string lobbyCode, string username)
        {
            var key = BuildKey(lobbyCode, username);
            _counters.TryRemove(key, out _);
        }
    }
}
