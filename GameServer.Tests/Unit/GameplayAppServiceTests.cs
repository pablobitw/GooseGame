using global::GameServer.DTOs.Gameplay;
using global::GameServer.Helpers;
using global::GameServer.Interfaces;
using global::GameServer.Models;
using global::GameServer.Repositories;
using global::GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Reflection;
using System.Data.SqlClient;
using System.Linq;
using Xunit;

namespace GameServer.Tests.Services
{
    public class GameplayAppServiceTests : IDisposable
    {
        private readonly Mock<IGameplayRepository> _mockRepository;
        private readonly Mock<IGameplayServiceCallback> _mockCallback;
        private readonly GameplayAppService _service;

        private const int GameId = 100;
        private const string LobbyCode = "GAME123";
        private const string User1 = "PlayerOne";
        private const string User2 = "PlayerTwo";

        public GameplayAppServiceTests()
        {
            _mockRepository = new Mock<IGameplayRepository>();
            _mockCallback = new Mock<IGameplayServiceCallback>();
            _service = new GameplayAppService(_mockRepository.Object);

            ResetStaticData();
            SetupBasicGame();
        }

        public void Dispose()
        {
            ResetStaticData();
        }

        private void ResetStaticData()
        {
            var procField = typeof(GameplayAppService).GetField("_processingGames", BindingFlags.NonPublic | BindingFlags.Static);
            if (procField != null)
                ((ConcurrentDictionary<int, bool>)procField.GetValue(null)).Clear();

            var afkField = typeof(GameplayAppService).GetField("_afkStrikes", BindingFlags.NonPublic | BindingFlags.Static);
            if (afkField != null)
                ((ConcurrentDictionary<string, int>)afkField.GetValue(null)).Clear();

            var connField = typeof(ConnectionManager).GetField("_gameplayCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
            if (connField != null)
                ((Dictionary<string, IGameplayServiceCallback>)connField.GetValue(null)).Clear();
        }

        private void SetupBasicGame()
        {
            var game = new Game { IdGame = GameId, LobbyCode = LobbyCode, GameStatus = (int)GameStatus.InProgress };
            var p1 = new Player { IdPlayer = 1, Username = User1, GameIdGame = GameId, Account_IdAccount = 1, TurnsSkipped = 0 };
            var p2 = new Player { IdPlayer = 2, Username = User2, GameIdGame = GameId, Account_IdAccount = 2, TurnsSkipped = 0 };
            var players = new List<Player> { p1, p2 };

            _mockRepository.Setup(r => r.GetGameByLobbyCodeAsync(It.IsAny<string>())).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetGameByIdAsync(It.IsAny<int>())).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(It.IsAny<int>())).ReturnsAsync(players);
            _mockRepository.Setup(r => r.GetGameLogsAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new List<string>());
            _mockRepository.Setup(r => r.GetMoveCountAsync(It.IsAny<int>())).ReturnsAsync(0);
            _mockRepository.Setup(r => r.GetExtraTurnCountAsync(It.IsAny<int>())).ReturnsAsync(0);

            _mockRepository.Setup(r => r.GetLastMoveForPlayerAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new MoveRecord { FinalPosition = 0 });
            _mockRepository.Setup(r => r.GetLastGlobalMoveAsync(It.IsAny<int>()))
                .ReturnsAsync(new MoveRecord { DiceOne = 0, DiceTwo = 0 });

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ReturnsAsync((string u) => u == User1 ? p1 : p2);
            _mockRepository.Setup(r => r.GetPlayerWithStatsByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => id == 1 ? p1 : p2);

            ConnectionManager.RegisterGameplayClient(User1, _mockCallback.Object);
            ConnectionManager.RegisterGameplayClient(User2, new Mock<IGameplayServiceCallback>().Object);
        }

        [Fact]
        public async Task RollDice_CorrectTurn_ShouldProcess()
        {
            // Test 1
            var request = new GameplayRequest { LobbyCode = LobbyCode, Username = User1 };
            var result = await _service.RollDiceAsync(request);

            Assert.NotNull(result);
            Assert.True(result.Total > 0);
            _mockRepository.Verify(r => r.AddMove(It.IsAny<MoveRecord>()), Times.Once);
        }

        [Fact]
        public async Task RollDice_WrongTurn_ShouldReturnNull()
        {
            // Act
            var request = new GameplayRequest { LobbyCode = LobbyCode, Username = User2 };
            var result = await _service.RollDiceAsync(request);

            // Assert
            Assert.Null(result);
            _mockRepository.Verify(r => r.AddMove(It.IsAny<MoveRecord>()), Times.Never);
        }

        [Fact]
        public async Task ProcessAfkTimeout_FirstStrike_ShouldWarn()
        {
            // Test 3
            await _service.ProcessAfkTimeout(GameId);

            // Assert
            _mockRepository.Verify(r => r.AddMove(It.Is<MoveRecord>(m =>
                m.ActionDescription.Contains("Pierde") || m.ActionDescription.Contains("AFK"))), Times.Once);
        }

        [Fact]
        public async Task NotifyTurnUpdate_CommunicationException_ShouldUnregisterClient()
        {
            _mockCallback.Setup(c => c.OnTurnChanged(It.IsAny<GameStateDto>())).Throws(new CommunicationException());

            // Act
            var request = new GameplayRequest { LobbyCode = LobbyCode, Username = User1 };
            await _service.RollDiceAsync(request);

            // Assert:
            Assert.Null(ConnectionManager.GetGameplayClient(User1));
        }

        [Fact]
        public async Task LeaveGame_ThreePlayers_GameContinues()
        {
            var p3 = new Player { IdPlayer = 3, Username = "User3", GameIdGame = GameId };
            var players = new List<Player> {
                new Player { IdPlayer = 1, Username = User1, GameIdGame = GameId },
                new Player { IdPlayer = 2, Username = User2, GameIdGame = GameId },
                p3
            };

            _mockRepository.Setup(r => r.GetPlayersInGameAsync(It.IsAny<int>())).ReturnsAsync(players);
            var request = new GameplayRequest { LobbyCode = LobbyCode, Username = User1 };

            // Act
            bool result = await _service.LeaveGameAsync(request);

            // Assert
            Assert.True(result);
            _mockCallback.Verify(c => c.OnGameFinished(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RollDice_SqlException_ShouldHandleGracefully()
        {
            var request = new GameplayRequest { LobbyCode = LobbyCode, Username = User1 };
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(CreateSqlException(53));

            // Act
            var result = await _service.RollDiceAsync(request);

            Assert.Null(result);
        }

        private SqlException CreateSqlException(int number)
        {
            var collectionConstructor = typeof(SqlErrorCollection).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
            var errorCollection = (SqlErrorCollection)collectionConstructor.Invoke(null);
            var errorConstructor = typeof(SqlError).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int) }, null);
            var error = (SqlError)errorConstructor.Invoke(new object[] { number, (byte)0, (byte)0, "server", "errMsg", "proc", 100 });
            var addMethod = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
            addMethod.Invoke(errorCollection, new object[] { error });
            var exceptionConstructor = typeof(SqlException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid) }, null);
            return (SqlException)exceptionConstructor.Invoke(new object[] { "Error simulated", errorCollection, null, Guid.NewGuid() });
        }
    }
}