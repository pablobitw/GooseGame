using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class LobbyAppServiceTests : IDisposable
    {
        private readonly Mock<ILobbyRepository> _mockRepository;
        private readonly LobbyAppService _service;

        public LobbyAppServiceTests()
        {
            ResetStaticState();
            _mockRepository = new Mock<ILobbyRepository>();
            _service = new LobbyAppService(_mockRepository.Object);
        }

        public void Dispose()
        {
            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var activeUsersField = typeof(ConnectionManager).GetField("_activeUsers", BindingFlags.NonPublic | BindingFlags.Static);
            var lobbyCallbacksField = typeof(ConnectionManager).GetField("_lobbyCallbacks", BindingFlags.NonPublic | BindingFlags.Static);

            if (activeUsersField != null)
            {
                var hashSet = activeUsersField.GetValue(null);
                hashSet?.GetType().GetMethod("Clear").Invoke(hashSet, null);
            }
            if (lobbyCallbacksField != null)
            {
                var dict = (IDictionary)lobbyCallbacksField.GetValue(null);
                dict?.Clear();
            }
        }

        [Fact]
        public async Task CreateLobbyAsync_ValidRequest_ShouldCreateGame()
        {
            var request = new CreateLobbyRequest
            {
                HostUsername = "Host",
                Settings = new LobbySettingsDto { MaxPlayers = 4, BoardId = 1, IsPublic = true }
            };
            var host = new Player { IdPlayer = 1, Username = "Host" };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);
            _mockRepository.Setup(r => r.IsLobbyCodeUnique(It.IsAny<string>())).Returns(true);

            var result = await _service.CreateLobbyAsync(request);

            Assert.True(result.Success);
            Assert.NotNull(result.LobbyCode);
            _mockRepository.Verify(r => r.AddGame(It.IsAny<Game>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateLobbyAsync_GuestUser_ShouldFail()
        {
            var request = new CreateLobbyRequest
            {
                HostUsername = "Guest_1",
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };
            var guest = new Player { IsGuest = true };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Guest_1")).ReturnsAsync(guest);

            var result = await _service.CreateLobbyAsync(request);

            Assert.False(result.Success);
            Assert.Contains("invitados", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateLobbyAsync_UserInGame_ShouldFail()
        {
            var request = new CreateLobbyRequest
            {
                HostUsername = "Host",
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };
            var host = new Player { GameIdGame = 100 };
            var activeGame = new Game { GameStatus = (int)GameStatus.WaitingForPlayers };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetGameByIdAsync(100)).ReturnsAsync(activeGame);

            var result = await _service.CreateLobbyAsync(request);

            Assert.False(result.Success);
            Assert.Contains("activa", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateLobbyAsync_SqlException_ShouldReturnConnectionError()
        {
            var request = new CreateLobbyRequest
            {
                HostUsername = "Host",
                Settings = new LobbySettingsDto { MaxPlayers = 4 }
            };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ThrowsAsync(CreateSqlException(53));

            var result = await _service.CreateLobbyAsync(request);

            Assert.False(result.Success);
            Assert.Equal("Error de conexión.", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobbyAsync_ValidRequest_ShouldAddPlayer()
        {
            var request = new JoinLobbyRequest { Username = "Player", LobbyCode = "ABCDE" };
            var player = new Player { IdPlayer = 2, Username = "Player" };
            var game = new Game { IdGame = 1, GameStatus = 0, MaxPlayers = 4, HostPlayerID = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByCodeAsync("ABCDE")).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player>());

            var result = await _service.JoinLobbyAsync(request);

            Assert.True(result.Success);
            Assert.Equal(1, player.GameIdGame);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task JoinLobbyAsync_LobbyFull_ShouldFail()
        {
            var request = new JoinLobbyRequest { Username = "Player", LobbyCode = "ABCDE" };
            var player = new Player { IdPlayer = 2 };
            var game = new Game { IdGame = 1, GameStatus = 0, MaxPlayers = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByCodeAsync("ABCDE")).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { new Player() });

            var result = await _service.JoinLobbyAsync(request);

            Assert.False(result.Success);
            Assert.Contains("llena", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobbyAsync_GameStarted_ShouldFail()
        {
            var request = new JoinLobbyRequest { Username = "Player", LobbyCode = "ABCDE" };
            var player = new Player { IdPlayer = 2 };
            var game = new Game { IdGame = 1, GameStatus = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByCodeAsync("ABCDE")).ReturnsAsync(game);

            var result = await _service.JoinLobbyAsync(request);

            Assert.False(result.Success);
            Assert.Contains("comenzado", result.ErrorMessage);
        }

        [Fact]
        public async Task GetLobbyStateAsync_ValidCode_ShouldReturnState()
        {
            string code = "CODE1";
            var game = new Game { IdGame = 1, HostPlayerID = 1, GameStatus = 0 };
            var players = new List<Player> { new Player { Username = "Host", IdPlayer = 1 } };

            _mockRepository.Setup(r => r.GetGameByCodeAsync(code)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);

            var result = await _service.GetLobbyStateAsync(code);

            Assert.NotNull(result);
            Assert.Single(result.Players);
            Assert.True(result.Players[0].IsHost);
        }

        [Fact]
        public async Task StartGameAsync_EnoughPlayers_ShouldUpdateStatus()
        {
            string code = "CODE1";
            var game = new Game { IdGame = 1, GameStatus = 0 };
            var players = new List<Player> { new Player(), new Player() };

            _mockRepository.Setup(r => r.GetGameByCodeAsync(code)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);

            var result = await _service.StartGameAsync(code);

            Assert.True(result);
            Assert.Equal(1, game.GameStatus);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task StartGameAsync_NotEnoughPlayers_ShouldFail()
        {
            string code = "CODE1";
            var game = new Game { IdGame = 1 };
            var players = new List<Player> { new Player() };

            _mockRepository.Setup(r => r.GetGameByCodeAsync(code)).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(players);

            var result = await _service.StartGameAsync(code);

            Assert.False(result);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task KickPlayerAsync_ValidRequest_ShouldRemovePlayer()
        {
            var req = new KickPlayerRequest { LobbyCode = "CODE", RequestorUsername = "Host", TargetUsername = "Target" };
            var game = new Game { IdGame = 1, HostPlayerID = 1 };
            var host = new Player { IdPlayer = 1 };
            var target = new Player { IdPlayer = 2, GameIdGame = 1, Username = "Target" };

            _mockRepository.Setup(r => r.GetGameByCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Target")).ReturnsAsync(target);

            await _service.KickPlayerAsync(req);

            Assert.Null(target.GameIdGame);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task KickPlayerAsync_NotHost_ShouldDoNothing()
        {
            var req = new KickPlayerRequest { LobbyCode = "CODE", RequestorUsername = "NotHost", TargetUsername = "Target" };
            var game = new Game { IdGame = 1, HostPlayerID = 1 };
            var notHost = new Player { IdPlayer = 3 };

            _mockRepository.Setup(r => r.GetGameByCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("NotHost")).ReturnsAsync(notHost);

            await _service.KickPlayerAsync(req);

            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task DisbandLobbyAsync_HostRequests_ShouldDeleteGame()
        {
            string hostName = "Host";
            var host = new Player { IdPlayer = 1, GameIdGame = 1 };
            var game = new Game { IdGame = 1, LobbyCode = "CODE" };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(hostName)).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            await _service.DisbandLobbyAsync(hostName);

            _mockRepository.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task LeaveLobbyAsync_LastPlayer_ShouldDeleteGame()
        {
            string user = "Player";
            var player = new Player { IdPlayer = 1, GameIdGame = 1 };
            var game = new Game { IdGame = 1, GameStatus = 0 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player>());

            var result = await _service.LeaveLobbyAsync(user);

            Assert.True(result);
            Assert.Null(player.GameIdGame);
            _mockRepository.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);
        }

        [Fact]
        public async Task LeaveLobbyAsync_GameInProgress_LastPlayerWins_ShouldFinishGame()
        {
            string user = "Loser";
            var player = new Player { IdPlayer = 1, GameIdGame = 1 };
            var game = new Game { IdGame = 1, GameStatus = 1 };
            var winner = new Player { Username = "Winner" };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            _mockRepository.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { winner });

            var result = await _service.LeaveLobbyAsync(user);

            Assert.True(result);
            Assert.Equal(2, game.GameStatus);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task GetPublicMatchesAsync_ReturnsOnlyAvailable_ShouldFilterFullGames()
        {
            var gameFull = new Game { IdGame = 1, MaxPlayers = 2 };
            var gameOpen = new Game { IdGame = 2, MaxPlayers = 2, LobbyCode = "OPEN", HostPlayerID = 1 };
            var list = new List<Game> { gameFull, gameOpen };

            _mockRepository.Setup(r => r.GetActivePublicGamesAsync()).ReturnsAsync(list);
            _mockRepository.Setup(r => r.CountPlayersInGameAsync(1)).ReturnsAsync(2);
            _mockRepository.Setup(r => r.CountPlayersInGameAsync(2)).ReturnsAsync(1);
            _mockRepository.Setup(r => r.GetUsernameByIdAsync(1)).ReturnsAsync("Host");

            var result = await _service.GetPublicMatchesAsync();

            Assert.Single(result);
            Assert.Equal("OPEN", result[0].LobbyCode);
        }

        [Fact]
        public void LobbyDtos_Serialization_ShouldWork()
        {
            var dto = new JoinLobbyResultDto { Success = true, IsHost = true };
            string xml = SerializeDto(dto);
            var deserialized = DeserializeDto<JoinLobbyResultDto>(xml);
            Assert.True(deserialized.Success);
        }

        [Fact]
        public void Constructor_NullRepository_ShouldThrowException()
        {
            Assert.Throws<ArgumentNullException>(() => new LobbyAppService(null));
        }

        [Fact]
        public async Task CreateLobbyAsync_NullRequest_ShouldReturnError()
        {
            var result = await _service.CreateLobbyAsync(null);
            Assert.False(result.Success);
            Assert.Equal("Datos inválidos.", result.ErrorMessage);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public async Task CreateLobbyAsync_InvalidMaxPlayers_ShouldReturnError(int maxPlayers)
        {
            var request = new CreateLobbyRequest
            {
                HostUsername = "Host",
                Settings = new LobbySettingsDto { MaxPlayers = maxPlayers, BoardId = 1, IsPublic = true }
            };
            var host = new Player { Username = "Host" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);

            var result = await _service.CreateLobbyAsync(request);

            Assert.False(result.Success);
            Assert.Contains("jugadores", result.ErrorMessage);
        }

        [Fact]
        public async Task KickPlayerAsync_HostKicksSelf_ShouldDoNothing()
        {
            var req = new KickPlayerRequest { LobbyCode = "CODE", RequestorUsername = "Host", TargetUsername = "Host" };
            var game = new Game { IdGame = 1, HostPlayerID = 1 };
            var host = new Player { IdPlayer = 1, GameIdGame = 1, Username = "Host" };

            _mockRepository.Setup(r => r.GetGameByCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);

            await _service.KickPlayerAsync(req);

            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task KickPlayerAsync_TargetNotInLobby_ShouldDoNothing()
        {
            var req = new KickPlayerRequest { LobbyCode = "CODE", RequestorUsername = "Host", TargetUsername = "Alien" };
            var game = new Game { IdGame = 1, HostPlayerID = 1 };
            var host = new Player { IdPlayer = 1 };
            var alien = new Player { IdPlayer = 2, GameIdGame = 99 };

            _mockRepository.Setup(r => r.GetGameByCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Alien")).ReturnsAsync(alien);

            await _service.KickPlayerAsync(req);

            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        private string SerializeDto<T>(T instance)
        {
            var serializer = new DataContractSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private T DeserializeDto<T>(string xml)
        {
            var serializer = new DataContractSerializer(typeof(T));
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            {
                return (T)serializer.ReadObject(stream);
            }
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