using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GameServer.Services.Logic;
using GameServer.Repositories.Interfaces;
using GameServer.Contracts;
using GameServer.DTOs.Gameplay;
using GameServer.Models;
using GameServer.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace GameServer.Tests
{
    [TestClass]
    public class GameplayAppServiceTests
    {
        private Mock<IGameplayRepository> _mockRepo;
        private Mock<IGameplayConnectionManager> _mockConnection;
        private Mock<IGameMonitoringService> _mockMonitor;
        private Mock<IRandomGenerator> _mockRandom;

        private GameplayAppService _service;

        [TestInitialize]
        public void Setup()
        {
            _mockRepo = new Mock<IGameplayRepository>();
            _mockConnection = new Mock<IGameplayConnectionManager>();
            _mockMonitor = new Mock<IGameMonitoringService>();
            _mockRandom = new Mock<IRandomGenerator>();

            _service = new GameplayAppService(
                _mockRepo.Object,
                _mockConnection.Object,
                _mockMonitor.Object,
                _mockRandom.Object
            );
        }

        [TestMethod]
        public async Task RollDiceAsync_ShouldMovePlayer_WhenTurnIsCorrect()
        {
            var request = new GameplayRequest { LobbyCode = "GAME123", Username = "Player1" };

            var game = new Game { IdGame = 1, LobbyCode = "GAME123", GameStatus = (int)GameStatus.InProgress };
            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("GAME123")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            var p1 = new Player { IdPlayer = 10, Username = "Player1", GameIdGame = 1 };
            var p2 = new Player { IdPlayer = 20, Username = "Player2", GameIdGame = 1 };
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1, p2 });

            _mockRepo.Setup(r => r.GetMoveCountAsync(1)).ReturnsAsync(0);
            _mockRepo.Setup(r => r.GetExtraTurnCountAsync(1)).ReturnsAsync(0);

            var lastMove = new MoveRecord { FinalPosition = 1 };
            _mockRepo.Setup(r => r.GetLastMoveForPlayerAsync(1, 10)).ReturnsAsync(lastMove);

            _mockRandom.SetupSequence(r => r.Next(It.IsAny<int>(), It.IsAny<int>()))
                       .Returns(2)
                       .Returns(1);

            var result = await _service.RollDiceAsync(request);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.Total);

            _mockRepo.Verify(r => r.AddMove(It.Is<MoveRecord>(m => m.FinalPosition == 4)), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task RollDiceAsync_ShouldFail_WhenNotPlayerTurn()
        {
            var request = new GameplayRequest { LobbyCode = "GAME123", Username = "Player2" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("GAME123")).ReturnsAsync(game);

            var p1 = new Player { IdPlayer = 10, Username = "Player1" };
            var p2 = new Player { IdPlayer = 20, Username = "Player2" };
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1, p2 });

            _mockRepo.Setup(r => r.GetMoveCountAsync(1)).ReturnsAsync(0);

            var result = await _service.RollDiceAsync(request);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayErrorType.NotYourTurn, result.ErrorType);
        }

        [TestMethod]
        public async Task GetGameStateAsync_ShouldReturnState_WhenGameExists()
        {
            var request = new GameplayRequest { LobbyCode = "GAME123", Username = "Player1" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            var p1 = new Player { IdPlayer = 10, Username = "Player1", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("GAME123")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player1")).ReturnsAsync(p1);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1 });
            _mockRepo.Setup(r => r.GetGameLogsAsync(1, 20)).ReturnsAsync(new List<string> { "Juego iniciado" });

            var state = await _service.GetGameStateAsync(request);

            Assert.IsTrue(state.Success);
            Assert.IsFalse(state.IsGameOver);
            Assert.AreEqual(1, state.PlayerPositions.Count);
            Assert.AreEqual("Player1", state.PlayerPositions[0].Username);
        }

        [TestMethod]
        public async Task LeaveGameAsync_ShouldNotifyOthers_WhenPlayerLeaves()
        {
            var request = new GameplayRequest { LobbyCode = "GAME123", Username = "Player1" };
            var game = new Game { IdGame = 1, LobbyCode = "GAME123" };
            var p1 = new Player { IdPlayer = 10, Username = "Player1", GameIdGame = 1 };
            var p2 = new Player { IdPlayer = 20, Username = "Player2", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("GAME123")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player1")).ReturnsAsync(p1);
            _mockRepo.Setup(r => r.GetPlayerWithStatsByIdAsync(10)).ReturnsAsync(p1);

            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1, p2 });

            var mockClient = new Mock<IGameplayServiceCallback>();
            _mockConnection.Setup(c => c.GetGameplayClient("Player2")).Returns(mockClient.Object);

            bool result = await _service.LeaveGameAsync(request);

            Assert.IsTrue(result);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);

            mockClient.Verify(c => c.OnTurnChanged(It.IsAny<GameStateDto>()), Times.AtLeastOnce);
        }
    }
}