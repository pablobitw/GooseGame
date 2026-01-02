using GameServer.DTOs.Gameplay;
using GameServer.Helpers;
using GameServer.Repositories;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public class GameplayAppServiceTests : IDisposable
    {
        private readonly Mock<IGameplayRepository> _mockRepo;
        private readonly GameplayAppService _service;

        public GameplayAppServiceTests()
        {
            ResetGameplayStaticState();
            ResetConnectionManager();

            _mockRepo = new Mock<IGameplayRepository>();
            _service = new GameplayAppService(_mockRepo.Object);
        }

        public void Dispose()
        {
            ResetGameplayStaticState();
            ResetConnectionManager();
        }


        [Fact]
        public async Task RollDiceAsync_RequestNulo_RetornaNull()
        {
            var result = await _service.RollDiceAsync(null);
            Assert.Null(result);
        }

        [Fact]
        public async Task RollDiceAsync_JuegoNoExiste_RetornaNull()
        {
            var req = new GameplayRequest { LobbyCode = "CODE", Username = "Player1" };
            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync((Game)null);

            var result = await _service.RollDiceAsync(req);
            Assert.Null(result);
        }

        [Fact]
        public async Task RollDiceAsync_TurnoValido_GeneraMovimientoYGuardaEnBD()
        {
            var req = new GameplayRequest { LobbyCode = "CODE", Username = "Player1" };
            var game = new Game { IdGame = 1, GameStatus = 1, LobbyCode = "CODE" }; 
            var p1 = new Player { IdPlayer = 10, Username = "Player1", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1 });
            _mockRepo.Setup(r => r.GetMoveCountAsync(1)).ReturnsAsync(0); 
            _mockRepo.Setup(r => r.GetLastMoveForPlayerAsync(1, 10)).ReturnsAsync((MoveRecord)null); 

            _mockRepo.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            var result = await _service.RollDiceAsync(req);

            Assert.NotNull(result);
            Assert.True(result.Total >= 2 && result.Total <= 12);

            _mockRepo.Verify(r => r.AddMove(It.IsAny<MoveRecord>()), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RollDiceAsync_JugadorGanaPartida_CambiaEstadoAFinished()
        {
            var req = new GameplayRequest { LobbyCode = "WIN", Username = "Winner" };
            var game = new Game { IdGame = 1, GameStatus = 1, LobbyCode = "WIN" };
            var p1 = new Player { IdPlayer = 10, Username = "Winner", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("WIN")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1 });
            _mockRepo.Setup(r => r.GetMoveCountAsync(1)).ReturnsAsync(0);

          
            _mockRepo.Setup(r => r.GetLastMoveForPlayerAsync(1, 10)).ReturnsAsync(new MoveRecord { FinalPosition = 60 });
            _mockRepo.Setup(r => r.GetGameByIdAsync(1)).ReturnsAsync(game);

            
            var result = await _service.RollDiceAsync(req);

            Assert.NotNull(result);
        }


        [Fact]
        public async Task LeaveGameAsync_JugadorSale_QuedanMasDeDos_JuegoContinua()
        {
            var req = new GameplayRequest { LobbyCode = "CODE", Username = "Leaver" };
            var game = new Game { IdGame = 1, GameStatus = 1, LobbyCode = "CODE" };
            var leaver = new Player { IdPlayer = 10, Username = "Leaver", GameIdGame = 1 };
            var p2 = new Player { IdPlayer = 20 };
            var p3 = new Player { IdPlayer = 30 };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Leaver")).ReturnsAsync(leaver);
            _mockRepo.Setup(r => r.GetPlayerWithStatsByIdAsync(10)).ReturnsAsync(leaver);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { leaver, p2, p3 });

            var result = await _service.LeaveGameAsync(req);

            Assert.True(result);
            Assert.Equal(1, game.GameStatus); 
            Assert.Null(leaver.GameIdGame);
        }

        [Fact]
        public async Task LeaveGameAsync_JugadorSale_QuedaSoloUno_JuegoTermina()
        {
            var req = new GameplayRequest { LobbyCode = "CODE", Username = "Leaver" };
            var game = new Game { IdGame = 1, GameStatus = 1, LobbyCode = "CODE" };
            var leaver = new Player { IdPlayer = 10, Username = "Leaver", GameIdGame = 1 };
            var winner = new Player { IdPlayer = 20, Username = "Winner", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("CODE")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Leaver")).ReturnsAsync(leaver);
            _mockRepo.Setup(r => r.GetPlayerWithStatsByIdAsync(10)).ReturnsAsync(leaver);
            _mockRepo.Setup(r => r.GetPlayerWithStatsByIdAsync(20)).ReturnsAsync(winner); 
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { leaver, winner });

            var result = await _service.LeaveGameAsync(req);

            Assert.True(result);
            Assert.Equal(2, game.GameStatus);
            Assert.Equal(20, game.WinnerIdPlayer);
        }


        [Fact]
        public async Task InitiateVoteKickAsync_UsuarioNoEnPartida_Falla()
        {
            var req = new VoteRequestDto { Username = "Ghost", TargetUsername = "P2" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Ghost")).ReturnsAsync((Player)null);

            await Assert.ThrowsAsync<System.ServiceModel.FaultException>(() =>
                _service.InitiateVoteKickAsync(req));
        }

        [Fact]
        public async Task InitiateVoteKickAsync_AutoVoto_Falla()
        {
            var req = new VoteRequestDto { Username = "P1", TargetUsername = "P1" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.InitiateVoteKickAsync(req));
        }

        [Fact]
        public async Task InitiateVoteKickAsync_MenosDe3Jugadores_Falla()
        {
            var req = new VoteRequestDto { Username = "P1", TargetUsername = "P2" };
            var p1 = new Player { IdPlayer = 1, GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("P1")).ReturnsAsync(p1);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1, new Player() });

            await Assert.ThrowsAsync<System.ServiceModel.FaultException>(() =>
                _service.InitiateVoteKickAsync(req));
        }

        [Fact]
        public async Task InitiateVoteKickAsync_Valido_RegistraVotoInicial()
        {
            var req = new VoteRequestDto { Username = "P1", TargetUsername = "P2", Reason = "AFK" };
            var p1 = new Player { IdPlayer = 1, Username = "P1", GameIdGame = 1 };
            var p2 = new Player { IdPlayer = 2, Username = "P2", GameIdGame = 1 };
            var p3 = new Player { IdPlayer = 3, Username = "P3", GameIdGame = 1 };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("P1")).ReturnsAsync(p1);
            _mockRepo.Setup(r => r.GetPlayersInGameAsync(1)).ReturnsAsync(new List<Player> { p1, p2, p3 });
            _mockRepo.Setup(r => r.GetLastMoveForPlayerAsync(1, 2)).ReturnsAsync(new MoveRecord { FinalPosition = 10 });

            await _service.InitiateVoteKickAsync(req);

          
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.InitiateVoteKickAsync(req)); 
        }


        [Fact]
        public async Task GetGameStateAsync_JuegoTerminado_DevuelveGanador()
        {
            var req = new GameplayRequest { LobbyCode = "FIN", Username = "P1" };
            var game = new Game { IdGame = 1, GameStatus = 2, WinnerIdPlayer = 50 }; 
            var winner = new Player { IdPlayer = 50, Username = "Champion" };

            _mockRepo.Setup(r => r.GetGameByLobbyCodeAsync("FIN")).ReturnsAsync(game);
            _mockRepo.Setup(r => r.GetPlayerByIdAsync(50)).ReturnsAsync(winner);

            var state = await _service.GetGameStateAsync(req);

            Assert.True(state.IsGameOver);
            Assert.Equal("Champion", state.WinnerUsername);
        }


        private void ResetGameplayStaticState()
        {
            var type = typeof(GameplayAppService);
            var fields = new[] { "_processingGames", "_lastGameActivity", "_afkStrikes", "_activeVotes" };

            foreach (var fieldName in fields)
            {
                var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                {
                    var dict = field.GetValue(null) as IDictionary;
                    dict?.Clear();
                }
            }
        }

        private void ResetConnectionManager()
        {
            var type = typeof(ConnectionManager);
            var field = type.GetField("_gameplayCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                var dict = field.GetValue(null) as IDictionary;
                dict?.Clear();
            }
        }
    }
}