using GameServer.DTOs;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class LeaderboardAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardAppService));
        private readonly LeaderboardRepository _repository;

        public LeaderboardAppService()
        {
            _repository = new LeaderboardRepository();
        }

        public async Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername)
        {
            try
            {
                var rawStats = await _repository.GetAllPlayerStatsAsync();

                var fullLeaderboard = rawStats.Select((s, index) => new LeaderboardDto
                {
                    Rank = index + 1,
                    Username = s.Username,

                    AvatarPath = string.IsNullOrEmpty(s.Avatar)
                 ? null
                 : $"/Assets/Avatar/{s.Avatar}",

                    Wins = s.Wins,
                    IsCurrentUser = s.Username == requestingUsername
                }).ToList();

                var finalDisplayList = new List<LeaderboardDto>();
                var top10 = fullLeaderboard.Take(10).ToList();
                var currentUser = fullLeaderboard.FirstOrDefault(u => u.IsCurrentUser);

                if (currentUser != null && currentUser.Rank <= 10)
                {
                    return top10;
                }

                if (currentUser != null && currentUser.Rank > 10)
                {
                    finalDisplayList.AddRange(top10.Take(9));
                    finalDisplayList.Add(currentUser);
                    return finalDisplayList;
                }

                return top10;
            }
            catch (Exception ex)
            {
                Log.Error($"Error generando leaderboard para {requestingUsername}", ex);
                return new List<LeaderboardDto>();
            }
        }
    }
}