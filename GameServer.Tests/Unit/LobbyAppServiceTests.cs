using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class LobbyAppServiceTests : IDisposable
    {
        private readonly Mock<ILobbyRepository> _mockRepository;
        private readonly Mock<ILobbyServiceCallback> _mockCallback;
        private readonly LobbyAppService _service;

        private const string HostUser = "HostPlayer";
        private const string GuestUser = "GuestPlayer";
        private const string OtherUser = "OtherPlayer";
        private const string LobbyCode = "TEST01";
        private const int GameId = 100;

        public LobbyAppServiceTests()
        {
            _mockRepository = new Mock<ILobbyRepository>();
            _mockCallback = new Mock<ILobbyServiceCallback>();
            _service = new LobbyAppService(_mockRepository.Object);

            ResetStaticData();
        }

        public void Dispose()
        {
            ResetStaticData();
        }

        private void ResetStaticData()
        {
            var field = typeof(ConnectionManager).GetField("_lobbyCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                var dict = (Dictionary<string, ILobbyServiceCallback>)field.GetValue(null);
                lock (dict) { dict.Clear(); }
            }
        }


        [Fact]
        public async Task CreateLobby_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateLobbyRequest
            {
                HostUsername = HostUser,
                Settings = new LobbySettingsDto { MaxPlayers = 4, BoardId = 1, IsPublic = true }
            };

            var host = new Player { IdPlayer = 1, Username = HostUser, IsGuest = false, GameIdGame = null };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(HostUser)).ReturnsAsync(host);
            _mockRepository.Setup(r => r.IsLobbyCodeUnique(It.IsAny<string>())).Returns(true); 

            // Act
            var result = await _service.CreateLobbyAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.LobbyCode);
            _mockRepository.Verify(r => r.AddGame(It.IsAny<Game>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CreateLobby_GuestUser_ReturnsError()
        {
            // Arrange
            var request = new CreateLobbyRequest { HostUsername = GuestUser, Settings = new LobbySettingsDto { MaxPlayers = 4 } };
            var guest = new Player { IdPlayer = 2, Username = GuestUser, IsGuest = true };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(GuestUser)).ReturnsAsync(guest);

            // Act
            var result = await _service.CreateLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("invitados no pueden crear", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateLobby_AlreadyInGame_ReturnsError()
        {
            // Arrange
            var request = new CreateLobbyRequest { HostUsername = HostUser, Settings = new LobbySettingsDto { MaxPlayers = 4 } };
            var host = new Player { IdPlayer = 1, GameIdGame = 50 }; 
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(HostUser)).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetGameByIdAsync(50)).ReturnsAsync(new Game { GameStatus = (int)GameStatus.InProgress });

            // Act
            var result = await _service.CreateLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("partida activa", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateLobby_InvalidSettings_ReturnsError()
        {
            // Arrange
            var request = new CreateLobbyRequest { HostUsername = HostUser, Settings = new LobbySettingsDto { MaxPlayers = 1 } }; 

            // Act
            var result = await _service.CreateLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("entre 2 y 4", result.ErrorMessage);
        }



        [Fact]
        public async Task JoinLobby_Valid_ReturnsSuccessAndNotifies()
        {
            // Arrange
            var request = new JoinLobbyRequest { Username = OtherUser, LobbyCode = LobbyCode };
            var player = new Player { IdPlayer = 2, Username = OtherUser, GameIdGame = null };
            var game = new Game { IdGame = GameId, LobbyCode = LobbyCode, GameStatus = (int)GameStatus.WaitingForPlayers, MaxPlayers = 4, HostPlayerID = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { new Player { IdPlayer = 1, Username = HostUser } }); 

            // Registrar callback para verificar notificación
            ConnectionManager.RegisterLobbyClient(HostUser, _mockCallback.Object);

            // Act
            var result = await _service.JoinLobbyAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(GameId, player.GameIdGame);
            _mockCallback.Verify(c => c.OnPlayerJoined(It.Is<PlayerLobbyDto>(p => p.Username == OtherUser)), Times.Once);
        }

        [Fact]
        public async Task JoinLobby_LobbyFull_ReturnsError()
        {
            // Arrange
            var request = new JoinLobbyRequest { Username = OtherUser, LobbyCode = LobbyCode };
            var player = new Player { Username = OtherUser };
            var game = new Game { IdGame = GameId, MaxPlayers = 2 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { new Player(), new Player() });

            // Act
            var result = await _service.JoinLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("llena", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobby_GameStarted_ReturnsError()
        {
            // Arrange
            var request = new JoinLobbyRequest { Username = OtherUser, LobbyCode = LobbyCode };
            var player = new Player { Username = OtherUser };
            var game = new Game { IdGame = GameId, GameStatus = (int)GameStatus.InProgress }; 

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);

            // Act
            var result = await _service.JoinLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("comenzado", result.ErrorMessage);
        }


        [Fact]
        public async Task LeaveLobby_NormalUser_ReturnsSuccess()
        {
            // Arrange
            var player = new Player { IdPlayer = 2, Username = OtherUser, GameIdGame = GameId };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByIdAsync(GameId)).ReturnsAsync(new Game { GameStatus = (int)GameStatus.WaitingForPlayers });
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { new Player { Username = HostUser } }); // Queda el host

            // Act
            var result = await _service.LeaveLobbyAsync(OtherUser);

            // Assert
            Assert.True(result);
            Assert.Null(player.GameIdGame);
        }

        [Fact]
        public async Task DisbandLobby_Host_ReturnsSuccessAndNotifies()
        {
            // Arrange
            var host = new Player { IdPlayer = 1, Username = HostUser, GameIdGame = GameId };
            var game = new Game { IdGame = GameId, LobbyCode = LobbyCode, HostPlayerID = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(HostUser)).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetGameByIdAsync(GameId)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { new Player { Username = OtherUser } }); 

            ConnectionManager.RegisterLobbyClient(OtherUser, _mockCallback.Object);

            // Act
            await _service.DisbandLobbyAsync(HostUser);

            // Assert
            _mockRepository.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);
            _mockCallback.Verify(c => c.OnLobbyDisbanded(), Times.Once);
        }


        [Fact]
        public async Task StartGame_EnoughPlayers_ReturnsSuccess()
        {
            // Arrange
            var game = new Game { IdGame = GameId, LobbyCode = LobbyCode, GameStatus = (int)GameStatus.WaitingForPlayers };
            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { new Player(), new Player() });

            // Act
            var result = await _service.StartGameAsync(LobbyCode);

            // Assert
            Assert.True(result);
            Assert.Equal((int)GameStatus.InProgress, game.GameStatus);
        }

        [Fact]
        public async Task StartGame_NotEnoughPlayers_ReturnsFalse()
        {
            // Arrange
            var game = new Game { IdGame = GameId, LobbyCode = LobbyCode };
            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            // 1 Jugador
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { new Player() });

            // Act
            var result = await _service.StartGameAsync(LobbyCode);

            // Assert
            Assert.False(result);
            Assert.NotEqual((int)GameStatus.InProgress, game.GameStatus);
        }



        [Fact]
        public async Task KickPlayer_HostKicksTarget_ReturnsTrue()
        {
            // Arrange
            var request = new KickPlayerRequest { RequestorUsername = HostUser, TargetUsername = OtherUser, LobbyCode = LobbyCode };
            var game = new Game { IdGame = GameId, HostPlayerID = 1 };
            var host = new Player { IdPlayer = 1, Username = HostUser };
            var target = new Player { IdPlayer = 2, Username = OtherUser, GameIdGame = GameId, KickCount = 0 };

            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(HostUser)).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(target);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { host }); 

            // Act
            var result = await _service.KickPlayerAsync(request);

            // Assert
            Assert.True(result);
            Assert.Null(target.GameIdGame);
            Assert.Equal(1, target.KickCount);
        }

        [Fact]
        public async Task KickPlayer_NotHost_ReturnsFalse()
        {
            // Arrange 
            var request = new KickPlayerRequest { RequestorUsername = OtherUser, TargetUsername = HostUser, LobbyCode = LobbyCode };
            var game = new Game { IdGame = GameId, HostPlayerID = 1 };
            var notHost = new Player { IdPlayer = 2, Username = OtherUser };

            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(notHost);

            // Act
            var result = await _service.KickPlayerAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task KickPlayer_SelfKick_ReturnsFalse()
        {
            // Arrange
            var request = new KickPlayerRequest { RequestorUsername = HostUser, TargetUsername = HostUser }; // Mismo usuario

            // Act
            var result = await _service.KickPlayerAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetLobbyState_ValidCode_ReturnsDto()
        {
            // Arrange
            var game = new Game { IdGame = GameId, LobbyCode = LobbyCode, HostPlayerID = 1 };
            var p1 = new Player { Username = HostUser, IdPlayer = 1 };
            var p2 = new Player { Username = OtherUser, IdPlayer = 2 };

            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(GameId)).ReturnsAsync(new List<Player> { p1, p2 });

            // Act
            var state = await _service.GetLobbyStateAsync(LobbyCode);

            // Assert
            Assert.NotNull(state);
            Assert.Equal(2, state.Players.Count);
            Assert.True(state.Players.Find(p => p.Username == HostUser).IsHost);
        }



        [Fact]
        public async Task CreateLobby_SqlException_ReturnsConnectionError()
        {
            // Arrange
            var request = new CreateLobbyRequest { HostUsername = HostUser, Settings = new LobbySettingsDto { MaxPlayers = 4 } };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(HostUser)).ReturnsAsync(new Player());
            _mockRepository.Setup(r => r.IsLobbyCodeUnique(It.IsAny<string>())).Returns(true);
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(CreateSqlException(53));

            // Act
            var result = await _service.CreateLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Error de conexión.", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobby_DbUpdateException_ReturnsError()
        {
            // Arrange
            var request = new JoinLobbyRequest { Username = OtherUser, LobbyCode = LobbyCode };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(OtherUser)).ReturnsAsync(new Player());
            _mockRepository.Setup(r => r.GetGameByCodeAsync(LobbyCode)).ReturnsAsync(new Game { GameStatus = (int)GameStatus.WaitingForPlayers });
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException());

            // Act
            var result = await _service.JoinLobbyAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error al unirse", result.ErrorMessage);
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