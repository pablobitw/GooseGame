using GameServer.DTOs; 
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    public interface ILeaderboardRepository
    {
        Task<List<PlayerStatResult>> GetAllPlayerStatsAsync();
    }
}