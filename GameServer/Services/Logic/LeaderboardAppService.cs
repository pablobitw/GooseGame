using GameServer.DTOs;
using GameServer.Repositories;
using GameServer.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class LeaderboardAppService
    {
        private readonly ILeaderboardRepository _repository;

        public LeaderboardAppService(ILeaderboardRepository repository = null)
        {
            _repository = repository ?? new LeaderboardRepository();
        }

        public async Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername)
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
                Rank = index + 1,
                Username = s.Username,
                AvatarPath = string.IsNullOrEmpty(s.Avatar)
                    ? "/Assets/Avatar/default_avatar.png"
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

            var finalDisplayList = new List<LeaderboardDto>(top10.Take(9));
            finalDisplayList.Add(currentUser);

            return finalDisplayList;
        }
    }
}