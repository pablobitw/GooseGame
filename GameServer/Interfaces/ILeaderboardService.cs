using GameServer.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface ILeaderboardService
    {
        [OperationContract]
        Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername);
    }
}