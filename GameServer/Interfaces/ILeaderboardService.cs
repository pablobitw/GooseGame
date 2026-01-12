using GameServer.DTOs;
using GameServer.Faults;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface ILeaderboardService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername);
    }
}