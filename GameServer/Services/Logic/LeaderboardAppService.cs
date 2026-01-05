using GameServer.DTOs;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class LeaderboardAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardAppService));

        private readonly ILeaderboardRepository _repository;

        public LeaderboardAppService(ILeaderboardRepository repository = null)
        {
            _repository = repository ?? new LeaderboardRepository();
        }

        public async Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername)
        {
            try
            {
                var rawStats = await _repository.GetAllPlayerStatsAsync();

                if (rawStats == null)
                {
                    return new List<LeaderboardDto>();
                }

                var fullLeaderboard = rawStats.Select((s, index) => new LeaderboardDto
                {
                    Rank = index + 1,
                    Username = s.Username,

                    AvatarPath = string.IsNullOrEmpty(s.Avatar)
                        ? null
                        : $"/Assets/Avatar/{s.Avatar}",

                    Wins = s.Wins,
                    IsCurrentUser = s.Username.Equals(requestingUsername, StringComparison.OrdinalIgnoreCase)
                }).ToList();

                var top10 = fullLeaderboard.Take(10).ToList();
                var currentUser = fullLeaderboard.FirstOrDefault(u => u.IsCurrentUser);

                if (currentUser == null || currentUser.Rank <= 10)
                {
                    return top10;
                }

                var finalDisplayList = new List<LeaderboardDto>();
                finalDisplayList.AddRange(top10.Take(9));
                finalDisplayList.Add(currentUser);

                return finalDisplayList;
            }
            catch (EntityException ex)
            {
                Log.Error($"Error de Entity Framework generando leaderboard para {requestingUsername}", ex);
                return new List<LeaderboardDto>();
            }
            catch (SqlException ex)
            {
                Log.Error($"Error de SQL generando leaderboard para {requestingUsername}", ex);
                return new List<LeaderboardDto>();
            }
            catch (TimeoutException ex)
            {
                Log.Error($"Timeout generando leaderboard para {requestingUsername}", ex);
                return new List<LeaderboardDto>();
            }
            catch (ArgumentNullException ex)
            {
                Log.Error($"Referencia nula detectada generando leaderboard para {requestingUsername}", ex);
                return new List<LeaderboardDto>();
            }
            catch (Exception ex)
            {
                Log.Error($"Error inesperado generando leaderboard para {requestingUsername}", ex);
                return new List<LeaderboardDto>();
            }
        }
    }
}