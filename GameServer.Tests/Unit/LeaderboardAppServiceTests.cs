using global::GameServer.DTOs;
using global::GameServer.Repositories;
using global::GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;
using static global::GameServer.Repositories.LeaderboardRepository;

namespace GameServer.Tests.Services
{
    public class LeaderboardAppServiceTests
    {
        private readonly Mock<ILeaderboardRepository> _mockRepository;
        private readonly LeaderboardAppService _service;

        public LeaderboardAppServiceTests()
        {
            _mockRepository = new Mock<ILeaderboardRepository>();
            _service = new LeaderboardAppService(_mockRepository.Object);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_NullRepositoryResponse_ReturnsEmptyList()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync((List<PlayerStatResult>)null);

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("AnyUser");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_EmptyStats_ReturnsEmptyList()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(new List<PlayerStatResult>());

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("AnyUser");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_UserInTop10_ReturnsExactlyTop10()
        {
            var stats = GenerateStats(15);
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("User_5");

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(1, result.First().Rank);
            Assert.Equal(10, result.Last().Rank);
            Assert.Contains(result, x => x.Username == "User_5" && x.IsCurrentUser);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_UserOutsideTop10_ReturnsTop9PlusCurrentUser()
        {
            var stats = GenerateStats(20);
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("User_20");

            // Assert
            Assert.Equal(10, result.Count);
            // Los primeros 9 deben ser el top real
            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(i + 1, result[i].Rank);
            }
            Assert.Equal("User_20", result[9].Username);
            Assert.Equal(20, result[9].Rank);
            Assert.True(result[9].IsCurrentUser);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_AvatarFormatting_FormatsPathCorrectly()
        {
            // Arrange
            var stats = new List<PlayerStatResult>
            {
                new PlayerStatResult { Username = "User1", Avatar = "cat.png", Wins = 10 }
            };
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("User1");

            // Assert
            Assert.Equal("/Assets/Avatar/cat.png", result[0].AvatarPath);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_NoAvatar_ReturnsNullPath()
        {
            // Arrange
            var stats = new List<PlayerStatResult>
            {
                new PlayerStatResult { Username = "User1", Avatar = null, Wins = 10 }
            };
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("User1");

            // Assert
            Assert.Null(result[0].AvatarPath);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_CaseInsensitiveUsername_IdentifiesCurrentUser()
        {
            // Arrange
            var stats = new List<PlayerStatResult>
            {
                new PlayerStatResult { Username = "PlayerOne", Wins = 10 }
            };
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ReturnsAsync(stats);

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("playerone");

            // Assert
            Assert.True(result[0].IsCurrentUser);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_EntityException_ReturnsEmptyList()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ThrowsAsync(new EntityException());

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("User1");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetGlobalLeaderboard_TimeoutException_ReturnsEmptyList()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllPlayerStatsAsync()).ThrowsAsync(new TimeoutException());

            // Act
            var result = await _service.GetGlobalLeaderboardAsync("User1");

            // Assert
            Assert.Empty(result);
        }

        private List<PlayerStatResult> GenerateStats(int count)
        {
            var list = new List<PlayerStatResult>();
            for (int i = 1; i <= count; i++)
            {
                list.Add(new PlayerStatResult
                {
                    Username = $"User_{i}",
                    Wins = 100 - i, 
                    Avatar = "avatar.png"
                });
            }
            return list;
        }
    }
}