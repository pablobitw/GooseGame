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
        public async Task<List<dynamic>> GetAllPlayerStatsAsync()
        {
            using (var context = new GameDatabase_Container())
            {
                var query = from stat in context.PlayerStats
                            join player in context.Players
                            on stat.IdPlayer_IdPlayer equals player.IdPlayer
                            select new
                            {
                                Username = player.Username,
                                Avatar = player.Avatar,
                                Wins = stat.MatchesWon
                            };

                return await query.OrderByDescending(s => s.Wins).ToListAsync<dynamic>();
            }
        }
    }
}