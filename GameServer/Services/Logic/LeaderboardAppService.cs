using GameServer.DTOs;
using GameServer.Faults;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Common;
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

        private const int RankStartOffset = 1;
        private const int TopLeaderboardLimit = 10;
        private const int TopEntriesBeforeCurrentUser = 9;
        private const string DefaultAvatarPath = "/Assets/Avatar/default_avatar.png";
        private const string AvatarPathPrefix = "/Assets/Avatar/";

        private readonly ILeaderboardRepository _repository;
        public LeaderboardAppService(ILeaderboardRepository repository = null)
        {
            _repository = repository ?? new LeaderboardRepository();
        }
        public async Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestingUsername))
                {
                    throw new ArgumentNullException(nameof(requestingUsername), "El usuario solicitante es requerido.");
                }
                var rawStats = await _repository.GetAllPlayerStatsAsync();
                if (rawStats == null || !rawStats.Any())
                {
                    return new List<LeaderboardDto>();
                }
                var fullLeaderboard = rawStats.Select((s, index) => new LeaderboardDto
                {
                    Rank = index + RankStartOffset,
                    Username = s.Username,
                    AvatarPath = string.IsNullOrEmpty(s.Avatar)
                        ? DefaultAvatarPath
                        : $"{AvatarPathPrefix}{s.Avatar}",
                    Wins = s.Wins,
                    IsCurrentUser = s.Username.Equals(requestingUsername, StringComparison.OrdinalIgnoreCase)
                }).ToList();
                var top10 = fullLeaderboard.Take(TopLeaderboardLimit).ToList();
                var currentUser = fullLeaderboard.FirstOrDefault(u => u.IsCurrentUser);
                if (currentUser == null || currentUser.Rank <= TopLeaderboardLimit)
                {
                    return top10;
                }
                var finalDisplayList = new List<LeaderboardDto>(top10.Take(TopEntriesBeforeCurrentUser));
                finalDisplayList.Add(currentUser);
                return finalDisplayList;
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo leaderboard para {requestingUsername}", ex);
                throw ExceptionManager.Map(ex);
            }
        }
    }
}