using GameServer.DTOs.Gameplay;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class GameplayAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayAppService));
        private static readonly Random RandomGenerator = new Random();
        private static readonly object _randomLock = new object();
        private static readonly int[] GooseTiles = { 5, 9, 14, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };
        private readonly GameplayRepository _repository;

        public GameplayAppService(GameplayRepository repository)
        {
            _repository = repository;
        }

        public async Task<DiceRollDTO> RollDiceAsync(GameplayRequest request)
        {
            DiceRollDTO result = null;
            try
            {
                if (request != null)
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);

                    if (game != null && game.GameStatus == (int)GameStatus.InProgress)
                    {
                        var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                        if (player != null)
                        {
                            var lastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, player.IdPlayer);
                            int currentPos = lastMove?.FinalPosition ?? 0;

                            int d1;
                            int d2;

                            lock (_randomLock)
                            {
                                d1 = RandomGenerator.Next(1, 7);
                                d2 = (currentPos < 60) ? RandomGenerator.Next(1, 7) : 0;
                            }

                            int total = d1 + d2;
                            int finalPos = currentPos + total;

                            string message = "";
                            bool isExtraTurn = false;

                            if (finalPos > 64)
                            {
                                int excess = finalPos - 64;
                                finalPos = 64 - excess;
                            }

                            if (finalPos == 64)
                            {
                                message = "¡HA LLEGADO A LA META!";
                                game.GameStatus = (int)GameStatus.Finished;
                                game.WinnerIdPlayer = player.IdPlayer;

                                await UpdateStatsEndGame(game.IdGame, player.IdPlayer);
                            }

                            else if (GooseTiles.Contains(finalPos))
                            {
                                int nextGoose = GooseTiles.FirstOrDefault(t => t > finalPos);
                                if (nextGoose != 0)
                                {
                                    message = $"¡De Oca a Oca ({finalPos} -> {nextGoose})! Tira de nuevo.";
                                    finalPos = nextGoose;
                                }
                                else
                                {
                                    message = "¡Oca (59)! Tira de nuevo.";
                                }
                                isExtraTurn = true;
                            }
                            else if (finalPos == 6 || finalPos == 12) { message = "¡Puente! Saltas a la Posada (19)."; finalPos = 19; }
                            else if (finalPos == 42) { message = "¡Laberinto! Retrocedes a la 30."; finalPos = 30; }
                            else if (finalPos == 58) { message = "¡CALAVERA! Regresas al inicio (1)."; finalPos = 1; }
                            else if (finalPos == 26 || finalPos == 53)
                            {
                                int bonus = finalPos;
                                message = $"¡Dados! Sumas {bonus} casillas extra.";
                                finalPos += bonus;
                                if (finalPos > 64) finalPos = 64 - (finalPos - 64);
                            }
                            else if (finalPos == 19) message = "¡Posada! Pierdes turno.";
                            else if (finalPos == 31) message = "¡Pozo! Esperas rescate.";
                            else if (finalPos == 56) message = "¡Cárcel! Esperas turno.";

                            string baseMsg = d2 > 0 ? $"{request.Username} tiró {d1} y {d2}." : $"{request.Username} tiró {d1}.";
                            string fullDescription = string.IsNullOrEmpty(message) ? $"{baseMsg} Avanza a {finalPos}." : $"{baseMsg} {message}";
                            if (isExtraTurn) fullDescription = "[EXTRA] " + fullDescription;

                            int turnNum = await _repository.GetMoveCountAsync(game.IdGame) + 1;

                            var move = new MoveRecord
                            {
                                GameIdGame = game.IdGame,
                                PlayerIdPlayer = player.IdPlayer,
                                DiceOne = d1,
                                DiceTwo = d2,
                                TurnNumber = turnNum,
                                ActionDescription = fullDescription,
                                StartPosition = currentPos,
                                FinalPosition = finalPos
                            };

                            _repository.AddMove(move);
                            await _repository.SaveChangesAsync();

                            result = new DiceRollDTO { DiceOne = d1, DiceTwo = d2, Total = total };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL en RollDice.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB en RollDice.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en RollDice.", ex);
            }

            return result;
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game == null) return false;

                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player == null) return false;

                var playerWithStats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
                if (playerWithStats != null && playerWithStats.PlayerStat != null)
                {
                    playerWithStats.PlayerStat.MatchesPlayed++;
                    playerWithStats.PlayerStat.MatchesLost++;
                }

                var allPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                player.GameIdGame = null;

                var remainingPlayers = allPlayers.Where(p => p.IdPlayer != player.IdPlayer).ToList();

                if (remainingPlayers.Count < 2)
                {
                    game.GameStatus = (int)GameStatus.Finished;

                    if (remainingPlayers.Count == 1)
                    {
                        var winner = remainingPlayers[0];
                        game.WinnerIdPlayer = winner.IdPlayer;

                        var winnerWithStats = await _repository.GetPlayerWithStatsByIdAsync(winner.IdPlayer);
                        if (winnerWithStats != null && winnerWithStats.PlayerStat != null)
                        {
                            winnerWithStats.PlayerStat.MatchesPlayed++;
                            winnerWithStats.PlayerStat.MatchesWon++;
                            winnerWithStats.TicketCommon += 1;
                        }

                        Log.InfoFormat("Juego {0} terminado por abandono. Ganador: {1}", game.LobbyCode, winner.Username);
                    }
                    else
                    {
                        game.WinnerIdPlayer = null;
                        Log.InfoFormat("Juego {0} terminado. Todos los jugadores abandonaron.", game.LobbyCode);
                    }
                }

                await _repository.SaveChangesAsync();
                return true;
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL al abandonar juego.", ex);
                return false;
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB al abandonar juego.", ex);
                return false;
            }
        }

        private async Task UpdateStatsEndGame(int gameId, int winnerId)
        {
           
            var players = await _repository.GetPlayersWithStatsInGameAsync(gameId);
            foreach (var p in players)
            {
                if (p.PlayerStat != null)
                {
                    p.PlayerStat.MatchesPlayed++;
                    if (p.IdPlayer == winnerId)
                    {
                        p.PlayerStat.MatchesWon++;
                        
                        p.TicketCommon += 1;
                    }
                    else
                    {
                        p.PlayerStat.MatchesLost++;
                    }
                }
            }
        }

        public async Task<GameStateDTO> GetGameStateAsync(GameplayRequest request)
        {
            GameStateDTO state = new GameStateDTO
            {
                GameLog = new List<string>(),
                PlayerPositions = new List<PlayerPositionDTO>()
            };

            try
            {
                if (request != null)
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                    if (game != null)
                    {
                        if (game.GameStatus == (int)GameStatus.Finished)
                        {
                            string winner = "Desconocido";

                            if (game.WinnerIdPlayer.HasValue)
                            {
                                var winnerPlayer = await _repository.GetPlayerByIdAsync(game.WinnerIdPlayer.Value);
                                winner = winnerPlayer?.Username ?? "Desconocido";
                            }
                            else
                            {
                                var winningMove = await _repository.GetWinningMoveAsync(game.IdGame);
                                if (winningMove != null)
                                {
                                    var winnerP = await _repository.GetPlayerByIdAsync(winningMove.PlayerIdPlayer);
                                    winner = winnerP?.Username ?? "Desconocido";
                                }
                                else
                                {
                                    winner = "Nadie";
                                }
                            }

                            return new GameStateDTO
                            {
                                IsGameOver = true,
                                WinnerUsername = winner,
                                GameLog = new List<string> { "La partida ha finalizado." },
                                PlayerPositions = new List<PlayerPositionDTO>(),
                                CurrentTurnUsername = "",
                                IsMyTurn = false
                            };
                        }

                        var activePlayers = await _repository.GetPlayersInGameAsync(game.IdGame);

                        if (activePlayers.Count > 0)
                        {
                            activePlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();

                            int totalMoves = await _repository.GetMoveCountAsync(game.IdGame);
                            int extraTurns = await _repository.GetExtraTurnCountAsync(game.IdGame);

                            int effectiveTurns = totalMoves - extraTurns;
                            int nextPlayerIndex = effectiveTurns % activePlayers.Count;
                            var currentTurnPlayer = activePlayers[nextPlayerIndex];

                            var lastMove = await _repository.GetLastGlobalMoveAsync(game.IdGame);
                            var logs = await _repository.GetGameLogsAsync(game.IdGame, 20);
                            var cleanLogs = logs.Select(l => l.Replace("[EXTRA] ", "")).ToList();

                            var playerPositions = new List<PlayerPositionDTO>();
                            foreach (var p in activePlayers)
                            {
                                var pLastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, p.IdPlayer);
                                playerPositions.Add(new PlayerPositionDTO
                                {
                                    Username = p.Username,
                                    CurrentTile = pLastMove?.FinalPosition ?? 0,
                                    IsOnline = true,
                                    AvatarPath = p.Avatar,
                                    IsMyTurn = (p.Username == currentTurnPlayer.Username)
                                });
                            }

                            state = new GameStateDTO
                            {
                                CurrentTurnUsername = currentTurnPlayer.Username,
                                IsMyTurn = (currentTurnPlayer.Username == request.Username),
                                LastDiceOne = lastMove?.DiceOne ?? 0,
                                LastDiceTwo = lastMove?.DiceTwo ?? 0,
                                GameLog = cleanLogs,
                                PlayerPositions = playerPositions,
                                IsGameOver = false,
                                WinnerUsername = null
                            };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo estado de juego.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo estado de juego.", ex);
            }

            return state;
        }
    }
}