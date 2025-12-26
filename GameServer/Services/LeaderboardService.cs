using GameServer.DTOs;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LeaderboardService : ILeaderboardService
    {
        private readonly LeaderboardAppService _logic;

        public LeaderboardService()
        {
            _logic = new LeaderboardAppService();
        }

        public async Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername)
        {
            return await _logic.GetGlobalLeaderboardAsync(requestingUsername);
        }
    }
}