using global::GameServer.Models;
using global::GameServer.Repositories;
using global::GameServer.Services.Logic;
using global::GameServer;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class SanctionAppServiceTests : IDisposable
    {
        private readonly Mock<IGameplayRepository> _mockRepository;
        private readonly SanctionAppService _service;
        private const string TestUser = "SanctionedPlayer";
        private const string TestLobby = "LOBBY123";

        public SanctionAppServiceTests()
        {
            _mockRepository = new Mock<IGameplayRepository>();
            // El servicio ahora recibe la interfaz mockeada por constructor
            _service = new SanctionAppService(_mockRepository.Object);
        }

        public void Dispose()
        {
            _service.Dispose();
        }

        [Fact]
        public async Task ProcessKickAsync_FirstTime_IncrementsKickCount()
        {
            // Arrange
            var player = new Player { IdPlayer = 1, Username = TestUser, KickCount = 0 };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(TestUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetPlayerWithStatsByIdAsync(1)).ReturnsAsync(new Player { PlayerStat = new PlayerStat() });

            // Act
            await _service.ProcessKickAsync(TestUser, TestLobby, "Spam", "CHAT");

            // Assert
            Assert.Equal(1, player.KickCount);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessKickAsync_ThirdStrike_AppliesAutoBan()
        {
            // Arrange
            // El jugador ya tiene 2 faltas, la siguiente será la 3ra (Ban)
            var player = new Player { IdPlayer = 1, Username = TestUser, KickCount = 2, Account_IdAccount = 10 };
            var game = new Game { IdGame = 100, LobbyCode = TestLobby };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(TestUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetPlayerWithStatsByIdAsync(1)).ReturnsAsync(new Player { PlayerStat = new PlayerStat() });

            // CORRECCIÓN CLAVE: Configuramos el juego para que el service encuentre un GameId != 0
            _mockRepository.Setup(r => r.GetGameByLobbyCodeAsync(TestLobby)).ReturnsAsync(game);

            // Act
            await _service.ProcessKickAsync(TestUser, TestLobby, "Toxic", "CHAT");

            // Assert
            Assert.True(player.IsBanned);
            // Verificamos que se llamó a AddSanction con SanctionType 2 (Auto-Ban)
            _mockRepository.Verify(r => r.AddSanction(It.Is<Sanction>(s => s.SanctionType == 2)), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessKickAsync_GuestPlayer_SkipsPersistentSanction()
        {
            // Arrange
            // Account_IdAccount null o 0 simula un invitado
            var guest = new Player { IdPlayer = 1, Username = "Guest_123", Account_IdAccount = 0 };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("Guest_123")).ReturnsAsync(guest);

            // Act
            await _service.ProcessKickAsync("Guest_123", TestLobby, "Leaver", "GAME");

            // Assert
            // No debe intentar agregar nada a la tabla de sanciones persistentes
            _mockRepository.Verify(r => r.AddSanction(It.IsAny<Sanction>()), Times.Never);
        }

        [Fact]
        public async Task ProcessKickAsync_InActiveGame_RecordsLoss()
        {
            // Arrange
            var player = new Player { IdPlayer = 1, Username = TestUser, GameIdGame = 500 };
            var stats = new PlayerStat { MatchesPlayed = 10, MatchesLost = 5 };
            var playerWithStats = new Player { IdPlayer = 1, PlayerStat = stats };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(TestUser)).ReturnsAsync(player);
            _mockRepository.Setup(r => r.GetPlayerWithStatsByIdAsync(1)).ReturnsAsync(playerWithStats);

            // Act
            await _service.ProcessKickAsync(TestUser, TestLobby, "AFK", "GAME");

            // Assert
            Assert.Equal(11, stats.MatchesPlayed);
            Assert.Equal(6, stats.MatchesLost);
        }

        [Fact]
        public async Task ProcessKickAsync_SqlException_HandlesGracefully()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(TestUser)).ThrowsAsync(CreateSqlException(547));

            // Act
            var exception = await Record.ExceptionAsync(() => _service.ProcessKickAsync(TestUser, TestLobby, "Error", "TEST"));

            // Assert
            Assert.Null(exception); // Debe capturar el error internamente y no crashear el servidor
        }

        [Fact]
        public async Task ProcessKickAsync_EntityException_LogsError()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(TestUser)).ThrowsAsync(new EntityException("DB Error"));

            // Act
            var exception = await Record.ExceptionAsync(() => _service.ProcessKickAsync(TestUser, TestLobby, "Error", "TEST"));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public async Task ProcessKickAsync_PlayerNotFound_DoesNothing()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ReturnsAsync((Player)null);

            // Act
            await _service.ProcessKickAsync("Unknown", TestLobby, "None", "TEST");

            // Assert
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act
            _service.Dispose();
            var exception = Record.Exception(() => _service.Dispose());

            // Assert
            Assert.Null(exception);
        }

        private SqlException CreateSqlException(int number)
        {
            var collectionConstructor = typeof(SqlErrorCollection).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
            var errorCollection = (SqlErrorCollection)collectionConstructor.Invoke(null);
            var errorConstructor = typeof(SqlError).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int) }, null);
            var error = (SqlError)errorConstructor.Invoke(new object[] { number, (byte)0, (byte)0, "server", "msg", "proc", 100 });
            typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(errorCollection, new object[] { error });
            var exceptionConstructor = typeof(SqlException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid) }, null);
            return (SqlException)exceptionConstructor.Invoke(new object[] { "Error simulated", errorCollection, null, Guid.NewGuid() });
        }
    }
}