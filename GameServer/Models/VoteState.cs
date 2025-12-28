using System.Collections.Generic;

namespace GameServer.Models
{
    public class VoteState
    {
        public string TargetUsername { get; set; }
        public string InitiatorUsername { get; set; }
        public string Reason { get; set; }
        public HashSet<string> VotesFor { get; set; } = new HashSet<string>();
        public HashSet<string> VotesAgainst { get; set; } = new HashSet<string>();
        public int TotalEligibleVoters { get; set; }
    }
}