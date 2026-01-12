#nullable disable
using GameServer.DTOs.Lobby;
using GameServer.Faults;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Services.Common;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class LobbyAppServiceTests
    {
        private readonly Mock<ILobbyRepository> _repoMock;
        private readonly Mock<ILobbyConnectionManager> _connMock;
        private readonly Mock<IWcfContext> _wcfMock;
        private readonly Mock<IGameMonitor> _monitorMock;
        private readonly Mock<ILobbyCodeGenerator> _codeGenMock;
        private readonly Mock<ILobbyServiceCallback> _callbackMock;

        private readonly LobbyAppService _service;

        private const string HOST = "HostUser";
        private const string USER = "JoinerUser";
        private const string LOBBY_CODE = "ABCDE";
        private const int GAME_ID = 100;
        private const int HOST_ID = 1;
        private const int USER_ID = 2;

        public LobbyAppServiceTests()
        {
            _repoMock = new Mock<ILobbyRepository>();
            _connMock = new Mock<ILobbyConnectionManager>();
            _wcfMock = new Mock<IWcfContext>();
            _monitorMock = new Mock<IGameMonitor>();
            _codeGenMock = new Mock<ILobbyCodeGenerator>();
            _callbackMock = new Mock<ILobbyServiceCallback>();

            _wcfMock.Setup(w => w.GetCallbackChannel<ILobbyServiceCallback>()).Returns(_callbackMock.Object);
            _codeGenMock.Setup(c => c.GenerateRandomString(It.IsAny<int>())).Returns(LOBBY_CODE);
            _repoMock.Setup(r => r.IsLobbyCodeUnique(LOBBY_CODE)).Returns(true);

            _service = new LobbyAppService(
                _repoMock.Object,
                _connMock.Object,
                _wcfMock.Object,
                _monitorMock.Object,
                _codeGenMock.Object
            );
        }

        private SqlException CreateSqlException()
        {
            var exception = FormatterServices.GetUninitializedObject(typeof(SqlException)) as SqlException;
            var errors = FormatterServices.GetUninitializedObject(typeof(SqlErrorCollection)) as SqlErrorCollection;

            // Inyectamos un error dummy para evitar NullReferenceException al leer .Number o .Errors
            var error = FormatterServices.GetUninitializedObject(typeof(SqlError)) as SqlError;
            var errorsListField = typeof(SqlErrorCollection).GetField("errors", BindingFlags.NonPublic | BindingFlags.Instance);
            if (errorsListField != null)
            {
                var list = new System.Collections.ArrayList { error };
                errorsListField.SetValue(errors, list);
            }

            var errorsField = typeof(SqlException).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance);
            if (errorsField != null) errorsField.SetValue(exception, errors);

            return exception;
        }

        [Fact]
        public async Task CreateLobby_InvalidSettings_ReturnsInvalidData()
        {
            var req = new CreateLobbyRequest { Settings = null };
            var res = await _service.CreateLobbyAsync(req);
            Assert.Equal(LobbyErrorType.InvalidData, res.ErrorType);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public async Task CreateLobby_InvalidMaxPlayers_ReturnsInvalidData(int max)
        {
            var req = new CreateLobbyRequest
            {
                Settings = new LobbySettingsDto { MaxPlayers = max }
            };
            var res = await _service.CreateLobbyAsync(req);
            Assert.Equal(LobbyErrorType.InvalidData, res.ErrorType);
        }

        [Fact]
        public async Task CreateLobby_HostNotFound_ReturnsUserNotFound()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync((Player)null);
            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };

            var res = await _service.CreateLobbyAsync(req);

            Assert.Equal(LobbyErrorType.UserNotFound, res.ErrorType);
        }

        [Fact]
        public async Task CreateLobby_HostIsGuest_ReturnsGuestNotAllowed()
        {
            var host = new Player { IsGuest = true };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };

            var res = await _service.CreateLobbyAsync(req);

            Assert.Equal(LobbyErrorType.GuestNotAllowed, res.ErrorType);
        }

        [Fact]
        public async Task CreateLobby_HostInGame_ReturnsPlayerAlreadyInGame()
        {
            var host = new Player { IsGuest = false, GameIdGame = 999 };
            var activeGame = new Game { IdGame = 999, GameStatus = (int)GameStatus.InProgress };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetGameByIdAsync(999)).ReturnsAsync(activeGame);

            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };

            var res = await _service.CreateLobbyAsync(req);

            Assert.Equal(LobbyErrorType.PlayerAlreadyInGame, res.ErrorType);
        }

        [Fact]
        public async Task CreateLobby_Success_ReturnsLobbyCode()
        {
            var host = new Player { IsGuest = false, IdPlayer = HOST_ID };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4, BoardId = 1, IsPublic = true }
            };

            var res = await _service.CreateLobbyAsync(req);

            Assert.True(res.Success);
            Assert.Equal(LobbyErrorType.None, res.ErrorType);
            Assert.Equal(LOBBY_CODE, res.LobbyCode);
            _repoMock.Verify(r => r.AddGame(It.IsAny<Game>()), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
            _connMock.Verify(c => c.RegisterClient(HOST, _callbackMock.Object), Times.Once);
        }

        [Fact]
        public async Task CreateLobby_SqlException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ThrowsAsync(CreateSqlException());
            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };

            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.CreateLobbyAsync(req));
        }

        [Fact]
        public async Task JoinLobby_UserNotFound_ReturnsUserNotFound()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync((Player)null);
            var req = new JoinLobbyRequest { Username = USER };

            var res = await _service.JoinLobbyAsync(req);

            Assert.Equal(LobbyErrorType.UserNotFound, res.ErrorType);
        }

        [Fact]
        public async Task JoinLobby_PlayerInGame_ReturnsPlayerAlreadyInGame()
        {
            var player = new Player { GameIdGame = 500, IdPlayer = USER_ID };
            var activeGame = new Game { IdGame = 500, GameStatus = (int)GameStatus.InProgress, HostPlayerID = HOST_ID, LobbyCode = "OTHER" }; // LobbyCode diferente para forzar error

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetGameByIdAsync(500)).ReturnsAsync(activeGame);

            var req = new JoinLobbyRequest { Username = USER, LobbyCode = LOBBY_CODE };

            var res = await _service.JoinLobbyAsync(req);

            Assert.Equal(LobbyErrorType.PlayerAlreadyInGame, res.ErrorType);
        }

        [Fact]
        public async Task JoinLobby_GameNotFound_ReturnsGameNotFound()
        {
            var player = new Player();
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync((Game)null);
            var req = new JoinLobbyRequest { Username = USER, LobbyCode = LOBBY_CODE };

            var res = await _service.JoinLobbyAsync(req);

            Assert.Equal(LobbyErrorType.GameNotFound, res.ErrorType);
        }

        [Fact]
        public async Task JoinLobby_GameStarted_ReturnsGameStarted()
        {
            var player = new Player();
            var game = new Game { GameStatus = (int)GameStatus.InProgress };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            var req = new JoinLobbyRequest { Username = USER, LobbyCode = LOBBY_CODE };

            var res = await _service.JoinLobbyAsync(req);

            Assert.Equal(LobbyErrorType.GameStarted, res.ErrorType);
        }

        [Fact]
        public async Task JoinLobby_GameFull_ReturnsGameFull()
        {
            var player = new Player();
            var game = new Game { IdGame = GAME_ID, MaxPlayers = 2, GameStatus = (int)GameStatus.WaitingForPlayers };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { new Player(), new Player() });

            var req = new JoinLobbyRequest { Username = USER, LobbyCode = LOBBY_CODE };

            var res = await _service.JoinLobbyAsync(req);

            Assert.Equal(LobbyErrorType.GameFull, res.ErrorType);
        }

        [Fact]
        public async Task JoinLobby_Success_ReturnsSuccessAndNotifies()
        {
            var player = new Player { Username = USER, IdPlayer = USER_ID };
            var game = new Game { IdGame = GAME_ID, MaxPlayers = 4, GameStatus = (int)GameStatus.WaitingForPlayers, HostPlayerID = HOST_ID };
            var host = new Player { Username = HOST, IdPlayer = HOST_ID };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { host, player });
            _connMock.Setup(c => c.GetClient(HOST)).Returns(_callbackMock.Object);

            var req = new JoinLobbyRequest { Username = USER, LobbyCode = LOBBY_CODE };

            var res = await _service.JoinLobbyAsync(req);

            Assert.True(res.Success);
            Assert.Equal(2, res.PlayersInLobby.Count);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnPlayerJoined(It.Is<PlayerLobbyDto>(p => p.Username == USER)), Times.Once);
        }

        [Fact]
        public async Task StartGame_NotEnoughPlayers_ReturnsFalse()
        {
            var game = new Game { IdGame = GAME_ID };
            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { new Player() });

            var result = await _service.StartGameAsync(LOBBY_CODE);

            Assert.False(result);
        }

        [Fact]
        public async Task StartGame_Success_ReturnsTrueAndNotifies()
        {
            var game = new Game { IdGame = GAME_ID };
            var p1 = new Player { Username = HOST };
            var p2 = new Player { Username = USER };

            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { p1, p2 });
            _connMock.Setup(c => c.GetClient(It.IsAny<string>())).Returns(_callbackMock.Object);

            var result = await _service.StartGameAsync(LOBBY_CODE);

            Assert.True(result);
            Assert.Equal((int)GameStatus.InProgress, game.GameStatus);
            _monitorMock.Verify(m => m.StartMonitoring(GAME_ID), Times.Once);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnGameStarted(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task LeaveLobby_HostLeaves_DisbandsLobby()
        {
            var host = new Player { Username = HOST, IdPlayer = HOST_ID, GameIdGame = GAME_ID };
            var game = new Game { IdGame = GAME_ID, HostPlayerID = HOST_ID, LobbyCode = LOBBY_CODE };
            var user = new Player { Username = USER };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetGameByIdAsync(GAME_ID)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { host, user });
            _connMock.Setup(c => c.GetClient(USER)).Returns(_callbackMock.Object);

            var result = await _service.LeaveLobbyAsync(HOST);

            Assert.True(result);
            _repoMock.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnLobbyDisbanded(), Times.Once);
        }

        [Fact]
        public async Task LeaveLobby_PlayerLeaves_NotifiesOthers()
        {
            var user = new Player { Username = USER, IdPlayer = USER_ID, GameIdGame = GAME_ID };
            var game = new Game { IdGame = GAME_ID, HostPlayerID = HOST_ID, GameStatus = (int)GameStatus.WaitingForPlayers };
            var host = new Player { Username = HOST };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(user);
            _repoMock.Setup(r => r.GetGameByIdAsync(GAME_ID)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { host });
            _connMock.Setup(c => c.GetClient(HOST)).Returns(_callbackMock.Object);

            var result = await _service.LeaveLobbyAsync(USER);

            Assert.True(result);
            Assert.Null(user.GameIdGame);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnPlayerLeft(USER), Times.Once);
        }

        [Fact]
        public async Task KickPlayer_Success_KicksTarget()
        {
            var game = new Game { IdGame = GAME_ID, HostPlayerID = HOST_ID };
            var host = new Player { IdPlayer = HOST_ID };
            var target = new Player { IdPlayer = USER_ID, GameIdGame = GAME_ID, KickCount = 0 };

            var req = new KickPlayerRequest { LobbyCode = LOBBY_CODE, RequestorUsername = HOST, TargetUsername = USER };

            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(target);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { host });
            _connMock.Setup(c => c.GetClient(It.IsAny<string>())).Returns(_callbackMock.Object);

            var result = await _service.KickPlayerAsync(req);

            Assert.True(result);
            Assert.Equal(1, target.KickCount);
            Assert.Null(target.GameIdGame);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnPlayerKicked(It.Is<string>(s => s.Contains("expulsado"))), Times.Once);
        }

        [Fact]
        public async Task KickPlayer_Ban_BansAfter3Kicks()
        {
            var game = new Game { IdGame = GAME_ID, HostPlayerID = HOST_ID };
            var host = new Player { IdPlayer = HOST_ID };
            var target = new Player { IdPlayer = USER_ID, GameIdGame = GAME_ID, KickCount = 2 };

            var req = new KickPlayerRequest { LobbyCode = LOBBY_CODE, RequestorUsername = HOST, TargetUsername = USER };

            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(target);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { host });
            _connMock.Setup(c => c.GetClient(USER)).Returns(_callbackMock.Object);

            var result = await _service.KickPlayerAsync(req);

            Assert.True(result);
            Assert.True(target.IsBanned);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnPlayerKicked(It.Is<string>(s => s.Contains("BANEADO"))), Times.Once);
        }

        [Fact]
        public async Task SystemKickPlayer_Success_KicksAndNotifies()
        {
            var game = new Game { IdGame = GAME_ID };
            var player = new Player { GameIdGame = GAME_ID };

            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player>());
            _connMock.Setup(c => c.GetClient(USER)).Returns(_callbackMock.Object);

            await _service.SystemKickPlayerAsync(LOBBY_CODE, USER, "System Kick");

            Assert.Null(player.GameIdGame);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.OnPlayerKicked("System Kick"), Times.Once);
        }

        [Fact]
        public async Task GetLobbyState_Success_ReturnsDto()
        {
            var game = new Game { IdGame = GAME_ID, GameStatus = (int)GameStatus.InProgress, HostPlayerID = HOST_ID };
            var players = new List<Player> { new Player { Username = HOST, IdPlayer = HOST_ID } };

            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(players);

            var result = await _service.GetLobbyStateAsync(LOBBY_CODE);

            Assert.NotNull(result);
            Assert.True(result.IsGameStarted);
            Assert.Single(result.Players);
            Assert.True(result.Players[0].IsHost);
        }

        [Fact]
        public async Task GetPublicMatches_ReturnsAvailableGames()
        {
            var game = new Game { IdGame = GAME_ID, MaxPlayers = 4, LobbyCode = LOBBY_CODE, HostPlayerID = HOST_ID };
            _repoMock.Setup(r => r.GetActivePublicGamesAsync()).ReturnsAsync(new List<Game> { game });
            _repoMock.Setup(r => r.CountPlayersInGameAsync(GAME_ID)).ReturnsAsync(2);
            _repoMock.Setup(r => r.GetUsernameByIdAsync(HOST_ID)).ReturnsAsync(HOST);

            var result = await _service.GetPublicMatchesAsync();

            Assert.Single(result);
            Assert.Equal(LOBBY_CODE, result[0].LobbyCode);
            Assert.Equal(2, result[0].CurrentPlayers);
        }

        [Fact]
        public async Task GetPublicMatches_FullGame_ReturnsEmpty()
        {
            var game = new Game { IdGame = GAME_ID, MaxPlayers = 4 };
            _repoMock.Setup(r => r.GetActivePublicGamesAsync()).ReturnsAsync(new List<Game> { game });
            _repoMock.Setup(r => r.CountPlayersInGameAsync(GAME_ID)).ReturnsAsync(4);

            var result = await _service.GetPublicMatchesAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task CreateLobby_OldGameFinished_CleansStateAndCreates()
        {
            var host = new Player { IdPlayer = HOST_ID, GameIdGame = 10, IsGuest = false };
            var oldGame = new Game { IdGame = 10, GameStatus = (int)GameStatus.Finished };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetGameByIdAsync(10)).ReturnsAsync(oldGame);

            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };

            var res = await _service.CreateLobbyAsync(req);

            Assert.True(res.Success);
            Assert.NotEqual(10, host.GameIdGame);
            _repoMock.Verify(r => r.AddGame(It.IsAny<Game>()), Times.Once);
        }

        [Fact]
        public async Task CreateLobby_OldGameInProgress_DeniesCreation()
        {
            var host = new Player { IdPlayer = HOST_ID, GameIdGame = 10, IsGuest = false };
            var activeGame = new Game { IdGame = 10, GameStatus = (int)GameStatus.InProgress };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetGameByIdAsync(10)).ReturnsAsync(activeGame);

            var req = new CreateLobbyRequest
            {
                HostUsername = HOST,
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };

            var res = await _service.CreateLobbyAsync(req);

            Assert.False(res.Success);
            Assert.Equal(LobbyErrorType.PlayerAlreadyInGame, res.ErrorType);
            Assert.Equal(10, host.GameIdGame);
        }

        [Fact]
        public async Task LeaveLobby_LastPlayerWaiting_DeletesGame()
        {
            var user = new Player { Username = USER, IdPlayer = USER_ID, GameIdGame = GAME_ID };
            var game = new Game { IdGame = GAME_ID, GameStatus = (int)GameStatus.WaitingForPlayers };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(user);
            _repoMock.Setup(r => r.GetGameByIdAsync(GAME_ID)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player>());

            await _service.LeaveLobbyAsync(USER);

            _repoMock.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task LeaveLobby_LastPlayerInProgress_FinishesGame()
        {
            var user = new Player { Username = USER, IdPlayer = USER_ID, GameIdGame = GAME_ID };
            var game = new Game { IdGame = GAME_ID, GameStatus = (int)GameStatus.InProgress };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(user);
            _repoMock.Setup(r => r.GetGameByIdAsync(GAME_ID)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayersInGameAsync(GAME_ID)).ReturnsAsync(new List<Player> { new Player() });

            await _service.LeaveLobbyAsync(USER);

            Assert.Equal((int)GameStatus.Finished, game.GameStatus);
            _monitorMock.Verify(m => m.StopMonitoring(GAME_ID), Times.Once);
        }

        [Fact]
        public async Task DisbandLobby_GameNotFound_CleansOrphanId()
        {
            var host = new Player { Username = HOST, GameIdGame = 999 };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetGameByIdAsync(999)).ReturnsAsync((Game)null);

            await _service.DisbandLobbyAsync(HOST);

            Assert.Null(host.GameIdGame);
            _repoMock.Verify(r => r.DeleteGameAndCleanDependencies(It.IsAny<Game>()), Times.Never);
        }

        [Fact]
        public async Task StartGame_GameNotFound_ReturnsFalse()
        {
            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync((Game)null);
            var res = await _service.StartGameAsync(LOBBY_CODE);
            Assert.False(res);
        }

        [Fact]
        public async Task StartGame_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetGameByCodeAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.StartGameAsync(LOBBY_CODE));
        }

        [Fact]
        public async Task KickPlayer_SelfKick_ReturnsFalse()
        {
            var req = new KickPlayerRequest { RequestorUsername = HOST, TargetUsername = HOST };
            var res = await _service.KickPlayerAsync(req);
            Assert.False(res);
        }

        [Fact]
        public async Task KickPlayer_TargetNotInGame_ReturnsFalse()
        {
            var game = new Game { IdGame = GAME_ID, HostPlayerID = HOST_ID };
            var host = new Player { IdPlayer = HOST_ID };
            var target = new Player { IdPlayer = USER_ID, GameIdGame = 200 };

            var req = new KickPlayerRequest { LobbyCode = LOBBY_CODE, RequestorUsername = HOST, TargetUsername = USER };

            _repoMock.Setup(r => r.GetGameByCodeAsync(LOBBY_CODE)).ReturnsAsync(game);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(HOST)).ReturnsAsync(host);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(target);

            var res = await _service.KickPlayerAsync(req);
            Assert.False(res);
        }

        [Fact]
        public async Task LeaveLobby_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.LeaveLobbyAsync(USER));
        }

        [Fact]
        public async Task GetLobbyState_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetGameByCodeAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.GetLobbyStateAsync(LOBBY_CODE));
        }

        [Fact]
        public async Task GetPublicMatches_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetActivePublicGamesAsync()).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.GetPublicMatchesAsync());
        }

        [Fact]
        public async Task SystemKickPlayer_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetGameByCodeAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.SystemKickPlayerAsync(LOBBY_CODE, USER, "Reason"));
        }
    }
}