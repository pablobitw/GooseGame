using System.Collections.Generic;
using System.Threading.Tasks;
using static GameServer.Repositories.LeaderboardRepository;

namespace GameServer.Repositories
{
    public interface ILeaderboardRepository
    {
        Task<List<PlayerStatResult>> GetAllPlayerStatsAsync();
    }
}