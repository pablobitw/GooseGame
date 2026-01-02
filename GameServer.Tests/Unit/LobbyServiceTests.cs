using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public class LobbyServiceTests : IDisposable
    {
        private readonly Mock<ILobbyRepository> _mockRepo;
        private readonly LobbyAppService _service;
        private readonly Mock<ILobbyServiceCallback> _mockCallback;

        public LobbyServiceTests()
        {
            ResetConnectionManager();

            _mockRepo = new Mock<ILobbyRepository>();
            _service = new LobbyAppService(_mockRepo.Object);
            _mockCallback = new Mock<ILobbyServiceCallback>();
        }

        public void Dispose()
        {
            ResetConnectionManager();
        }

        private void ResetConnectionManager()
        {
            var type = typeof(ConnectionManager);
            var lobbyField = type.GetField("_lobbyCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
            if (lobbyField != null)
            {
                var dict = (Dictionary<string, ILobbyServiceCallback>)lobbyField.GetValue(null);
                dict.Clear();
            }
        }

        [Fact]
        public async Task CreateLobby_RequestNulo_RetornaFallo()
        {
            var result = await _service.CreateLobbyAsync(null);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreateLobby_HostNoExiste_RetornaFallo()
        {
            var req = new CreateLobbyRequest { HostUsername = "Ghost", Settings = new LobbySettingsDto() };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Ghost")).ReturnsAsync((Player)null);

            var result = await _service.CreateLobbyAsync(req);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreateLobby_HostEsInvitado_RetornaFallo()
        {
            var req = new CreateLobbyRequest { HostUsername = "Guest_1", Settings = new LobbySettingsDto() };
            var guest = new Player { Username = "Guest_1", IsGuest = true };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Guest_1")).ReturnsAsync(guest);

            var result = await _service.CreateLobbyAsync(req);

            Assert.Equal("Los invitados no pueden crear partidas.", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateLobby_HostYaEnPartida_RetornaFallo()
        {
            // CORRECCIÓN AQUÍ: Usamos LobbySettingsDto
            var req = new CreateLobbyRequest { HostUsername = "Host", Settings = new LobbySettingsDto() };
            var player = new Player { Username = "Host", GameIdGame = 99 }; 
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.GetGameByIdAsync(99)).ReturnsAsync(new Game { GameStatus = 1 });

            var result = await _service.CreateLobbyAsync(req);

            Assert.Equal("Ya estás en una partida activa.", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateLobby_FlujoExitoso_RetornaSuccess()
        {
            var req = new CreateLobbyRequest { HostUsername = "Host", Settings = new LobbySettingsDto { BoardId = 1, MaxPlayers = 4 } };
            var player = new Player { IdPlayer = 10, Username = "Host" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.IsLobbyCodeUnique(It.IsAny<string>())).Returns(true);

            var result = await _service.CreateLobbyAsync(req);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task CreateLobby_FlujoExitoso_GeneraCodigoLobby()
        {
            var req = new CreateLobbyRequest { HostUsername = "Host", Settings = new LobbySettingsDto() };
            var player = new Player { IdPlayer = 10, Username = "Host" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.IsLobbyCodeUnique(It.IsAny<string>())).Returns(true);

            var result = await _service.CreateLobbyAsync(req);

            Assert.NotNull(result.LobbyCode);
            Assert.Equal(5, result.LobbyCode.Length);
        }

        [Fact]
        public async Task CreateLobby_FlujoExitoso_GuardaJuegoEnBD()
        {
            var req = new CreateLobbyRequest { HostUsername = "Host", Settings = new LobbySettingsDto() };
            var player = new Player { IdPlayer = 10, Username = "Host" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.IsLobbyCodeUnique(It.IsAny<string>())).Returns(true);

            await _service.CreateLobbyAsync(req);

            _mockRepo.Verify(r => r.AddGame(It.IsAny<Game>()), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task JoinLobby_JugadorNoExiste_RetornaFallo()
        {
            var req = new JoinLobbyRequest { Username = "Ghost", LobbyCode = "ABCDE" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Ghost")).ReturnsAsync((Player)null);

            var result = await _service.JoinLobbyAsync(req);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task JoinLobby_PartidaNoExiste_RetornaFallo()
        {
            var req = new JoinLobbyRequest { Username = "Player", LobbyCode = "INVALID" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(new Player());
            _mockRepo.Setup(r => r.GetGameByCodeAsync("INVALID")).ReturnsAsync((Game)null);

            var result = await _service.JoinLobbyAsync(req);

            Assert.Equal("Código de partida no encontrado.", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobby_PartidaLlena_RetornaFallo()
        {
            var req = new JoinLobbyRequest { Username = "Player", LobbyCode = "FULL1" };
            var game = new Game { IdGame = 1, MaxPlayers = 2, GameStatus = 0 }; // Waiting

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(new Player());
            _mockRepo.Setup(r => r.GetGameByCodeAsync("FULL1")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { new Player(), new Player() });

            var result = await _service.JoinLobbyAsync(req);

            Assert.Equal("La partida está llena.", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobby_PartidaYaIniciada_RetornaFallo()
        {
            var req = new JoinLobbyRequest { Username = "Player", LobbyCode = "BUSY1" };
            var game = new Game { IdGame = 1, GameStatus = 1 }; // 1 = InProgress

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(new Player());
            _mockRepo.Setup(r => r.GetGameByCodeAsync("BUSY1")).ReturnsAsync(game);

            var result = await _service.JoinLobbyAsync(req);

            Assert.Equal("La partida ya ha comenzado.", result.ErrorMessage);
        }

        [Fact]
        public async Task JoinLobby_Exito_RetornaSuccess()
        {
            var req = new JoinLobbyRequest { Username = "Player", LobbyCode = "GOOD1" };
            var game = new Game { IdGame = 1, MaxPlayers = 4, GameStatus = 0 };
            var player = new Player { IdPlayer = 20, Username = "Player" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.GetGameByCodeAsync("GOOD1")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player>());

            var result = await _service.JoinLobbyAsync(req);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task JoinLobby_Exito_AsignaGameIdAJugador()
        {
            var req = new JoinLobbyRequest { Username = "Player", LobbyCode = "GOOD1" };
            var game = new Game { IdGame = 55 };
            var player = new Player { IdPlayer = 20 };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Player")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.GetGameByCodeAsync("GOOD1")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(55)).ReturnsAsync(new List<Player>());

            await _service.JoinLobbyAsync(req);

            Assert.Equal(55, player.GameIdGame);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartGame_JugadoresInsuficientes_RetornaFalse()
        {
            var game = new Game { IdGame = 1, LobbyCode = "SOLO" };
            _mockRepo.Setup(r => r.GetGameByCodeAsync("SOLO")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { new Player() });

            bool result = await _service.StartGameAsync("SOLO");

            Assert.False(result);
        }

        [Fact]
        public async Task StartGame_SuficientesJugadores_RetornaTrue()
        {
            var game = new Game { IdGame = 1, LobbyCode = "DUO" };
            _mockRepo.Setup(r => r.GetGameByCodeAsync("DUO")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { new Player(), new Player() });

            bool result = await _service.StartGameAsync("DUO");

            Assert.True(result);
        }

        [Fact]
        public async Task StartGame_Exito_CambiaEstadoAInProgress()
        {
            var game = new Game { IdGame = 1, LobbyCode = "DUO", GameStatus = 0 };
            _mockRepo.Setup(r => r.GetGameByCodeAsync("DUO")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { new Player(), new Player() });

            await _service.StartGameAsync("DUO");

            Assert.Equal(1, game.GameStatus);
        }

        [Fact]
        public async Task DisbandLobby_HostValido_EliminaJuego()
        {
            var host = new Player { Username = "Host", GameIdGame = 100 };
            var game = new Game { IdGame = 100, LobbyCode = "CODE" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);
            _mockRepo.Setup(r => r.GetGameByIdAsync(100)).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(100)).ReturnsAsync(new List<Player>());

            await _service.DisbandLobbyAsync("Host");

            _mockRepo.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DisbandLobby_JugadorSinJuego_NoHaceNada()
        {
            var player = new Player { Username = "Alone", GameIdGame = null };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Alone")).ReturnsAsync(player);

            await _service.DisbandLobbyAsync("Alone");

            _mockRepo.Verify(r => r.DeleteGameAndCleanDependencies(It.IsAny<Game>()), Times.Never);
        }

        [Fact]
        public async Task LeaveLobby_JugadorEnPartida_LimpiaIdJuego()
        {
            var player = new Player { Username = "Leaver", GameIdGame = 50 };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Leaver")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.GetGameByIdAsync(50)).ReturnsAsync(new Game { IdGame = 50, GameStatus = 0 });
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(50)).ReturnsAsync(new List<Player> { new Player() });

            bool result = await _service.LeaveLobbyAsync("Leaver");

            Assert.True(result);
            Assert.Null(player.GameIdGame);
        }

        [Fact]
        public async Task LeaveLobby_UltimoJugador_EliminaJuego()
        {
            var player = new Player { Username = "LastOne", GameIdGame = 50 };
            var game = new Game { IdGame = 50, GameStatus = 0 };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("LastOne")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.GetGameByIdAsync(50)).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(50)).ReturnsAsync(new List<Player>());

            await _service.LeaveLobbyAsync("LastOne");

            _mockRepo.Verify(r => r.DeleteGameAndCleanDependencies(game), Times.Once);
        }

        [Fact]
        public async Task KickPlayer_HostValido_ExpulsaJugador()
        {
            var req = new KickPlayerRequest { LobbyCode = "CODE", RequestorUsername = "Host", TargetUsername = "Target" };
            var game = new Game { IdGame = 1, HostPlayerID = 10 };
            var host = new Player { IdPlayer = 10, Username = "Host" };
            var target = new Player { IdPlayer = 20, Username = "Target", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetGameByCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Host")).ReturnsAsync(host);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Target")).ReturnsAsync(target);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { host });

            await _service.KickPlayerAsync(req);

            Assert.Null(target.GameIdGame);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task KickPlayer_NoEsHost_NoExpulsa()
        {
            var req = new KickPlayerRequest { LobbyCode = "CODE", RequestorUsername = "Imposter", TargetUsername = "Target" };
            var game = new Game { IdGame = 1, HostPlayerID = 10 };
            var imposter = new Player { IdPlayer = 99, Username = "Imposter" };

            _mockRepo.Setup(r => r.GetGameByCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Imposter")).ReturnsAsync(imposter);

            await _service.KickPlayerAsync(req);

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        }
    }
}