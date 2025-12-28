using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GameServer.Chat.Moderation
{
    public sealed class SpamTracker
    {
        private const int MaxMessages = 5;
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(20);

        private readonly ConcurrentDictionary<string, Queue<DateTime>> _messageHistory;

        public SpamTracker()
        {
            _messageHistory = new ConcurrentDictionary<string, Queue<DateTime>>();
        }

        public ChatModerationResult Analyze(string lobbyCode, string username)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode) || string.IsNullOrWhiteSpace(username))
            {
                return ChatModerationResult.Allowed(null);
            }

            var key = BuildKey(lobbyCode, username);
            var now = DateTime.UtcNow;

            var queue = _messageHistory.GetOrAdd(key, _ => new Queue<DateTime>());

            lock (queue)
            {
                Cleanup(queue, now);

                if (queue.Count >= MaxMessages)
                {
                    return ChatModerationResult.Blocked(
                        $"SYSTEM: El mensaje de {username} no fue enviado por spam."
                    );
                }

                queue.Enqueue(now);
            }

            return ChatModerationResult.Allowed(null);
        }

        private static void Cleanup(Queue<DateTime> queue, DateTime now)
        {
            while (queue.Count > 0 && now - queue.Peek() > Window)
            {
                queue.Dequeue();
            }
        }

        private static string BuildKey(string lobbyCode, string username)
        {
            return $"{lobbyCode}:{username}".ToLowerInvariant();
        }
    }
}
