#nullable disable
using GameServer.DTOs;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class LeaderboardAppServiceTests
    {
        private readonly Mock<ILeaderboardRepository> _repoMock;
        private readonly LeaderboardAppService _service;

        public LeaderboardAppServiceTests()
        {
            _repoMock = new Mock<ILeaderboardRepository>();
            _service = new LeaderboardAppService(_repoMock.Object);
        }

        private List<PlayerStatResult> GenerateStats(int count)
        {
            var list = new List<PlayerStatResult>();
            for (int i = 1; i <= count; i++)
            {
                list.Add(new PlayerStatResult
                {
                    Username = $"Player{i}",
                    Wins = 100 - i, 
                    Avatar = $"avatar{i}.png"
                });
            }
            return list;
        }

       

        [Fact]
        public async Task GetGlobalLeaderboard_EmptyRepo_ReturnsEmptyList()
        {
            _repoMock.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(new List<PlayerStatResult>());

            var result = await _service.GetGlobalLeaderboardAsync("Player1");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_AvatarFormatting_DefaultAndCustom()
        {
            var stats = new List<PlayerStatResult>
            {
                new PlayerStatResult { Username = "Custom", Avatar = "pic.png", Wins = 10 },
                new PlayerStatResult { Username = "Default", Avatar = null, Wins = 5 }
            };
            _repoMock.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            var result = await _service.GetGlobalLeaderboardAsync("Custom");

            Assert.Equal(2, result.Count);

            Assert.Equal("/Assets/Avatar/pic.png", result[0].AvatarPath);

            Assert.Equal("/Assets/Avatar/default_avatar.png", result[1].AvatarPath);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_UserInTop10_ReturnsNormalTop10()
        {
            var stats = GenerateStats(15);
            _repoMock.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            var result = await _service.GetGlobalLeaderboardAsync("Player5");

            Assert.Equal(10, result.Count);
            Assert.Equal("Player1", result[0].Username);
            Assert.Equal("Player10", result[9].Username);

            var currentUser = result.FirstOrDefault(u => u.IsCurrentUser);
            Assert.NotNull(currentUser);
            Assert.Equal("Player5", currentUser.Username);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_UserOutsideTop10_ReturnsTop9PlusUser()
        {
            var stats = GenerateStats(20);
            _repoMock.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            var result = await _service.GetGlobalLeaderboardAsync("Player15");

            Assert.Equal(10, result.Count);

            Assert.Equal("Player1", result[0].Username);
            Assert.Equal("Player9", result[8].Username);

            var lastItem = result[9];
            Assert.Equal("Player15", lastItem.Username);
            Assert.Equal(15, lastItem.Rank);
            Assert.True(lastItem.IsCurrentUser);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_UserNotFoundInList_ReturnsStandardTop10()
        {
            var stats = GenerateStats(15);
            _repoMock.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            var result = await _service.GetGlobalLeaderboardAsync("GhostPlayer");

            Assert.Equal(10, result.Count);
            Assert.Equal("Player10", result[9].Username);
            Assert.DoesNotContain(result, u => u.IsCurrentUser);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_LessThen10Players_ReturnsAll()
        {
            var stats = GenerateStats(5);
            _repoMock.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            var result = await _service.GetGlobalLeaderboardAsync("Player1");

            Assert.Equal(5, result.Count);
            Assert.Equal(1, result[0].Rank);
            Assert.Equal(5, result[4].Rank);
        }
    }
}