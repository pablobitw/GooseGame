using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GameServer.Chat.Moderation
{
    public static class ProfanityFilter
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private static readonly IReadOnlyList<string> _bannedWords = LoadBannedWords();

        public static ChatModerationResult Analyze(string message)
        {
            ChatModerationResult result;

            var originalMessage = message ?? string.Empty;
            var censoredMessage = originalMessage;
            var normalizedMessage = Normalize(originalMessage);
            var wasCensored = false;

            if (!string.IsNullOrWhiteSpace(originalMessage))
            {
                foreach (var bannedWord in _bannedWords)
                {
                    if (string.IsNullOrEmpty(bannedWord))
                        continue;

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
            }

            if (wasCensored)
            {
                result = ChatModerationResult.Censored(censoredMessage);
            }
            else
            {
                result = ChatModerationResult.Allowed(originalMessage);
            }

            return result;
        }

        private static Regex BuildFlexibleRegex(string bannedWord)
        {
            if (string.IsNullOrEmpty(bannedWord))
                throw new ArgumentException("bannedWord must not be null or empty", nameof(bannedWord));

            var pattern = string.Join(
                @"[\W_]*",
                bannedWord.Select(c => $"{Regex.Escape(c.ToString())}+")
            );

            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
        }

        private static string Normalize(string input)
        {
            string result = string.Empty;

            if (!string.IsNullOrEmpty(input))
            {
                var lower = input.ToLowerInvariant();

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

                var sb = new StringBuilder(lower.Length);

                foreach (var c in lower)
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

                result = sb.ToString();
            }

            return result;
        }

        private static IReadOnlyList<string> LoadBannedWords()
        {
            IReadOnlyList<string> resultList;

            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var filePath = Path.Combine(basePath, "Chat", "Moderation", "profanities.txt");

                if (!File.Exists(filePath))
                {
                    resultList = new ReadOnlyCollection<string>(new List<string>());
                }
                else
                {
                    var lines = File.ReadAllLines(filePath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => Normalize(line))
                        .Distinct()
                        .OrderByDescending(word => word.Length)
                        .ToList();

                    resultList = new ReadOnlyCollection<string>(lines);
                }
            }
            catch (IOException)
            {
                resultList = new ReadOnlyCollection<string>(new List<string>());
            }
            catch (UnauthorizedAccessException)
            {
                resultList = new ReadOnlyCollection<string>(new List<string>());
            }

            return resultList;
        }
    }
}
