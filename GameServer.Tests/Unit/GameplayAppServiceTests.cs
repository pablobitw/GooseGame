#nullable disable
using GameServer.DTOs.Gameplay;
using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Services.Common;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class GameplayAppServiceTests
    {
        private readonly Mock<IGameplayRepository> _repoMock;
        private readonly Mock<IGameplayRepositoryFactory> _repoFactoryMock;
        private readonly Mock<IGameplayConnectionManager> _connMock;
        private readonly Mock<IGameplayStateManager> _stateMock;
        private readonly Mock<IGameMonitor> _monitorMock;
        private readonly Mock<IVoteLogic> _voteMock;
        private readonly Mock<ISanctionFactory> _sanctionFactoryMock;
        private readonly Mock<ISanctionAppService> _sanctionServiceMock;
        private readonly Mock<IGameplayServiceCallback> _callbackMock;

        private readonly GameplayAppService _service;

        public GameplayAppServiceTests()
        {
            _repoMock = new Mock<IGameplayRepository>();
            _repoFactoryMock = new Mock<IGameplayRepositoryFactory>();
            _connMock = new Mock<IGameplayConnectionManager>();
            _stateMock = new Mock<IGameplayStateManager>();
            _monitorMock = new Mock<IGameMonitor>();
            _voteMock = new Mock<IVoteLogic>();
            _sanctionFactoryMock = new Mock<ISanctionFactory>();
            _sanctionServiceMock = new Mock<ISanctionAppService>();
            _callbackMock = new Mock<IGameplayServiceCallback>();

            _repoFactoryMock.Setup(f => f.Create()).Returns(_repoMock.Object);
            _sanctionFactoryMock.Setup(f => f.Create()).Returns(_sanctionServiceMock.Object);
            _connMock.Setup(c => c.GetGameplayClient(It.IsAny<string>())).Returns(_callbackMock.Object);

            _service = new GameplayAppService(
                _repoMock.Object,
                _repoFactoryMock.Object,
                _connMock.Object,
                _stateMock.Object,
                _monitorMock.Object,
                _voteMock.Object,
                _sanctionFactoryMock.Object
            );
        }

        [Fact]
        public async Task RollDice_NullRequest_ReturnsUnknownError()
        {
            GameplayRequest request = null;

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(GameplayErrorType.Unknown, result.ErrorType);
        }

        [Fact]
        public async Task RollDice_GameNotFound_ReturnsGameNotFoundError()
        {
            var request = new GameplayRequest { LobbyCode = "INVALID" };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("INVALID")).ReturnsAsync((Game)null);

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(GameplayErrorType.GameNotFound, result.ErrorType);
        }

        [Fact]
        public async Task RollDice_GameFinished_ReturnsGameFinishedError()
        {
            var request = new GameplayRequest { LobbyCode = "CODE" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.Finished };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(GameplayErrorType.GameFinished, result.ErrorType);
        }

        [Fact]
        public async Task RollDice_ProcessingLockActive_ReturnsTimeoutError()
        {
            var request = new GameplayRequest { LobbyCode = "CODE" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(false);

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(GameplayErrorType.Timeout, result.ErrorType);
        }

        [Fact]
        public async Task RollDice_NotPlayerTurn_ReturnsNotYourTurnError()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Player2" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            var players = new List<Player>
            {
                new Player { IdPlayer = 10, Username = "Player1" },
                new Player { IdPlayer = 20, Username = "Player2" }
            };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _repoMock.Setup(r => r.GetMoveCountAsync(1)).ReturnsAsync(0);
            _repoMock.Setup(r => r.GetExtraTurnCountAsync(1)).ReturnsAsync(0);

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(GameplayErrorType.NotYourTurn, result.ErrorType);
        }

        [Fact]
        public async Task RollDice_SkippedTurn_ProcessesSkipAndReturnsZeroRoll()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Player1" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            var player = new Player { IdPlayer = 10, Username = "Player1", TurnsSkipped = 1 };
            var players = new List<Player> { player };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _repoMock.Setup(r => r.GetMoveCountAsync(1)).ReturnsAsync(0);
            _repoMock.Setup(r => r.GetLastMoveForPlayerAsync(1, 10)).ReturnsAsync(new MoveRecord { FinalPosition = 5 });

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(0, result.Total);
        }

        [Fact]
        public async Task RollDice_NormalTurn_ReturnsSuccess()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Player1" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            var player = new Player { IdPlayer = 10, Username = "Player1", TurnsSkipped = 0 };
            var players = new List<Player> { player };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _repoMock.Setup(r => r.GetLastMoveForPlayerAsync(1, 10)).ReturnsAsync(new MoveRecord { FinalPosition = 0 });

            var result = await _service.RollDiceAsync(request);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task RollDice_RepositoryException_ReturnsUnknownError()
        {
            var request = new GameplayRequest { LobbyCode = "CODE" };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ThrowsAsync(new Exception());

            var result = await _service.RollDiceAsync(request);

            Assert.Equal(GameplayErrorType.Unknown, result.ErrorType);
        }

        [Fact]
        public async Task RollDice_WinningTurn_ReturnsSuccess()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Winner" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            var player = new Player { IdPlayer = 10, Username = "Winner" };
            var players = new List<Player> { player };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _repoMock.Setup(r => r.GetPlayersWithStatsInGameAsync(1)).ReturnsAsync(players);
            _repoMock.Setup(r => r.GetLastMoveForPlayerAsync(1, 10)).ReturnsAsync(new MoveRecord { FinalPosition = 62 });

            var result = await _service.RollDiceAsync(request);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task GetGameState_NullRequest_ReturnsFailure()
        {
            GameplayRequest request = null;

            var result = await _service.GetGameStateAsync(request);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetGameState_GameNotFound_ReturnsGameNotFoundError()
        {
            var request = new GameplayRequest { LobbyCode = "INVALID" };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("INVALID")).ReturnsAsync((Game)null);

            var result = await _service.GetGameStateAsync(request);

            Assert.Equal(GameplayErrorType.GameNotFound, result.ErrorType);
        }

        [Fact]
        public async Task GetGameState_PlayerKicked_ReturnsKickedStatus()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "KickedPlayer" };
            var game = new Game { IdGame = 1 };
            var player = new Player { IdPlayer = 10, GameIdGame = 2 };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync("KickedPlayer")).ReturnsAsync(player);

            var result = await _service.GetGameStateAsync(request);

            Assert.True(result.IsKicked);
        }

        [Fact]
        public async Task GetGameState_FinishedGame_ReturnsGameOver()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Player1" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.Finished };
            var player = new Player { IdPlayer = 10, GameIdGame = 1 };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync("Player1")).ReturnsAsync(player);

            var result = await _service.GetGameStateAsync(request);

            Assert.True(result.IsGameOver);
        }

        [Fact]
        public async Task GetGameState_ActiveGame_ReturnsStateWithPositions()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Player1" };
            var game = new Game { IdGame = 1, GameStatus = (int)GameStatus.InProgress };
            var player1 = new Player { IdPlayer = 10, Username = "Player1", GameIdGame = 1 };
            var player2 = new Player { IdPlayer = 20, Username = "Player2", GameIdGame = 1 };
            var players = new List<Player> { player1, player2 };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync("Player1")).ReturnsAsync(player1);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _repoMock.Setup(r => r.GetGameLogsAsync(1, 20)).ReturnsAsync(new List<string>());

            var result = await _service.GetGameStateAsync(request);

            Assert.Equal(2, result.PlayerPositions.Count);
        }

        [Fact]
        public async Task GetGameState_Exception_ReturnsUnknownError()
        {
            var request = new GameplayRequest { LobbyCode = "CODE" };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ThrowsAsync(new Exception());

            var result = await _service.GetGameStateAsync(request);

            Assert.Equal(GameplayErrorType.Unknown, result.ErrorType);
        }

        [Fact]
        public async Task LeaveGame_GameNotFound_ReturnsFalse()
        {
            var request = new GameplayRequest { LobbyCode = "INVALID" };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("INVALID")).ReturnsAsync((Game)null);

            var result = await _service.LeaveGameAsync(request);

            Assert.False(result);
        }

        [Fact]
        public async Task LeaveGame_PlayerNotFound_ReturnsFalse()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Ghost" };
            var game = new Game { IdGame = 1 };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync("Ghost")).ReturnsAsync((Player)null);

            var result = await _service.LeaveGameAsync(request);

            Assert.False(result);
        }

        [Fact]
        public async Task LeaveGame_OnePlayerRemaining_EndsGame()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Leaver" };
            var game = new Game { IdGame = 1 };
            var leaver = new Player { IdPlayer = 10, Username = "Leaver" };
            var winner = new Player { IdPlayer = 20, Username = "Winner" };
            var allPlayers = new List<Player> { leaver, winner };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync("Leaver")).ReturnsAsync(leaver);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(10)).ReturnsAsync(leaver);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(allPlayers);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(20)).ReturnsAsync(winner);

            var result = await _service.LeaveGameAsync(request);

            _monitorMock.Verify(m => m.StopMonitoring(1), Times.Once);
        }

        [Fact]
        public async Task LeaveGame_MultiplePlayersRemaining_ContinuesGame()
        {
            var request = new GameplayRequest { LobbyCode = "CODE", Username = "Leaver" };
            var game = new Game { IdGame = 1 };
            var leaver = new Player { IdPlayer = 10, Username = "Leaver" };
            var p2 = new Player { IdPlayer = 20 };
            var p3 = new Player { IdPlayer = 30 };
            var allPlayers = new List<Player> { leaver, p2, p3 };

            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync("Leaver")).ReturnsAsync(leaver);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(10)).ReturnsAsync(leaver);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(allPlayers);

            var result = await _service.LeaveGameAsync(request);

            _monitorMock.Verify(m => m.StopMonitoring(1), Times.Never);
        }

        [Fact]
        public async Task LeaveGame_Exception_ReturnsFalse()
        {
            var request = new GameplayRequest { LobbyCode = "CODE" };
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ThrowsAsync(new Exception());

            var result = await _service.LeaveGameAsync(request);

            Assert.False(result);
        }

        [Fact]
        public async Task ProcessAfk_LockedGame_DoesNothing()
        {
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(false);

            await _service.ProcessAfkTimeout(1);

            _repoMock.Verify(r => r.GetPlayersInGameAsync(1), Times.Never);
        }

        [Fact]
        public async Task ProcessAfk_NoPlayers_DoesNothing()
        {
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player>());

            await _service.ProcessAfkTimeout(1);

            _stateMock.Verify(s => s.RemoveProcessingGame(1), Times.Once);
        }

        [Fact]
        public async Task ProcessAfk_LowStrikes_AddsStrikeAndSkipsTurn()
        {
            var player = new Player { IdPlayer = 10, Username = "AFK_Guy" };
            var players = new List<Player> { player };

            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _stateMock.Setup(s => s.AddOrUpdateAfkStrike("AFK_Guy")).Returns(1);

            await _service.ProcessAfkTimeout(1);

            _repoMock.Verify(r => r.AddMove(It.IsAny<MoveRecord>()), Times.Once);
        }

        [Fact]
        public async Task ProcessAfk_MaxStrikes_KicksPlayer()
        {
            var player = new Player { IdPlayer = 10, Username = "AFK_Guy" };
            var players = new List<Player> { player };
            var game = new Game { IdGame = 1, LobbyCode = "CODE" };

            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);
            _stateMock.Setup(s => s.AddOrUpdateAfkStrike("AFK_Guy")).Returns(3);
            _repoMock.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            await _service.ProcessAfkTimeout(1);

            _sanctionServiceMock.Verify(s => s.ProcessKickAsync("AFK_Guy", "CODE", "AFK", "SYSTEM"), Times.Once);
        }

        [Fact]
        public async Task ProcessAfk_Exception_LogsAndRemovesLock()
        {
            _stateMock.Setup(s => s.TryAddProcessingGame(1)).Returns(true);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(1)).ThrowsAsync(new Exception());

            await _service.ProcessAfkTimeout(1);

            _stateMock.Verify(s => s.RemoveProcessingGame(1), Times.Once);
        }

        [Fact]
        public async Task InitiateVoteKick_DelegatesToLogic()
        {
            var request = new VoteRequestDto();

            await _service.InitiateVoteKickAsync(request);

            _voteMock.Verify(v => v.InitiateVoteAsync(request), Times.Once);
        }

        [Fact]
        public async Task CastVote_DelegatesToLogic()
        {
            var request = new VoteResponseDto();

            await _service.CastVoteAsync(request);

            _voteMock.Verify(v => v.CastVoteAsync(request), Times.Once);
        }
    }
}