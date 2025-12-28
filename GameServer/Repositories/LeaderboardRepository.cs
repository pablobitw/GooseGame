using GameServer.DTOs;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class LeaderboardRepository
    {
        public class PlayerStatResult
        {
            public string Username { get; set; }
            public string Avatar { get; set; }
            public int Wins { get; set; }
        }

        public async Task<List<PlayerStatResult>> GetAllPlayerStatsAsync()
        {
            using (var context = new GameDatabase_Container())
            {
                var query = context.Players
                                   .Where(p => p.PlayerStat != null)
                                   .Select(p => new PlayerStatResult
                                   {
                                       Username = p.Username,
                                       Avatar = p.Avatar,
                                       Wins = p.PlayerStat.MatchesWon
                                   });

                return await query.OrderByDescending(s => s.Wins).ToListAsync();
            }
        }
    }
}