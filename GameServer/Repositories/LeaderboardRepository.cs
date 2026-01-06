using GameServer.DTOs;
using GameServer.Interfaces;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class LeaderboardRepository : ILeaderboardRepository
    {
        public async Task<List<PlayerStatResult>> GetAllPlayerStatsAsync()
        {
            using (var context = new GameDatabase_Container())
            {
                context.Configuration.LazyLoadingEnabled = false;
                context.Configuration.ProxyCreationEnabled = false;

                var query = context.Players
                    .Where(p => p.PlayerStat != null)
                    .Where(p => !p.IsGuest) 
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