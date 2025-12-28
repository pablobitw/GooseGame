using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GameServer.Chat.Moderation
{
    public sealed class ProfanityFilter
    {
        private static readonly List<string> _bannedWords;

        static ProfanityFilter()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "Chat", "Moderation", "profanities.txt");

            if (!File.Exists(filePath))
            {
                _bannedWords = new List<string>();
                return;
            }

            _bannedWords = File.ReadAllLines(filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => Normalize(line))
                .Distinct()
                .OrderByDescending(word => word.Length)
                .ToList();
        }

        public ChatModerationResult Analyze(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return ChatModerationResult.Allowed(message);
            }

            var originalMessage = message;
            var censoredMessage = message;
            var normalizedMessage = Normalize(message);

            bool wasCensored = false;

            foreach (var bannedWord in _bannedWords)
            {
                if (!normalizedMessage.Contains(bannedWord))
                    continue;

                var regex = BuildFlexibleRegex(bannedWord);

                censoredMessage = regex.Replace(censoredMessage, match =>
                {
                    wasCensored = true;
                    return new string('*', match.Value.Length);
                });

                normalizedMessage = Normalize(censoredMessage);
            }

            return wasCensored
                ? ChatModerationResult.Censored(censoredMessage)
                : ChatModerationResult.Allowed(originalMessage);
        }

        private static Regex BuildFlexibleRegex(string bannedWord)
        {
            var pattern = string.Join(
                @"[\W_]*",
                bannedWord.Select(c => $"{Regex.Escape(c.ToString())}+")
            );

            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }


        private static string Normalize(string input)
        {
            input = input.ToLowerInvariant();

            var map = new Dictionary<char, char>
            {
                ['0'] = 'o',
                ['1'] = 'i',
                ['3'] = 'e',
                ['4'] = 'a',
                ['5'] = 's',
                ['7'] = 't',
                ['@'] = 'a',
                ['$'] = 's'
            };

            var sb = new StringBuilder();

            foreach (var c in input)
            {
                if (map.TryGetValue(c, out var mapped))
                {
                    sb.Append(mapped);
                }
                else if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
