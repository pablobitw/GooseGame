using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace GameServer.Helpers
{
    public static class VoteManager
    {
        public class ActiveVote
        {
            public string TargetUsername { get; set; }
            public int VotesForKick { get; set; }
            public int VotesKeep { get; set; }
            public int TotalPlayers { get; set; }
            public HashSet<string> Voters { get; set; } = new HashSet<string>();
            public CancellationTokenSource TimerToken { get; set; }
        }

        private static readonly ConcurrentDictionary<string, ActiveVote> _activeVotes = new ConcurrentDictionary<string, ActiveVote>();
        private static readonly ConcurrentDictionary<string, bool> _voteHistory = new ConcurrentDictionary<string, bool>();

        public static bool CanInitiateVote(string requestor, string target, string lobbyCode)
        {
            string key = $"{requestor}_{target}_{lobbyCode}";
            return !_voteHistory.ContainsKey(key) && !_activeVotes.ContainsKey(lobbyCode);
        }

        public static bool TryStartVote(string lobbyCode, ActiveVote newVote, string requestor, string target)
        {
            string key = $"{requestor}_{target}_{lobbyCode}";
            if (_activeVotes.TryAdd(lobbyCode, newVote))
            {
                _voteHistory.TryAdd(key, true);
                return true;
            }
            return false;
        }

        public static ActiveVote GetActiveVote(string lobbyCode)
        {
            if (_activeVotes.TryGetValue(lobbyCode, out var vote))
            {
                return vote;
            }
            return null;
        }

        public static bool RemoveVote(string lobbyCode, out ActiveVote vote)
        {
            return _activeVotes.TryRemove(lobbyCode, out vote);
        }
    }
}