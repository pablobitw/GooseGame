#nullable disable 
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Services.Common;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class SanctionAppServiceTests
    {
        private readonly Mock<IGameplayRepository> _repoMock;
        private readonly Mock<IGameplayConnectionManager> _connMock;
        private readonly Mock<ISanctionServiceFactory> _factoryMock;
        private readonly Mock<ILobbyServiceWrapper> _lobbyServiceMock;
        private readonly Mock<IGameplayServiceCallback> _callbackMock;

        private readonly SanctionAppService _service;

        private const string USER = "BadPlayer";
        private const string LOBBY = "Lobby1";
        private const string REASON = "AFK";
        private const string SOURCE = "VoteKick";
        private const int ID_PLAYER = 10;
        private const int ID_ACCOUNT = 100;
        private const int ID_GAME = 500;

        public SanctionAppServiceTests()
        {
            _repoMock = new Mock<IGameplayRepository>();
            _connMock = new Mock<IGameplayConnectionManager>();
            _factoryMock = new Mock<ISanctionServiceFactory>();
            _lobbyServiceMock = new Mock<ILobbyServiceWrapper>();
            _callbackMock = new Mock<IGameplayServiceCallback>();

            _factoryMock.Setup(f => f.CreateLobbyService()).Returns(_lobbyServiceMock.Object);

            _service = new SanctionAppService(
                _repoMock.Object,
                _connMock.Object,
                _factoryMock.Object
            );
        }

        private SqlException CreateSqlException()
        {
            var collection = FormatterServices.GetUninitializedObject(typeof(SqlErrorCollection)) as SqlErrorCollection;
            var exception = FormatterServices.GetUninitializedObject(typeof(SqlException)) as SqlException;
            FieldInfo errorsField = typeof(SqlException).GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (errorsField != null) errorsField.SetValue(exception, collection);
            return exception;
        }

        [Fact]
        public async Task ProcessKick_UserNotFound_DoesNothing()
        {
            // Ahora esto es válido gracias a #nullable disable
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync((Player)null);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ProcessKick_GuestUser_UpdatesStats_NoSanctionRecord()
        {
            var player = new Player { IdPlayer = ID_PLAYER, KickCount = 0, Account_IdAccount = null };
            var playerWithStats = new Player { PlayerStat = new PlayerStat() };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(ID_PLAYER)).ReturnsAsync(playerWithStats);
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _connMock.Setup(c => c.GetGameplayClient(USER)).Returns(_callbackMock.Object);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            Assert.Equal(1, player.KickCount);
            Assert.Equal(1, playerWithStats.PlayerStat.KicksReceived);
            _repoMock.Verify(r => r.AddSanction(It.IsAny<Sanction>()), Times.Never);
            _connMock.Verify(c => c.UnregisterGameplayClient(USER), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_RegisteredUser_AddsSanctionRecord()
        {
            var player = new Player { IdPlayer = ID_PLAYER, KickCount = 0, Account_IdAccount = ID_ACCOUNT, GameIdGame = ID_GAME };
            var playerWithStats = new Player { PlayerStat = new PlayerStat() };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(ID_PLAYER)).ReturnsAsync(playerWithStats);
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _repoMock.Verify(r => r.AddSanction(It.Is<Sanction>(s => s.SanctionType == 1 && s.Game_IdGame == ID_GAME)), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_BanThresholdReached_BansUser()
        {
            var player = new Player { IdPlayer = ID_PLAYER, KickCount = 2, Account_IdAccount = ID_ACCOUNT, GameIdGame = ID_GAME };
            var playerWithStats = new Player { PlayerStat = new PlayerStat() };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(ID_PLAYER)).ReturnsAsync(playerWithStats);
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            Assert.True(player.IsBanned);
            Assert.Equal(3, player.KickCount);
            _repoMock.Verify(r => r.AddSanction(It.Is<Sanction>(s => s.SanctionType == 2 && s.Reason.Contains("AUTO-BAN"))), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_InGame_UpdatesLoseStats()
        {
            var player = new Player { IdPlayer = ID_PLAYER, GameIdGame = ID_GAME };
            var playerWithStats = new Player
            {
                PlayerStat = new PlayerStat { MatchesPlayed = 10, MatchesLost = 5 }
            };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(ID_PLAYER)).ReturnsAsync(playerWithStats);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            Assert.Equal(11, playerWithStats.PlayerStat.MatchesPlayed);
            Assert.Equal(6, playerWithStats.PlayerStat.MatchesLost);
        }

        [Fact]
        public async Task ProcessKick_GameLookupByLobby_AddsSanction()
        {
            var player = new Player { IdPlayer = ID_PLAYER, Account_IdAccount = ID_ACCOUNT, GameIdGame = null };
            var game = new Game { IdGame = 999 };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.GetPlayerWithStatsByIdAsync(ID_PLAYER)).ReturnsAsync(new Player());
            _repoMock.Setup(r => r.GetGameByLobbyCodeAsync(LOBBY)).ReturnsAsync(game);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _repoMock.Verify(r => r.AddSanction(It.Is<Sanction>(s => s.Game_IdGame == 999)), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_Notification_Success()
        {
            var player = new Player { IdPlayer = ID_PLAYER };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _connMock.Setup(c => c.GetGameplayClient(USER)).Returns(_callbackMock.Object);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _callbackMock.Verify(c => c.OnPlayerKicked(REASON), Times.Once);
            _connMock.Verify(c => c.UnregisterGameplayClient(USER), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_Notification_Exception_UnregistersAnyway()
        {
            var player = new Player { IdPlayer = ID_PLAYER };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);
            _connMock.Setup(c => c.GetGameplayClient(USER)).Returns(_callbackMock.Object);
            _callbackMock.Setup(c => c.OnPlayerKicked(It.IsAny<string>())).Throws(new Exception());

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _connMock.Verify(c => c.UnregisterGameplayClient(USER), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_LobbyTask_CallsSystemKick()
        {
            var player = new Player { IdPlayer = ID_PLAYER };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(player);

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            await Task.Delay(200);
            _lobbyServiceMock.Verify(l => l.SystemKickPlayerAsync(LOBBY, USER, REASON), Times.Once);
            _lobbyServiceMock.Verify(l => l.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ProcessKick_SqlException_LogsAndDoesNotCrash()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ThrowsAsync(CreateSqlException());

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ProcessKick_EntityException_LogsAndDoesNotCrash()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ThrowsAsync(new EntityException());

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ProcessKick_GeneralException_LogsAndDoesNotCrash()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ThrowsAsync(new Exception());

            await _service.ProcessKickAsync(USER, LOBBY, REASON, SOURCE);

            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public void Dispose_CallsRepositoryDispose()
        {
            _service.Dispose();
            _repoMock.Verify(r => r.Dispose(), Times.Once);
        }
    }
}